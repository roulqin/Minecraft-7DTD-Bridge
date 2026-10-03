using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MC7DTD;
using MC7DTD.Bridge;

var root = Path.GetFullPath(args[0]);
var output = Path.Combine(root, "work/phase3_3-test"); Directory.CreateDirectory(output);
var passed = new List<string>(); string failure = null; Process server = null;
var serverLog = new ConcurrentQueue<string>(); var logs = new ConcurrentQueue<string>();
void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed.Add(name); Console.WriteLine("PASS " + name); }
JsonObject Entity(string action, double x = 10, string id = "6b6d9c36-909c-4fbb-a537-d309f48f8231") => new() {
    ["type"]="entity_state",["version"]=1,["source"]="minecraft",["stream_id"]="55c44b7e-82c8-461d-8c2b-d4e4e4e77fd1",
    ["entity_id"]=id,["entity_type"]="minecraft:marker",["world_id"]="marker-test",["dimension"]="minecraft:overworld",["sequence"]=1,
    ["lifecycle"]=action=="despawn" ? new JsonObject{["event"]=action,["reason"]="out_of_scope"} : new JsonObject{["event"]=action},
    ["position"]=action=="despawn"?null:new JsonObject{["x"]=x,["y"]=64,["z"]=20,["space"]="minecraft"},
    ["rotation"]=action=="despawn"?null:new JsonObject{["yaw"]=0,["pitch"]=0,["roll"]=0},["metadata"]=new JsonObject()
};
WireMessage Wire(JsonObject node) {
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
    return (WireMessage)new DataContractJsonSerializer(typeof(WireMessage)).ReadObject(stream);
}
WireMessage Mapped(string action, double x = 10, string id = "6b6d9c36-909c-4fbb-a537-d309f48f8231") {
    using var json=JsonDocument.Parse(Entity(action,x,id).ToJsonString());
    return Wire(EntityTransport.Prepare(json.RootElement,"minecraft",new CoordinateMapper(2,100,-10,25)));
}
try {
    var scene = new FakeScene(); var controller = new MarkerController(scene, logs.Enqueue);
    var spawn = Mapped("spawn");
    Task.Run(()=>controller.Enqueue(spawn)).GetAwaiter().GetResult();
    Check(scene.Created==0,"socket worker does not create Unity objects");
    spawn.Position.X=999; // Queue must own its snapshot.
    controller.Tick(true);
    Check(controller.Count==1 && scene.Created==1,"spawn creates one object on tick");
    Check(scene.Last.X==120 && scene.Last.Y==118 && scene.Last.Z==65,"non-default Bridge mapping consumed exactly once; snapshot isolated");
    controller.Enqueue(Mapped("spawn")); controller.Tick(true);
    Check(scene.Created==1,"duplicate spawn does not create another object");
    controller.Enqueue(Mapped("update",11)); controller.Tick(true);
    Check(scene.Last.X==122 && controller.Count==1,"update moves the existing object");
    var mismatch=Mapped("update",12); mismatch.StreamId=Guid.NewGuid().ToString(); controller.Enqueue(mismatch); controller.Tick(true);
    Check(scene.Last.X==122,"stream mismatch cannot move object");
    controller.Enqueue(Mapped("update",13,Guid.NewGuid().ToString())); controller.Tick(true);
    Check(controller.Count==1 && scene.Created==1,"unknown update cannot create object");
    var unsupported=Mapped("spawn"); unsupported.EntityType="minecraft:player"; controller.Enqueue(unsupported); controller.Tick(true);
    Check(controller.Count==1,"player type never creates a marker");
    var wrongSpace=Mapped("update"); wrongSpace.Position.Space="minecraft"; controller.Enqueue(wrongSpace); controller.Tick(true);
    Check(scene.Last.X==122,"unmapped source coordinates rejected");
    foreach(var value in new[]{double.NaN,double.PositiveInfinity,1000001d}) {
        var invalid=Mapped("update"); invalid.Position.X=value; controller.Enqueue(invalid); controller.Tick(true);
    }
    Check(scene.Last.X==122,"nonfinite and unsafe float/world coordinates rejected");
    controller.Enqueue(Mapped("despawn")); controller.Tick(true);
    Check(controller.Count==0 && scene.Deleted==1,"despawn deletes object");
    controller.Enqueue(Mapped("despawn")); controller.Tick(true);
    Check(scene.Deleted==1,"repeated despawn is harmless");
    controller.Enqueue(Mapped("spawn")); controller.Tick(false);
    Check(controller.Count==0 && scene.Created==1,"main menu cannot create scene objects");
    controller.Enqueue(Mapped("spawn")); controller.Tick(true); controller.Reset(); controller.Tick(true);
    Check(controller.Count==0 && scene.Deleted==2,"connection reset clears active objects");
    controller.Enqueue(Mapped("spawn")); controller.Tick(true); controller.Tick(false);
    Check(controller.Count==0 && scene.Deleted==3,"world exit clears active objects");
    for(int i=0;i<=MarkerController.Capacity;i++) controller.Enqueue(Mapped("spawn",10,Guid.NewGuid().ToString()));
    controller.Tick(true);
    Check(controller.Count==0,"queue overflow is bounded and resets safely");
    for(int i=0;i<MarkerController.Capacity;i++) controller.Enqueue(Mapped("spawn",10,Guid.NewGuid().ToString()));
    controller.Tick(true); controller.Enqueue(Mapped("spawn",10,Guid.NewGuid().ToString())); controller.Tick(true);
    Check(controller.Count==MarkerController.Capacity,"object capacity is bounded"); controller.Reset(); controller.Tick(true);
    var crossThread=Task.Run(()=>{try{controller.Tick(true);return false;}catch(InvalidOperationException){return true;}}).GetAwaiter().GetResult();
    Check(crossThread,"wrong-thread tick cannot access scene");

    // Real socket chain on a separate, disposable port; never takes the game's role.
    var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var config=Path.Combine(output,"network.json"); File.WriteAllText(config,JsonSerializer.Serialize(new{host="localhost",port}));
    File.WriteAllText(Path.Combine(output,"coordinate.json"),"{\"scale\":2,\"offsetX\":100,\"offsetY\":-10,\"offsetZ\":25}");
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    start.ArgumentList.Add(Path.Combine(root,"bridge-server/bin/Release/net10.0/BridgeServer.dll")); start.ArgumentList.Add(config);
    server=new Process{StartInfo=start}; server.OutputDataReceived+=(_,e)=>{if(e.Data!=null)serverLog.Enqueue(e.Data);}; server.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)serverLog.Enqueue(e.Data);};
    server.Start(); server.BeginOutputReadLine(); server.BeginErrorReadLine();
    async Task Wait(Func<bool> condition,string name) { var watch=Stopwatch.StartNew(); while(!condition()){if(watch.ElapsedMilliseconds>12000)throw new Exception("Timeout: "+name);await Task.Delay(40);} }
    await Wait(()=>serverLog.Any(s=>s.Contains("Now listening")),"server ready");
    var socketScene=new FakeScene(); var socketController=new MarkerController(socketScene,logs.Enqueue);
    using var gameThread=new SceneThread(); int resets=0;
    using var receiver=new BridgeClient(config,logs.Enqueue,socketController.Enqueue,()=>{socketController.Reset();Interlocked.Increment(ref resets);}); receiver.Start();
    await Wait(()=>logs.Any(s=>s=="7DTD connected"),"receiver");
    using var mc=new ClientWebSocket(); mc.Options.Proxy=null; await mc.ConnectAsync(new Uri($"ws://localhost:{port}/ws"),CancellationToken.None);
    async Task Send(JsonObject node) { var data=Encoding.UTF8.GetBytes(node.ToJsonString()); await mc.SendAsync(data,WebSocketMessageType.Text,true,CancellationToken.None); }
    await Send(new JsonObject{["type"]="minecraft_connect",["client"]="minecraft"});
    foreach(var action in new[]{"spawn","update","despawn"}) await Send(Entity(action,action=="update"?11:10));
    await Send(new JsonObject{["type"]="player_position",["source"]="minecraft",["x"]=10,["y"]=64,["z"]=20});
    await Send(new JsonObject{["type"]="test",["text"]="marker regression"});
    await Wait(()=>logs.Any(s=>s.Contains("Test received from minecraft: marker regression")),"messages received");
    gameThread.Run(()=>socketController.Tick(true));
    Check(socketScene.Created==1 && socketScene.Deleted==1 && socketController.Count==0,"WebSocket -> mapping -> registry -> actual receiver callback -> object lifecycle");
    Check(socketScene.Positions.Any(p=>p.X==122 && p.Y==118 && p.Z==65),"WebSocket update uses mapped position");
    Check(logs.Any(s=>s=="Minecraft player: x=120 y=118 z=65"),"existing player_position unchanged");
    Check(logs.Any(s=>s.Contains("Test received from minecraft")),"existing test messages unchanged");
    var prior=logs.Count(s=>s.Contains("Entity state: event=spawn"));
    await Send(Entity("spawn"));
    await Wait(()=>logs.Count(s=>s.Contains("Entity state: event=spawn"))>prior,"second spawn received");
    gameThread.Run(()=>socketController.Tick(true)); Check(socketController.Count==1,"active marker before transport loss");
    var resetBaseline=Volatile.Read(ref resets);
    server.Kill(true); server.WaitForExit();
    await Wait(()=>logs.Any(s=>s.Contains("Bridge disconnected") || s.Contains("Bridge unavailable")),"transport loss");
    await Wait(()=>Volatile.Read(ref resets)>resetBaseline,"disconnect reset callback"); gameThread.Run(()=>socketController.Tick(true));
    Check(socketController.Count==0,"real receiver disconnect callback clears marker");
} catch(Exception ex){ failure=ex.ToString(); Console.Error.WriteLine(failure); }
finally {
    if(server!=null&&!server.HasExited){server.Kill(true);server.WaitForExit();}
    File.WriteAllLines(Path.Combine(output,"server.log"),serverLog); File.WriteAllLines(Path.Combine(output,"receiver.log"),logs);
    File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed,failure,gameRuntimeTested=false},new JsonSerializerOptions{WriteIndented=true}));
}
return failure==null?0:1;

sealed class FakeScene : IMarkerScene {
    public int Created,Deleted; public (double X,double Y,double Z) Last;
    public readonly List<(double X,double Y,double Z)> Positions=new();
    public object Create(string name,double x,double y,double z){Created++;Move(new object(),x,y,z);return new object();}
    public void Move(object handle,double x,double y,double z){Last=(x,y,z);Positions.Add(Last);}
    public void Delete(object handle){Deleted++;}
}
sealed class SceneThread : IDisposable {
    readonly BlockingCollection<Action> work=new(); readonly Thread thread;
    public SceneThread(){thread=new Thread(()=>{foreach(var action in work.GetConsumingEnumerable())action();});thread.Start();}
    public void Run(Action action){Exception error=null;using var done=new ManualResetEventSlim();work.Add(()=>{try{action();}catch(Exception ex){error=ex;}finally{done.Set();}});done.Wait();if(error!=null)throw error;}
    public void Dispose(){work.CompleteAdding();thread.Join();work.Dispose();}
}
