using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using MC7DTD.Bridge;

var root = Path.GetFullPath(args[0]);
var lifecycleTests = args.Length > 1 && args[1] == "phase3_2";
var output = Path.Combine(root, "work", lifecycleTests ? "phase3_2-test" : "phase3_1-test"); Directory.CreateDirectory(output);
var passed = new List<string>(); var children = new List<Child>(); string? failure = null;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed.Add(name); Console.WriteLine("PASS " + name); }
JsonObject Entity(string action, int seq = 1) => new() {
    ["type"]="entity_state", ["version"]=1, ["source"]="minecraft",
    ["stream_id"]="55c44b7e-82c8-461d-8c2b-d4e4e4e77fd1", ["entity_id"]="6b6d9c36-909c-4fbb-a537-d309f48f8231",
    ["entity_type"]="minecraft:player", ["world_id"]="demo-world-01", ["dimension"]="minecraft:overworld", ["sequence"]=seq,
    ["lifecycle"]=action is "despawn" or "remove" ? new JsonObject { ["event"]=action,["reason"]="world_unloaded" } : new JsonObject { ["event"]=action },
    ["position"]=action is "despawn" or "remove" ? null : new JsonObject { ["x"]=10,["y"]=64,["z"]=20,["space"]="minecraft" },
    ["rotation"]=action is "despawn" or "remove" ? null : new JsonObject { ["yaw"]=90,["pitch"]=-10,["roll"]=0 },
    ["metadata"]=action is "despawn" or "remove" ? new JsonObject() : new JsonObject { ["health"]=20,["max_health"]=20,["is_alive"]=true,["custom"]="你好" }
};
JsonObject Prepare(JsonObject node, CoordinateMapper? mapper = null, string role="minecraft") {
    using var doc=JsonDocument.Parse(node.ToJsonString()); return EntityTransport.Prepare(doc.RootElement,role,mapper ?? new CoordinateMapper(1,0,0,0));
}
void Reject(Action<JsonObject> edit, string code, CoordinateMapper? mapper = null, string role="minecraft") {
    var node=Entity("spawn"); edit(node); var rejected=false;
    try { Prepare(node,mapper,role); } catch(InvalidDataException ex) { rejected=ex.Message==code; }
    Check(rejected,"reject "+code+" #"+passed.Count);
}
async Task Send(ClientWebSocket ws, JsonNode node) {
    using var ct=new CancellationTokenSource(5000); await ws.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(node)),WebSocketMessageType.Text,true,ct.Token);
}
async Task<JsonObject> Receive(ClientWebSocket ws) {
    using var ct=new CancellationTokenSource(5000); using var ms=new MemoryStream(); var bytes=new byte[2048]; WebSocketReceiveResult frame;
    do { frame=await ws.ReceiveAsync(new ArraySegment<byte>(bytes),ct.Token); if(frame.MessageType!=WebSocketMessageType.Text)throw new Exception("Expected text"); ms.Write(bytes,0,frame.Count); }while(!frame.EndOfMessage);
    return JsonNode.Parse(ms.ToArray())!.AsObject();
}
async Task Wait(Func<Task<bool>> condition,string name) {
    for(var i=0;i<150;i++){try{if(await condition())return;}catch(HttpRequestException){}catch(TaskCanceledException){}await Task.Delay(100);}throw new Exception("Timeout "+name);
}
Child Start(string executable, params string[] arguments) { var c=new Child(root,executable,arguments);children.Add(c);return c; }
try {
    if (lifecycleTests)
    {
        var registry = new EntityRegistry();
        var key = new EntityKey("minecraft", "demo-world-01", "minecraft:overworld", "6b6d9c36-909c-4fbb-a537-d309f48f8231");
        Check(registry.Apply(Prepare(Entity("update"))).Code == "entity_not_found" && registry.Count == 0, "registry unknown update cannot create entity");
        Check(registry.Apply(Prepare(Entity("spawn"))).Forward && registry.Count == 1, "registry spawn creates one record");
        var original = registry.Find(key)!;
        Check(original.Type == "minecraft:player" && original.Status == "active" && original.LastUpdate != default, "record exposes type status and last update");
        var duplicate = Entity("spawn", 2); duplicate["position"]!["x"] = 999;
        Check(registry.Apply(Prepare(duplicate)).Code == "duplicate_spawn" && registry.Count == 1 && registry.Find(key)!.State["position"]!["x"]!.GetValue<double>() == 10, "duplicate spawn does not overwrite state");
        var update = Entity("update", 2); update["position"]!["x"] = 11;
        Check(registry.Apply(Prepare(update)).Forward && registry.Count == 1 && registry.Find(key)!.State["position"]!["x"]!.GetValue<double>() == 11, "update changes existing snapshot");
        Check(registry.Find(key)!.LastUpdate >= original.LastUpdate && registry.Find(key)!.Sequence == 2, "accepted update refreshes time and sequence");
        var snapshot = registry.Find(key)!; snapshot.State["position"]!["x"] = 800;
        Check(registry.Find(key)!.State["position"]!["x"]!.GetValue<double>() == 11, "inspection cannot mutate stored record");
        update["stream_id"] = Guid.NewGuid().ToString("D");
        Check(registry.Apply(Prepare(update)).Code == "entity_stream_mismatch", "different stream cannot update existing identity");
        update = Entity("update", 3); update["entity_type"] = "minecraft:zombie";
        Check(registry.Apply(Prepare(update)).Code == "entity_type_mismatch", "entity type immutable during active lifetime");
        Check(registry.Apply(Prepare(Entity("despawn", 3))).Forward && registry.Count == 0 && registry.Find(key) == null, "despawn deletes record");
        Check(registry.Apply(Prepare(Entity("despawn", 4))).Code == "unknown_despawn" && registry.Count == 0, "repeated despawn is harmless");
        Check(registry.Apply(Prepare(Entity("update", 5))).Code == "entity_not_found", "update cannot resurrect removed record");
        Check(registry.Apply(Prepare(Entity("spawn", 6))).Forward && registry.Count == 1, "explicit fresh spawn can recreate removed record");
        var dimension = Entity("spawn"); dimension["dimension"] = "minecraft:the_nether";
        Check(registry.Apply(Prepare(dimension)).Forward && registry.Count == 2, "dimension is part of identity key");
        Check(registry.Clear() == 2 && registry.Count == 0, "disconnect reset removes all records");
        var bounded = new EntityRegistry(1); bounded.Apply(Prepare(Entity("spawn")));
        Check(bounded.Apply(Prepare(dimension)).Code == "entity_registry_full" && bounded.Count == 1, "registry capacity bounds memory");
    }
    Check(Prepare(Entity("spawn"))["position"]!["x"]!.GetValue<double>()==10,"default entity mapping");
    var mapped=Prepare(Entity("update",2),new CoordinateMapper(2,100,-10,25));
    Check(mapped["position"]!["x"]!.GetValue<double>()==120 && mapped["position"]!["y"]!.GetValue<double>()==118 && mapped["position"]!["z"]!.GetValue<double>()==65,"scale plus offsets");
    Check(mapped["rotation"]!.ToJsonString()==Entity("update",2)["rotation"]!.ToJsonString(),"rotation unchanged");
    Check(Prepare(Entity("announce"))["lifecycle"]!["event"]!.GetValue<string>()=="spawn","announce alias -> spawn");
    Check(Prepare(Entity("remove"))["lifecycle"]!["event"]!.GetValue<string>()=="despawn","remove alias -> despawn");
    Reject(n=>n["version"]="1","invalid_entity_version");
    Reject(n=>n["version"]=2,"invalid_entity_version");
    Reject(n=>n["source"]="7dtd","invalid_entity_source");
    Reject(n=>{},"invalid_entity_source",role:"7dtd");
    Reject(n=>n["entity_id"]="123","invalid_entity_id");
    Reject(n=>n["entity_type"]="7dtd:zombie","invalid_entity_type");
    Reject(n=>n["world_id"]="C:\\world","invalid_entity_world");
    Reject(n=>n["dimension"]="bad","invalid_entity_dimension");
    Reject(n=>n["sequence"]=0,"invalid_entity_sequence");
    Reject(n=>n["sequence"]="1","invalid_entity_sequence");
    Reject(n=>n["sequence"]=9007199254740992L,"invalid_entity_sequence");
    Reject(n=>n["extra"]=1,"invalid_entity_fields");
    Reject(n=>n.Remove("metadata"),"invalid_entity_fields");
    Reject(n=>n["lifecycle"]!["event"]="kill","invalid_entity_lifecycle");
    Reject(n=>n["position"]!["space"]="7dtd","invalid_entity_space");
    Reject(n=>n["position"]!["x"]="10","invalid_entity_number");
    Reject(n=>n["rotation"]!["yaw"]=360,"invalid_entity_rotation");
    Reject(n=>n["rotation"]!["pitch"]=91,"invalid_entity_rotation");
    Reject(n=>n["rotation"]!["roll"]=-181,"invalid_entity_rotation");
    Reject(n=>n["metadata"]!["health"]=-1,"invalid_entity_metadata");
    Reject(n=>n["metadata"]!["max_health"]=1,"invalid_entity_metadata");
    Reject(n=>n["metadata"]!["custom"]=new JsonArray(1),"invalid_entity_metadata");
    Reject(n=>n["metadata"]!["custom"]=new string('a',257),"invalid_entity_metadata");
    Reject(n=>n["metadata"]!["is_alive"]="true","invalid_entity_metadata");
    Reject(n=>n["metadata"]!["display_name"]=10,"invalid_entity_metadata");
    Reject(n=>{for(var i=0;i<33;i++)n["metadata"]!["key"+i]=i;},"invalid_entity_metadata");
    Reject(n=>{for(var i=0;i<25;i++)n["metadata"]!["key"+i]=new string('中',100);},"entity_message_too_large");
    Reject(n=>{},"invalid_entity_scale",new CoordinateMapper(0,0,0,0));
    Reject(n=>{},"invalid_entity_scale",new CoordinateMapper(-1,0,0,0));
    Reject(n=>n["position"]!["x"]=double.MaxValue,"coordinate_mapping_overflow",new CoordinateMapper(2,0,0,0));
    var invalidDespawn=Entity("despawn");invalidDespawn["lifecycle"]!["reason"]="bad";
    try{Prepare(invalidDespawn);throw new Exception("Accepted invalid despawn");}catch(InvalidDataException ex){Check(ex.Message=="invalid_entity_reason","invalid despawn reason");}
    invalidDespawn=Entity("despawn");invalidDespawn["metadata"]!["health"]=0;
    try{Prepare(invalidDespawn);throw new Exception("Accepted despawn metadata");}catch(InvalidDataException ex){Check(ex.Message=="invalid_entity_metadata","despawn requires empty metadata");}
    invalidDespawn=Entity("despawn");invalidDespawn["position"]=Entity("spawn")["position"]!.DeepClone();
    try{Prepare(invalidDespawn);throw new Exception("Accepted despawn position");}catch(InvalidDataException ex){Check(ex.Message=="invalid_entity_state","despawn requires null position");}
    using(var duplicate=JsonDocument.Parse(Entity("spawn").ToJsonString().Replace("\"version\":1","\"version\":1,\"version\":1"))) {
        try{EntityTransport.Prepare(duplicate.RootElement,"minecraft",new CoordinateMapper(1,0,0,0));throw new Exception("Accepted duplicate");}
        catch(InvalidDataException ex){Check(ex.Message=="duplicate_entity_field","duplicate keys rejected");}
    }
    var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();var port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
    var config=Path.Combine(output,"network.json");File.WriteAllText(config,JsonSerializer.Serialize(new{host="localhost",port}));
    File.WriteAllText(Path.Combine(output,"coordinate.json"),"{\"scale\":2,\"offsetX\":100,\"offsetY\":-10,\"offsetZ\":25}");
    Child Server()=>Start("dotnet",Path.Combine(root,"bridge-server/bin/Release/net10.0/BridgeServer.dll"),config);
    var server=Server();
    using var http=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(1)};
    async Task Ready()=>await Wait(async()=> (await http.GetAsync($"http://localhost:{port}/health")).IsSuccessStatusCode,"server ready");
    async Task<ClientWebSocket> Connect(string role) {
        var ws=new ClientWebSocket();ws.Options.Proxy=null;using var ct=new CancellationTokenSource(5000);
        await ws.ConnectAsync(new Uri($"ws://localhost:{port}/ws"),ct.Token);
        await Send(ws,new JsonObject{["type"]=role=="minecraft"?"minecraft_connect":"7dtd_connect",["client"]=role});
        Check((await Receive(ws))["type"]!.GetValue<string>()=="welcome",role+" welcome");return ws;
    }
    await Ready();
    using(var mc=await Connect("minecraft")) {
        await Send(mc,Entity("spawn"));Check((await Receive(mc))["code"]!.GetValue<string>()=="peer_unavailable","offline entity peer reported without buffering");
        using var td=await Connect("7dtd");
        await Receive(mc);await Receive(td);
        foreach(var action in new[]{"spawn","update","despawn","announce","remove"}) {
            var node=Entity(action,2);await Send(mc,node);var received=await Receive(td);var expected=Prepare(node,new CoordinateMapper(2,100,-10,25));
            Check(JsonNode.DeepEquals(received,expected),"WebSocket snapshot preserved and mapped: "+action);
        }
        await Send(mc,new JsonObject{["type"]="player_position",["source"]="minecraft",["x"]=10,["y"]=64,["z"]=20});
        var player=await Receive(td);Check(player.Count==5&&player["x"]!.GetValue<double>()==120&&player["y"]!.GetValue<double>()==118&&player["z"]!.GetValue<double>()==65,"legacy player_position preserved");
        foreach(var pair in new[]{(From:mc,To:td,Role:"minecraft"),(From:td,To:mc,Role:"7dtd")}) {
            await Send(pair.From,new JsonObject{["type"]="test",["text"]="after entity"});var test=await Receive(pair.To);
            Check(test["from"]!.GetValue<string>()==pair.Role&&test["text"]!.GetValue<string>()=="after entity","test still works: "+pair.Role);
        }
        var invalid=Entity("spawn");invalid["version"]="1";await Send(mc,invalid);
        Check((await Receive(mc))["code"]!.GetValue<string>()=="invalid_entity_version","invalid entity returns protocol error");
    }
    await Wait(async()=>{using var d=JsonDocument.Parse(await http.GetStringAsync($"http://localhost:{port}/health"));return d.RootElement.GetProperty("clients").GetArrayLength()==0;},"roles cleared");
    if (lifecycleTests)
    {
        async Task ClientCount(int expected) => await Wait(async () => {
            using var doc = JsonDocument.Parse(await http.GetStringAsync($"http://localhost:{port}/health"));
            return doc.RootElement.GetProperty("clients").GetArrayLength() == expected;
        }, "client count " + expected);
        using (var mc = await Connect("minecraft"))
        using (var td = await Connect("7dtd"))
        {
            await Receive(mc); await Receive(td);
            await Send(mc, Entity("spawn")); await Receive(td);
            using (var duplicateClient = new ClientWebSocket())
            {
                duplicateClient.Options.Proxy = null;
                using var timeout = new CancellationTokenSource(5000);
                await duplicateClient.ConnectAsync(new Uri($"ws://localhost:{port}/ws"), timeout.Token);
                await Send(duplicateClient, new JsonObject { ["type"]="minecraft_connect", ["client"]="minecraft" });
                Check((await Receive(duplicateClient))["code"]!.GetValue<string>() == "duplicate_client", "rejected duplicate connection does not own registry cleanup");
            }
            await Send(mc, Entity("spawn", 2));
            await Send(mc, new JsonObject { ["type"]="test", ["text"]="duplicate marker" });
            Check((await Receive(td))["type"]!.GetValue<string>() == "test", "duplicate spawn not forwarded; connection still usable");
            await Send(mc, Entity("update", 2));
            Check((await Receive(td))["lifecycle"]!["event"]!.GetValue<string>() == "update", "registered update forwarded");
            await Send(mc, Entity("despawn", 3)); await Receive(td);
            await Send(mc, Entity("despawn", 4));
            await Send(mc, new JsonObject { ["type"]="test", ["text"]="despawn marker" });
            Check((await Receive(td))["type"]!.GetValue<string>() == "test", "repeated despawn not forwarded");
            await Send(mc, Entity("update", 5));
            Check((await Receive(mc))["code"]!.GetValue<string>() == "entity_not_found", "removed entity update rejected without disconnect");
            await Send(mc, Entity("spawn", 6)); await Receive(td);
            td.Dispose(); await ClientCount(1);
            using var target = await Connect("7dtd"); await Receive(mc); await Receive(target);
            await Send(mc, Entity("update", 7));
            Check((await Receive(mc))["code"]!.GetValue<string>() == "entity_not_found", "target reconnect requires fresh spawn");
            await Send(mc, Entity("spawn", 8));
            Check((await Receive(target))["lifecycle"]!["event"]!.GetValue<string>() == "spawn", "fresh spawn after target reconnect accepted");
            mc.Dispose(); await ClientCount(1);
            using var source = await Connect("minecraft"); await Receive(source); await Receive(target);
            await Send(source, Entity("update", 9));
            Check((await Receive(source))["code"]!.GetValue<string>() == "entity_not_found", "source reconnect cannot update previous session record");
            await Send(source, Entity("spawn", 10));
            Check((await Receive(target))["lifecycle"]!["event"]!.GetValue<string>() == "spawn", "fresh source session spawn accepted");
        }
        await ClientCount(0);
        Check(server.Count("Entity registry reset:") >= 2, "disconnect cleanup recorded in server logs");
    }
    var csharp=Start(Path.Combine(root,"tests/client-harness/bin/Release/net48/ClientHarness.exe"),config,"120");
    await Wait(()=>Task.FromResult(csharp.Count("7DTD connected")>=1),"actual C# receiver");
    using(var mc=await Connect("minecraft")) {
        await Receive(mc);await Receive(mc); // peer_connected and the real C# client's test.
        foreach(var action in new[]{"spawn","update","despawn"})await Send(mc,Entity(action,2));
        await Wait(()=>Task.FromResult(new[]{"spawn","update","despawn"}.All(a=>csharp.Count("Entity state: event="+a)>=1)),"actual receiver lifecycle logs");
        Check(csharp.Count(" x=120 y=118 z=65 yaw=90 pitch=-10 roll=0")>=2,"actual net48 client logs mapped spawn/update");
        Check(csharp.Count("reason=world_unloaded")>=1,"actual net48 client logs despawn without position");
        await Send(mc,new JsonObject{["type"]="player_position",["source"]="minecraft",["x"]=10,["y"]=64,["z"]=20});
        await Wait(()=>Task.FromResult(csharp.Count("Minecraft player: x=120 y=118 z=65")>=1),"actual player log");Check(true,"actual legacy position logger unchanged");
        server.Stop();
    }
    server=Server();await Ready();
    await Wait(()=>Task.FromResult(csharp.Count("7DTD connected")>=2),"C# reconnect");
    using(var mc=await Connect("minecraft")) {
        await Receive(mc);await Receive(mc);
        var node=Entity("spawn");node["stream_id"]=Guid.NewGuid().ToString("D");await Send(mc,node);
        await Wait(()=>Task.FromResult(csharp.Count("Entity state: event=spawn")>=2),"entity after restart");Check(true,"receiver reconnects and logs new-stream snapshot after Bridge restart");
    }
} catch(Exception ex){failure=ex.ToString();Console.Error.WriteLine(failure);}
finally {
    foreach(var c in children)c.Stop();
    for(var i=0;i<children.Count;i++)File.WriteAllLines(Path.Combine(output,$"process-{i}.log"),children[i].Lines);
    File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed,failure,gameRuntimeTested=false},new JsonSerializerOptions{WriteIndented=true}));
}
return failure is null?0:1;

sealed class Child {
    readonly Process process; public readonly ConcurrentQueue<string> Lines=new();
    public Child(string root,string executable,string[] args) {
        var info=new ProcessStartInfo(executable){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in args)info.ArgumentList.Add(a);
        process=new Process{StartInfo=info};process.OutputDataReceived+=(_,e)=>{if(e.Data!=null)Lines.Enqueue(e.Data);};process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)Lines.Enqueue(e.Data);};process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
    }
    public int Count(string text)=>Lines.Count(line=>line.Contains(text,StringComparison.Ordinal));
    public void Stop(){if(!process.HasExited)process.Kill(true);process.WaitForExit();}
}
