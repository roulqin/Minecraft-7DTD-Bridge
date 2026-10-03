using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var configPath = Path.GetFullPath(args.Length == 1 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../config/network.json"));
using var config = JsonDocument.Parse(File.ReadAllText(configPath));
var host = config.RootElement.GetProperty("host").GetString();
var port = config.RootElement.GetProperty("port").GetInt32();
if (host != "localhost" || port < 1024 || port > 65535)
    throw new InvalidDataException("network.json requires host=localhost and port=1024..65535");
var builder = WebApplication.CreateBuilder();
builder.WebHost.ConfigureKestrel(o => o.ListenLocalhost(port));
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
var app = builder.Build();
var peers = new ConcurrentDictionary<string, Peer>();
var registrationGate = new SemaphoreSlim(1, 1);
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
            if (StringProperty(message.RootElement, "type") != "test") throw new ProtocolException("unsupported_type");
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
        if (registered) { peers.TryRemove(role!, out _); app.Logger.LogInformation("Disconnected: {Role}", role); }
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)); await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "session ended", timeout.Token); }
            catch (Exception) { socket.Abort(); }
        }
    }
});
app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation("Bridge connected — listening ws://localhost:{Port}/ws; config {Config}", port, configPath));
await app.RunAsync();

static string StringProperty(JsonElement value, string key)
{
    if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.String)
        throw new ProtocolException("missing_or_invalid_" + key);
    return p.GetString()!;
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
