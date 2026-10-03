using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using MC7DTD.Bridge;

var root = Path.GetFullPath(args[0]);
var output = Path.Combine(root, "work", "phase2_1-test");
Directory.CreateDirectory(output);
var passed = new List<string>();
string? failure = null;
void Check(bool ok, string name) {
    if (!ok) throw new Exception(name);
    passed.Add(name); Console.WriteLine("PASS " + name);
}
async Task Send(ClientWebSocket socket, object data) {
    using var timeout = new CancellationTokenSource(5000);
    await socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(data)), WebSocketMessageType.Text, true, timeout.Token);
}
async Task<JsonElement> Receive(ClientWebSocket socket) {
    using var timeout = new CancellationTokenSource(5000);
    using var stream = new MemoryStream(); var buffer = new byte[2048];
    WebSocketReceiveResult frame;
    do {
        frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        if (frame.MessageType != WebSocketMessageType.Text) throw new Exception("Expected text frame");
        stream.Write(buffer, 0, frame.Count);
    } while (!frame.EndOfMessage);
    using var document = JsonDocument.Parse(stream.ToArray()); return document.RootElement.Clone();
}
try {
    var identity = CoordinateMapper.Load(Path.Combine(root, "config", "coordinate.json"));
    Check(identity.TryMap(-2.5, 64, 0, out var initial) && initial == new MappedPosition(-2.5, 64, 0), "checked-in default mapping preserves position");
    Check(!new CoordinateMapper(2, 0, 0, 0).TryMap(double.MaxValue, 1, 1, out _), "mapping arithmetic overflow rejected");
    Check(!identity.TryMap(double.NaN, 1, 1, out _), "non-finite input rejected");
    foreach (var bad in new[] {
        "{}", "[]", "{\"scale\":\"1\",\"offsetX\":0,\"offsetY\":0,\"offsetZ\":0}",
        "{\"scale\":1e309,\"offsetX\":0,\"offsetY\":0,\"offsetZ\":0}",
        "{\"scale\":1,\"offsetX\":null,\"offsetY\":0,\"offsetZ\":0}", "{" }) {
        var invalidPath = Path.Combine(output, "invalid-coordinate.json"); File.WriteAllText(invalidPath, bad);
        var rejected = false;
        try { CoordinateMapper.Load(invalidPath); }
        catch (Exception ex) when (ex is InvalidDataException or JsonException) { rejected = true; }
        Check(rejected, "invalid mapping config rejected: " + bad);
    }
    foreach (var scenario in new[] {
        (Name:"default", Scale:1d, X:0d, Y:0d, Z:0d, Expected:new MappedPosition(-2.5,64,8)),
        (Name:"offset", Scale:1d, X:100d, Y:-10d, Z:0.25d, Expected:new MappedPosition(97.5,54,8.25)),
        (Name:"scale", Scale:2d, X:0d, Y:0d, Z:0d, Expected:new MappedPosition(-5,128,16)),
        (Name:"combined", Scale:0.5d, X:10d, Y:-20d, Z:30d, Expected:new MappedPosition(8.75,12,34)) }) {
        var directory = Path.Combine(output, scenario.Name); Directory.CreateDirectory(directory);
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var config = Path.Combine(directory, "network.json");
        File.WriteAllText(config, JsonSerializer.Serialize(new { host = "localhost", port }));
        File.WriteAllText(Path.Combine(directory, "coordinate.json"), JsonSerializer.Serialize(new { scale = scenario.Scale, offsetX = scenario.X, offsetY = scenario.Y, offsetZ = scenario.Z }));
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(root, "bridge-server/bin/Release/net10.0/BridgeServer.dll")); info.ArgumentList.Add(config);
        using var process = new Process { StartInfo = info };
        var logs = new ConcurrentQueue<string>();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) logs.Enqueue(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) logs.Enqueue(e.Data); };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        try {
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(1) };
            var ready = false;
            for (var attempt = 0; attempt < 100; attempt++) {
                if (process.HasExited) throw new Exception("Bridge failed to start");
                try { ready = (await http.GetAsync($"http://localhost:{port}/health")).IsSuccessStatusCode; }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
                if (ready) break; await Task.Delay(100);
            }
            if (!ready) throw new Exception("Bridge startup timeout");
            using var mc = new ClientWebSocket(); using var td = new ClientWebSocket();
            mc.Options.Proxy = null; td.Options.Proxy = null;
            using var timeout = new CancellationTokenSource(5000);
            var uri = new Uri($"ws://localhost:{port}/ws");
            await mc.ConnectAsync(uri, timeout.Token);
            await Send(mc, new { type = "minecraft_connect", client = "minecraft" });
            Check((await Receive(mc)).GetProperty("type").GetString() == "welcome", scenario.Name + " Minecraft welcome unchanged");
            await td.ConnectAsync(uri, timeout.Token);
            await Send(td, new { type = "7dtd_connect", client = "7dtd" });
            Check((await Receive(td)).GetProperty("type").GetString() == "welcome", scenario.Name + " 7DTD welcome unchanged");
            await Receive(mc); await Receive(td);
            await Send(mc, new { type = "player_position", source = "minecraft", x = -2.5, y = 64, z = 8 });
            var position = await Receive(td);
            Check(position.GetProperty("type").GetString() == "player_position" && position.GetProperty("source").GetString() == "minecraft" && position.EnumerateObject().Count() == 5,
                scenario.Name + " position schema unchanged");
            Check(new MappedPosition(position.GetProperty("x").GetDouble(), position.GetProperty("y").GetDouble(), position.GetProperty("z").GetDouble()) == scenario.Expected,
                scenario.Name + " mapped coordinates reach WebSocket receiver");
            await Send(mc, new { type = "test", text = "from Minecraft" });
            var test = await Receive(td);
            Check(test.GetProperty("from").GetString() == "minecraft" && test.GetProperty("text").GetString() == "from Minecraft", scenario.Name + " Minecraft test unchanged");
            await Send(td, new { type = "test", text = "from 7DTD" });
            test = await Receive(mc);
            Check(test.GetProperty("from").GetString() == "7dtd" && test.GetProperty("text").GetString() == "from 7DTD", scenario.Name + " 7DTD test unchanged");
            if (scenario.Name == "scale") {
                await Send(mc, new { type = "player_position", source = "minecraft", x = double.MaxValue, y = 1, z = 1 });
                Check((await Receive(mc)).GetProperty("code").GetString() == "coordinate_mapping_overflow", "overflow returns protocol error instead of invalid JSON");
            }
        } finally {
            if (!process.HasExited) process.Kill(true); process.WaitForExit();
            File.WriteAllLines(Path.Combine(directory, "bridge.log"), logs);
        }
    }
} catch (Exception ex) { failure = ex.ToString(); Console.Error.WriteLine(failure); }
File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { passed, failure, gameRuntimeTested = false }, new JsonSerializerOptions { WriteIndented = true }));
return failure is null ? 0 : 1;
