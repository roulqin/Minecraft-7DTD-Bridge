using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MC7DTD.Bridge;

var configPath = Path.GetFullPath(args.Length == 1 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../config/network.json"));
using var config = JsonDocument.Parse(File.ReadAllText(configPath));
var host = config.RootElement.GetProperty("host").GetString();
var port = config.RootElement.GetProperty("port").GetInt32();
if (host != "localhost" || port < 1024 || port > 65535)
    throw new InvalidDataException("network.json requires host=localhost and port=1024..65535");
var coordinatePath = Path.Combine(Path.GetDirectoryName(configPath)!, "coordinate.json");
var mapper = CoordinateMapper.Load(coordinatePath);
var builder = WebApplication.CreateBuilder();
builder.WebHost.ConfigureKestrel(o => o.ListenLocalhost(port));
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
var app = builder.Build();
var peers = new ConcurrentDictionary<string, Peer>();
var registrationGate = new SemaphoreSlim(1, 1);
var entities = new EntityRegistry();
var nativeEntities = new NativeEntityRegistry();
var healthComponents = new HealthComponents();
var equipmentComponents = new EquipmentComponents(healthComponents);
var equipmentPermissions = new EquipmentPermissions(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../config")));
var presentation = new PresentationComponent(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../config/entity_types.json")));
var componentPermissions = ComponentPermissions.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../config")));
var inspector = new EntityInspector(entities, nativeEntities, healthComponents, presentation, equipmentComponents);
var debugConfig = MC7DTD.DebugConfig.Load(Path.Combine(Path.GetDirectoryName(configPath)!, "debug.json"), message => app.Logger.LogWarning("{DebugWarning}", message));
InspectorEndpoints.Map(app, inspector, registrationGate, debugConfig);
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.MapGet("/health", () => Results.Json(new { status = "ok", phase = 1, clients = peers.Keys.Order().ToArray() }));
app.Map("/ws", async context =>
{
    // No browser-origin connections. This local development bridge has no authentication.
    if (!context.WebSockets.IsWebSocketRequest || context.Request.Headers.ContainsKey("Origin") ||
        context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote))
    { context.Response.StatusCode = 400; return; }
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, app.Lifetime.ApplicationStopping);
    var ct = lifetime.Token;
    var peer = new Peer(socket);
    string? role = null;
    var registered = false;
    try
    {
        using var helloTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        helloTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var hello = await Receive(socket, helloTimeout.Token) ?? throw new ProtocolException("handshake_required");
        role = StringProperty(hello.RootElement, "client");
        var expected = role switch { "minecraft" => "minecraft_connect", "7dtd" => "7dtd_connect", _ => throw new ProtocolException("unknown_client") };
        if (StringProperty(hello.RootElement, "type") != expected) throw new ProtocolException("invalid_handshake");
        var otherRole = role == "minecraft" ? "7dtd" : "minecraft";
        // Finish welcome before another connection can announce this peer.
        await registrationGate.WaitAsync(ct);
        try
        {
            if (!peers.TryAdd(role, peer)) throw new ProtocolException("duplicate_client");
            registered = true;
            // Receiver reconnect is a controlled fresh snapshot boundary for the v2 sender.
            nativeEntities.Clear();
            healthComponents.Clear(); equipmentComponents.Clear();
            presentation.Clear();
            inspector.Clear();
            await peer.Send(new { type = "welcome", client = role, message = "Bridge connected" }, ct);
            app.Logger.LogInformation("{Message}", role == "minecraft" ? "Minecraft connected" : "7DTD connected");
            if (peers.TryGetValue(otherRole, out var other))
            {
                await peer.Send(new { type = "peer_connected", client = otherRole }, ct);
                await other.Send(new { type = "peer_connected", client = role }, ct);
            }
        }
        finally { registrationGate.Release(); }
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var message = await Receive(socket, ct);
            if (message is null) break;
            var type = StringProperty(message.RootElement, "type");
            if (type == "entity_components")
            {
                await registrationGate.WaitAsync(ct);
                try
                {
                    if (!peers.TryGetValue("minecraft", out var componentTarget))
                        await peer.Send(new { type = "error", code = "peer_unavailable" }, ct);
                    else
                    {
                        try
                        {
                            var transition = equipmentComponents.Apply(message.RootElement, role, nativeEntities, componentPermissions, equipmentPermissions);
                            if (transition.Forward)
                            {
                                await componentTarget.Send(message.RootElement, ct);
                                app.Logger.LogInformation("Components: {Transition}, id={Id}, revision={Revision}; 7dtd -> minecraft",
                                    transition.Code, StringProperty(message.RootElement, "entity_id"), message.RootElement.GetProperty("revision"));
                            }
                        }
                        catch (InvalidDataException ex)
                        { app.Logger.LogWarning("Health component rejected: {Code}", ex.Message); await peer.Send(new { type = "error", code = ex.Message }, ct); }
                    }
                }
                finally { registrationGate.Release(); }
                continue;
            }
            if (type == "entity_state")
            {
                System.Text.Json.Nodes.JsonObject entity;
                var isV2 = message.RootElement.TryGetProperty("version", out var wireVersion) && wireVersion.ValueKind == JsonValueKind.Number && wireVersion.TryGetInt32(out var version) && version == 2;
                try { entity = PresentationComponent.Prepare(message.RootElement, role, mapper); }
                catch (InvalidDataException ex) { throw new ProtocolException(ex.Message); }
                // Serialize lifecycle application/send with peer registration and disconnect cleanup.
                await registrationGate.WaitAsync(ct);
                try
                {
                    if (peers.TryGetValue(isV2 ? "minecraft" : "7dtd", out var entityTarget))
                    {
                        entity = presentation.Resolve(entity);
                        var transition = isV2 ? nativeEntities.Apply(entity) : entities.Apply(entity);
                        app.Logger.LogInformation("Entity registry: {Transition}, id={Id}, count={Count}",
                            transition.Code, entity["entity_id"]!.GetValue<string>(), isV2 ? nativeEntities.Count : entities.Count);
                        if (transition.Forward)
                        {
                            presentation.Commit(entity);
                            inspector.ObserveAccepted(entity);
                            if (entity["components"] != null) app.Logger.LogInformation("[Presentation] entity={Id} state={State}", entity["entity_id"], entity["components"]);
                            if (isV2 && entity["lifecycle"]!["event"]!.GetValue<string>() == "despawn") { healthComponents.Retire(entity); equipmentComponents.Retire(entity); }
                            await entityTarget.Send(entity, ct);
                            app.Logger.LogInformation("Entity state forwarded: {Source} -> {Target} ({Event}, {Id})", role, isV2 ? "minecraft" : "7dtd",
                                entity["lifecycle"]!["event"]!.GetValue<string>(), entity["entity_id"]!.GetValue<string>());
                        }
                        else if (transition.Code is not ("duplicate_spawn" or "unknown_despawn"))
                            await peer.Send(new { type = "error", code = transition.Code }, ct);
                    }
                    else await peer.Send(new { type = "error", code = "peer_unavailable" }, ct);
                }
                finally { registrationGate.Release(); }
                continue;
            }
            if (type == "player_position")
            {
                if (role != "minecraft" || StringProperty(message.RootElement, "source") != "minecraft")
                    throw new ProtocolException("invalid_position_source");
                var x = Coordinate(message.RootElement, "x");
                var y = Coordinate(message.RootElement, "y");
                var z = Coordinate(message.RootElement, "z");
                if (!mapper.TryMap(x, y, z, out var mapped))
                    throw new ProtocolException("coordinate_mapping_overflow");
                (x, y, z) = (mapped.X, mapped.Y, mapped.Z);
                if (peers.TryGetValue("7dtd", out var positionTarget))
                {
                    await positionTarget.Send(new { type = "player_position", source = "minecraft", x, y, z }, ct);
                    app.Logger.LogInformation("Player position forwarded: minecraft -> 7dtd ({X}, {Y}, {Z})", x, y, z);
                }
                else await peer.Send(new { type = "error", code = "peer_unavailable" }, ct);
                continue;
            }
            if (type != "test") throw new ProtocolException("unsupported_type");
            var text = StringProperty(message.RootElement, "text");
            if (text.Length is < 1 or > 256) throw new ProtocolException("invalid_test_text");
            if (peers.TryGetValue(otherRole, out var target))
            {
                await target.Send(new { type = "test", from = role, text }, ct);
                app.Logger.LogInformation("Test forwarded: {Source} -> {Target}", role, otherRole);
            }
            else await peer.Send(new { type = "error", code = "peer_unavailable" }, ct);
        }
    }
    catch (Exception ex) when (ex is ProtocolException or JsonException)
    {
        var code = ex is ProtocolException ? ex.Message : "invalid_json";
        app.Logger.LogWarning("Protocol rejected: {Code}", code);
        try { await peer.Send(new { type = "error", code }, ct); } catch (Exception) { }
    }
    catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException)
    { app.Logger.LogInformation("Connection ended ({Role}): {Reason}", role ?? "unregistered", ex.GetType().Name); }
    finally
    {
        if (registered)
        {
            await registrationGate.WaitAsync();
            try
            {
                peers.TryRemove(role!, out _);
                var removed = entities.Clear();
                if (role == "7dtd") nativeEntities.Clear();
                healthComponents.Clear(); equipmentComponents.Clear();
                presentation.Clear();
                inspector.Clear();
                app.Logger.LogInformation("Entity registry reset: disconnected {Role}, removed={Count}; fresh spawn required", role, removed);
                app.Logger.LogInformation("Disconnected: {Role}", role);
            }
            finally { registrationGate.Release(); }
        }
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)); await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "session ended", timeout.Token); }
            catch (Exception) { socket.Abort(); }
        }
    }
});
app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation("Bridge connected — listening ws://localhost:{Port}/ws; config {Config}", port, configPath));
app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation(
    "Coordinate mapping loaded: scale={Scale}, offsets=({X}, {Y}, {Z}); config {Config}",
    mapper.Scale, mapper.OffsetX, mapper.OffsetY, mapper.OffsetZ, coordinatePath));
