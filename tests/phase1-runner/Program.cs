using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var root = Path.GetFullPath(args[0]);
var output = Path.Combine(root, "work", "phase1-test"); Directory.CreateDirectory(output);
var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
var config = Path.Combine(output, "network.json"); File.WriteAllText(config, JsonSerializer.Serialize(new { host = "localhost", port }));
var uri = new Uri($"ws://localhost:{port}/ws");
using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
var processes = new List<Child>();
var passed = new List<string>();
void Pass(string name) { passed.Add(name); Console.WriteLine("PASS " + name); }
Child Start(string file, params string[] arguments) {
    var child = new Child(file, arguments, root); processes.Add(child); return child;
}
Child Server() => Start("dotnet", Path.Combine(root, "bridge-server/bin/Release/net10.0/BridgeServer.dll"), config);
async Task Wait(Func<Task<bool>> condition, string name, int seconds = 25) {
    var deadline = DateTime.UtcNow.AddSeconds(seconds);
    while (DateTime.UtcNow < deadline) { try { if (await condition()) return; } catch (HttpRequestException) { } catch (TaskCanceledException) { } await Task.Delay(200); }
    throw new Exception("Timeout: " + name);
}
async Task Ready() => await Wait(async () => (await http.GetAsync($"http://localhost:{port}/health")).IsSuccessStatusCode, "server ready");
async Task<ClientWebSocket> Connect() { var ws = new ClientWebSocket(); ws.Options.Proxy = null; using var timeout = new CancellationTokenSource(5000); await ws.ConnectAsync(uri, timeout.Token); return ws; }
async Task Send(ClientWebSocket ws, string data, WebSocketMessageType type = WebSocketMessageType.Text, bool last = true) {
    using var timeout = new CancellationTokenSource(5000); await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data)), type, last, timeout.Token);
}
async Task<JsonElement> Receive(ClientWebSocket ws) {
    using var timeout = new CancellationTokenSource(5000); using var ms = new MemoryStream(); var bytes = new byte[2048]; WebSocketReceiveResult frame;
    do { frame = await ws.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token); if(frame.MessageType == WebSocketMessageType.Close) throw new Exception("Unexpected close"); ms.Write(bytes, 0, frame.Count); } while(!frame.EndOfMessage);
    using var doc = JsonDocument.Parse(ms.ToArray()); return doc.RootElement.Clone();
}
void Equal(string? actual, string expected) { if(actual != expected) throw new Exception($"Expected {expected}, got {actual}"); }
async Task Hello(ClientWebSocket ws, string role) {
    await Send(ws, JsonSerializer.Serialize(new { type = role == "minecraft" ? "minecraft_connect" : "7dtd_connect", client = role }));
    Equal((await Receive(ws)).GetProperty("type").GetString(), "welcome");
}
async Task Reject(string payload, string code, WebSocketMessageType type = WebSocketMessageType.Text) {
    using var ws = await Connect(); await Send(ws, payload, type); var result = await Receive(ws); Equal(result.GetProperty("type").GetString(), "error"); Equal(result.GetProperty("code").GetString(), code); Pass("reject " + code);
}
string? failure = null;
try {
    var server = Server(); await Ready();
    var classpath = File.ReadAllText(Path.Combine(root, "minecraft-mod/build/test-classpath.txt")).Trim();
    var java = Start("java", "-cp", classpath, "io.mc7dtd.ClientHarness", config, "120");
    var csharp = Start(Path.Combine(root, "tests/client-harness/bin/Release/net48/ClientHarness.exe"), config, "120");
    await Wait(() => Task.FromResult(java.Count("Test received from 7dtd") >= 1 && csharp.Count("Test received from minecraft") >= 1), "actual client bidirectional messages");
    Pass("Java and .NET Framework client source: welcome and bidirectional tests");
    server.Stop(); await Task.Delay(1000); server = Server(); await Ready();
    await Wait(() => Task.FromResult(java.Count("Test received from 7dtd") >= 2 && csharp.Count("Test received from minecraft") >= 2), "reconnect after server restart");
    Pass("both clients reconnect and exchange tests after server restart");
    java.Stop(); csharp.Stop();
    await Wait(async () => { using var doc=JsonDocument.Parse(await http.GetStringAsync($"http://localhost:{port}/health"));return doc.RootElement.GetProperty("clients").GetArrayLength()==0; }, "cleanup disconnected roles");
    await Reject("{", "invalid_json");
    await Reject("[]", "missing_or_invalid_client");
    await Reject("{\"type\":\"hello\",\"client\":\"unknown\"}", "unknown_client");
    await Reject("{\"type\":\"7dtd_connect\",\"client\":\"minecraft\"}", "invalid_handshake");
    await Reject("{}", "text_only", WebSocketMessageType.Binary);
    await Reject(new string('a', 8193), "message_too_large");
    using(var mc = await Connect()) {
        await Hello(mc, "minecraft");
        await Reject("{\"type\":\"minecraft_connect\",\"client\":\"minecraft\"}", "duplicate_client");
        await Send(mc, "{\"type\":\"test\",\"text\":\"alone\"}"); Equal((await Receive(mc)).GetProperty("code").GetString(), "peer_unavailable"); Pass("offline peer reported");
        using(var td = await Connect()) {
            await Hello(td, "7dtd"); await Receive(mc); await Receive(td);
            await Send(mc, "{\"type\":\"test\",\"text\":\"你好", last:false);
            await Send(mc, "七日杀\",\"from\":\"forged\"}");
            var received = await Receive(td); Equal(received.GetProperty("text").GetString(), "你好七日杀"); Equal(received.GetProperty("from").GetString(), "minecraft"); Pass("fragmented UTF-8 JSON and server-owned sender identity");
            await Send(td, "{\"type\":\"player_position\",\"x\":1}"); Equal((await Receive(td)).GetProperty("code").GetString(), "unsupported_type"); Pass("Phase 2 message rejected");
        }
        await Send(mc, "{\"type\":\"test\",\"text\":\"" + new string('a',257) + "\"}"); Equal((await Receive(mc)).GetProperty("code").GetString(), "invalid_test_text"); Pass("test text length limit");
    }
    using(var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{port}/ws")) { request.Headers.Add("Origin", "https://example.com"); Equal(((int)(await http.SendAsync(request)).StatusCode).ToString(), "400"); Pass("browser origin rejected"); }
} catch(Exception ex) { failure=ex.ToString(); Console.Error.WriteLine(failure); }
finally {
    foreach(var child in processes) child.Stop();
    for(int i=0;i<processes.Count;i++) File.WriteAllLines(Path.Combine(output,$"process-{i}.log"),processes[i].Lines);
    File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new { passed, failure, gameRuntimeTested=false },new JsonSerializerOptions{WriteIndented=true}));
}
return failure is null ? 0 : 1;

sealed class Child {
    readonly Process process;
    public readonly ConcurrentQueue<string> Lines = new();
    public Child(string file, string[] arguments, string root) {
        var info=new ProcessStartInfo(file) { WorkingDirectory=root, UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        foreach(var argument in arguments) info.ArgumentList.Add(argument);
        process=new Process { StartInfo=info };
        process.OutputDataReceived+=(_,e)=>{if(e.Data!=null) Lines.Enqueue(e.Data);};
        process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null) Lines.Enqueue(e.Data);};
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
    }
    public int Count(string text)=>Lines.Count(line=>line.Contains(text,StringComparison.Ordinal));
    public void Stop() { if(!process.HasExited) process.Kill(true); process.WaitForExit(); }
}