await app.RunAsync();

static string StringProperty(JsonElement value, string key)
{
    if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.String)
        throw new ProtocolException("missing_or_invalid_" + key);
    return p.GetString()!;
}

static double Coordinate(JsonElement value, string key)
{
    if (!value.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.Number ||
        !p.TryGetDouble(out var number) || !double.IsFinite(number))
        throw new ProtocolException("invalid_coordinate_" + key);
    return number;
}

static async Task<JsonDocument?> Receive(WebSocket socket, CancellationToken ct)
{
    var buffer = new byte[2048];
    using var data = new MemoryStream();
    while (true)
    {
        var frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        if (frame.MessageType == WebSocketMessageType.Close) return null;
        if (frame.MessageType != WebSocketMessageType.Text) throw new ProtocolException("text_only");
        if (data.Length + frame.Count > 8192) throw new ProtocolException("message_too_large");
        data.Write(buffer, 0, frame.Count);
        if (frame.EndOfMessage) break;
    }
    return JsonDocument.Parse(data.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
}

sealed class ProtocolException(string message) : Exception(message);
sealed class Peer(WebSocket socket)
{
    readonly SemaphoreSlim sendLock = new(1, 1);
    public async Task Send(object message, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await sendLock.WaitAsync(timeout.Token);
        try { await socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(message)), WebSocketMessageType.Text, true, timeout.Token); }
        finally { sendLock.Release(); }
    }
}
