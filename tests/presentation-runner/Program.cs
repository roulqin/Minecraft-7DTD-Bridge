using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MC7DTD;
using MC7DTD.Bridge;
using Presentation = MC7DTD.Bridge.PresentationComponent;

var root=Path.GetFullPath(args[0]);var output=Path.Combine(root,"work/phase3_7_3-test");Directory.CreateDirectory(output);
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);Console.WriteLine("PASS "+label);}
JsonObject Fixture(bool v2=true){var e=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"docs/examples/entity_state_v2/7dtd_spawn.json"))).AsObject();e["entity_type"]=v2?"7dtd:player":"minecraft:marker";if(!v2){e.Remove("authority");e.Remove("origin");e["version"]=1;e["source"]="minecraft";e["position"]["space"]="minecraft";}return e;}
JsonObject Delta(JsonObject entity,string action,long sequence,JsonNode p=null,bool attach=false){var e=entity.DeepClone().AsObject();e["sequence"]=sequence;e["lifecycle"]=new JsonObject{["event"]=action};e.Remove("components");if(action=="despawn"){e["lifecycle"]["reason"]="world_unloaded";e["position"]=null;e["rotation"]=null;}if(attach)e["components"]=new JsonObject{["presentation"]=p?.DeepClone()};return e;}
JsonObject Prepare(JsonObject e,string role){using var d=JsonDocument.Parse(e.ToJsonString());return Presentation.Prepare(d.RootElement,role,new CoordinateMapper(2,1,2,3));}
void Reject(JsonObject e,string label,string role="7dtd"){try{Prepare(e,role);}catch(InvalidDataException){Check(true,"rejected "+label);return;}throw new Exception("accepted "+label);}
var catalog=Path.Combine(root,"config/entity_types.json");var store=new Presentation(catalog);var native=new NativeEntityRegistry();var legacy=new EntityRegistry();
EntityTransition Apply(JsonObject e,bool v2=true){var resolved=store.Resolve(Prepare(e,v2?"7dtd":"minecraft"));var transition=v2?native.Apply(resolved):legacy.Apply(resolved);if(transition.Forward)store.Commit(resolved);return transition;}
var spawn=Delta(Fixture(),"spawn",1,new JsonObject{["model"]="survivor",["renderer"]="humanoid",["variant"]="test",["scale"]=1.0},true);
var key=new EntityKey("7dtd",spawn["world_id"].GetValue<string>(),spawn["dimension"].GetValue<string>(),spawn["entity_id"].GetValue<string>());
Check(Apply(spawn).Forward && store.Get(key)["model"].GetValue<string>()=="survivor","Presentation Spawn");
Check(!Apply(spawn).Forward && store.Count==1,"duplicate spawn leaves presentation intact");
Check(Apply(Delta(spawn,"update",2,new JsonObject{["scale"]=1.5},true)).Forward && store.Get(key)["scale"].GetValue<double>()==1.5 && store.Get(key)["model"].GetValue<string>()=="survivor" && store.Get(key)["variant"].GetValue<string>()=="test","Presentation Update");
Apply(Delta(spawn,"update",3));Check(store.Get(key)["scale"].GetValue<double>()==1.5,"omitted presentation preserves state");
var old=store.Get(key).ToJsonString();Check(!Apply(Delta(spawn,"update",2,new JsonObject{["model"]="old"},true)).Forward && store.Get(key).ToJsonString()==old,"stale v2 patch no mutation");
var health=new HealthComponents();var policy=ComponentPermissions.Load(Path.Combine(root,"config"));var snapshot=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"docs/examples/entity_components/7dtd_snapshot.json"))).AsObject();
foreach(var field in new[]{"source","authority","origin","entity_id","entity_type","world_id","dimension","stream_id"})snapshot[field]=spawn[field].DeepClone();
snapshot["entity_sequence"]=3;snapshot["components"]=new JsonObject{["health"]=new JsonObject{["current"]=80,["max"]=100},["name"]=new JsonObject{["text"]="Steve"},["custom_metadata"]=new JsonObject{["tag.test"]="kept"}};
using(var d=JsonDocument.Parse(snapshot.ToJsonString()))health.Apply(d.RootElement,"7dtd",native,policy);
var before=health.Get(key);Check(Apply(Delta(spawn,"update",4,null,true)).Forward && store.Get(key)==null && health.Get(key)==before,"Presentation Remove");
Check(health.Get(key).Current==80 && health.Get(key).Components["name"]["text"].GetValue<string>()=="Steve","remove preserves Health and Identity");
Apply(Delta(spawn,"update",5,new JsonObject{["model"]="added"},true));Check(store.Get(key)["renderer"].GetValue<string>()=="unknown" && store.Get(key)["model"].GetValue<string>()=="added","add after remove uses generic baseline");
Apply(Delta(spawn,"despawn",6));Check(store.Count==0 && native.Count==0,"despawn deletes presentation");
var fresh=Fixture();fresh["entity_id"]=Guid.NewGuid().ToString();fresh["origin"]["entity_id"]=fresh["entity_id"].DeepClone();var resolved=store.Resolve(Prepare(fresh,"7dtd"));Check(resolved["components"]["presentation"]["model"].GetValue<string>()=="survivor","type default on legacy v2 spawn");
var unknown=fresh.DeepClone().AsObject();unknown["entity_type"]="7dtd:zombie";Check(store.Resolve(Prepare(unknown,"7dtd"))["components"]["presentation"]["renderer"].GetValue<string>()=="unknown","unknown type generic default");
var mc=Fixture(false);Check(Apply(mc,false).Forward && legacy.Find(new EntityKey("minecraft",mc["world_id"].GetValue<string>(),mc["dimension"].GetValue<string>(),mc["entity_id"].GetValue<string>())).State["components"]["presentation"]!=null,"Backward Compatibility");
var mcKey=new EntityKey("minecraft",mc["world_id"].GetValue<string>(),mc["dimension"].GetValue<string>(),mc["entity_id"].GetValue<string>());var detached=store.Get(mcKey);detached["model"]="tampered";Check(store.Get(mcKey)["model"].GetValue<string>()!="tampered","snapshot copy isolation");
Reject(spawn,"wrong source session","minecraft");var forged=spawn.DeepClone().AsObject();forged["authority"]="minecraft";Reject(forged,"wrong authority");forged=spawn.DeepClone().AsObject();forged["origin"]["entity_id"]=Guid.NewGuid().ToString();Reject(forged,"wrong origin");
foreach(var value in new JsonNode[]{JsonValue.Create(0),JsonValue.Create(-1),JsonValue.Create(17),JsonValue.Create("1"),JsonValue.Create(true),null})Reject(Delta(spawn,"update",7,new JsonObject{["scale"]=value},true),"invalid scale "+value);
foreach(var text in new[]{"", "../model", "https://evil",new string('x',65),"humanoid\n"})Reject(Delta(spawn,"update",7,new JsonObject{["model"]=text},true),"invalid model "+text);
Reject(Delta(spawn,"update",7,new JsonObject{["authority"]="minecraft"},true),"component owner injection");Reject(Delta(spawn,"spawn",7,null,true),"remove on spawn");Reject(Delta(spawn,"despawn",7,new JsonObject(),true),"presentation on despawn");
store.Clear();Check(store.Count==0,"reconnect clears presentation");
var scene=new TestScene();var logs=new ConcurrentQueue<string>();var controller=new MarkerController(scene,logs.Enqueue);var wire=new WireMessage{Type="entity_state",Version=1,Source="minecraft",StreamId="s",EntityId="i",EntityType="minecraft:marker",WorldId="w",Dimension="d",Lifecycle=new EntityLifecycle{Event="spawn"},Position=new EntityPosition{X=1,Y=2,Z=3,Space="7dtd"},Rotation=new EntityRotation{Yaw=0,Pitch=0,Roll=0},Components=new PresentationComponents{Presentation=new MC7DTD.PresentationComponent{Renderer="humanoid",Model="survivor",Scale=1}}};
controller.Enqueue(wire);wire.Components.Presentation.Model="tampered";controller.Tick(true);Check(logs.Any(l=>l.Contains("Spawn proxy:")&&l.Contains("model=survivor")),"7DTD snapshot captures presentation before game thread");
wire.Lifecycle.Event="update";wire.Components.Presentation=new MC7DTD.PresentationComponent{Scale=1.5};controller.Enqueue(wire);controller.Tick(true);Check(logs.Any(l=>l.Contains("[Presentation]")&&l.Contains("model=survivor")&&l.Contains("scale=1.5")),"7DTD partial update storage");
wire.Components.Presentation=null;controller.Enqueue(wire);controller.Tick(true);Check(logs.Any(l=>l.Contains("removed=True")&&l.Contains("model=default")),"7DTD remove resets fallback");controller.Reset();controller.Tick(true);Check(scene.Count==0,"7DTD reset cleans proxy");

var children=new List<Process>();var bridgeLogs=new ConcurrentQueue<string>();var javaLogs=new ConcurrentQueue<string>();
using var gameStop=new CancellationTokenSource(); Thread gameThread=null;
Process Start(string exe,ConcurrentQueue<string> target,params string[] argv){var info=new ProcessStartInfo(exe){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.Environment["TEMP"]=Path.Combine(root,"work");info.Environment["TMP"]=Path.Combine(root,"work");foreach(var a in argv)info.ArgumentList.Add(a);var p=Process.Start(info);children.Add(p);p.OutputDataReceived+=(_,e)=>{if(e.Data!=null)target.Enqueue(e.Data);};p.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)target.Enqueue(e.Data);};p.BeginOutputReadLine();p.BeginErrorReadLine();return p;}
async Task Wait(Func<bool> condition,string label){for(int i=0;i<150;i++){if(condition())return;await Task.Delay(100);}throw new Exception("Timeout "+label);}
async Task Send(ClientWebSocket s,JsonObject e)=>await s.SendAsync(Encoding.UTF8.GetBytes(e.ToJsonString()),WebSocketMessageType.Text,true,CancellationToken.None);
try{
var tcp=new TcpListener(IPAddress.Loopback,0);tcp.Start();var port=((IPEndPoint)tcp.LocalEndpoint).Port;tcp.Stop();var config=Path.Combine(output,"network.json");File.WriteAllText(config,JsonSerializer.Serialize(new{host="localhost",port}));File.WriteAllText(Path.Combine(output,"coordinate.json"),"{\"scale\":1,\"offsetX\":0,\"offsetY\":0,\"offsetZ\":0}");
Start("dotnet",bridgeLogs,Environment.GetEnvironmentVariable("MC7DTD_TEST_BRIDGE") ?? Path.Combine(root,"bridge-server/bin/Phase373/net10.0/BridgeServer.dll"),config);await Wait(()=>bridgeLogs.Any(l=>l.Contains("listening")),"Bridge start");
var liveLogs=new ConcurrentQueue<string>();var liveScene=new TestScene();var liveController=new MarkerController(liveScene,liveLogs.Enqueue);using var td=new BridgeClient(config,liveLogs.Enqueue,liveController.Enqueue,liveController.Reset);td.Start();await Wait(()=>liveLogs.Any(l=>l.Contains("7DTD connected")),"7DTD connect");
gameThread=new Thread(()=>{while(!gameStop.IsCancellationRequested){liveController.Tick(true);Thread.Sleep(10);}liveController.Reset();liveController.Tick(true);});gameThread.Start();
using(var socket=new ClientWebSocket()){
await socket.ConnectAsync(new Uri($"ws://localhost:{port}/ws"),CancellationToken.None);await Send(socket,new JsonObject{["type"]="minecraft_connect",["client"]="minecraft"});await Wait(()=>liveLogs.Any(l=>l.Contains("Minecraft connected")) || td.NativeEpoch>0,"peer connected");
var live=Delta(Fixture(false),"spawn",1,new JsonObject{["renderer"]="humanoid",["model"]="survivor",["scale"]=1.0},true);await Send(socket,live);await Wait(()=>liveLogs.Any(l=>l.Contains("Spawn proxy:")&&l.Contains("model=survivor")),"live v1 spawn");Check(true,"WebSocket Minecraft -> Bridge -> 7DTD spawn");
await Send(socket,Delta(live,"update",2,new JsonObject{["scale"]=1.5},true));await Wait(()=>liveLogs.Any(l=>l.Contains("[Presentation]")&&l.Contains("scale=1.5")),"live scale");Check(true,"WebSocket partial scale reaches 7DTD");
await Send(socket,Delta(live,"update",3,null,true));await Wait(()=>liveLogs.Any(l=>l.Contains("removed=True")),"live remove");Check(true,"WebSocket remove reaches 7DTD");
await Send(socket,Delta(live,"despawn",4));await Wait(()=>liveLogs.Any(l=>l.Contains("Marker despawned:")),"live despawn");using var closing=new CancellationTokenSource(TimeSpan.FromSeconds(5));await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,"test complete",closing.Token);
}
await Wait(()=>bridgeLogs.Any(l=>l.Contains("Disconnected: minecraft")),"old MC disconnected");
var gson=Directory.GetFiles(Path.Combine(root,"work/gradle-home/caches"),"gson-2.13.2.jar",SearchOption.AllDirectories).First();var cp=Path.Combine(root,"minecraft-mod/build/classes/java/test")+";"+Path.Combine(root,"minecraft-mod/build/classes/java/main")+";"+gson;
Start("C:/Program Files/Java/jdk-21.0.12/bin/java.exe",javaLogs,"-cp",cp,"io.mc7dtd.PresentationHarness",config,"listen");await Wait(()=>javaLogs.Any(l=>l.Contains("Test received from 7dtd")),"Java handshake");
var v2=Fixture();v2["components"]=new JsonObject{["presentation"]=new JsonObject{["model"]="live_survivor",["renderer"]="humanoid",["scale"]=1.0}};
NativeEntityMessage Native(JsonObject e){using var data=new MemoryStream(Encoding.UTF8.GetBytes(e.ToJsonString()));return (NativeEntityMessage)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(NativeEntityMessage),new System.Runtime.Serialization.Json.DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true}).ReadObject(data);}
Check(td.PublishNative(Native(v2),td.NativeEpoch),"7DTD publish native presentation");await Wait(()=>javaLogs.Any(l=>l.Contains("[Presentation]")&&l.Contains("model=live_survivor")),"Java initial");
td.PublishNative(Native(Delta(v2,"update",2,new JsonObject{["scale"]=1.5},true)),td.NativeEpoch);await Wait(()=>javaLogs.Any(l=>l.Contains("[Presentation]")&&l.Contains("scale=1.5")&&l.Contains("model=live_survivor")),"Java partial");Check(true,"WebSocket 7DTD -> Bridge -> Minecraft partial update");
td.PublishNative(Native(Delta(v2,"update",3,null,true)),td.NativeEpoch);await Wait(()=>javaLogs.Any(l=>l.Contains("[Presentation]")&&l.Contains("removed=true")),"Java remove");Check(true,"WebSocket Java remove logged");
td.PublishNative(Native(Delta(v2,"despawn",4)),td.NativeEpoch);await Wait(()=>javaLogs.Any(l=>l.Contains("7DTD entity received:")&&l.Contains("despawn")),"Java despawn");Check(!javaLogs.Any(l=>l.Contains("Bridge error"))&&!liveLogs.Any(l=>l.Contains("Bridge error")),"bidirectional transport no protocol errors");
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("TOTAL "+checks.Count+" passed");
File.WriteAllLines(Path.Combine(output,"7dtd-client.log"),liveLogs);
}finally{gameStop.Cancel();gameThread?.Join();foreach(var p in children){try{if(!p.HasExited)p.Kill(true);p.WaitForExit();p.Dispose();}catch{}}File.WriteAllLines(Path.Combine(output,"bridge.log"),bridgeLogs);File.WriteAllLines(Path.Combine(output,"minecraft.log"),javaLogs);}

sealed class TestScene:IMarkerScene{public int Count;public object Create(string name,double x,double y,double z){Count++;return new object();}public void Move(object handle,double x,double y,double z){}public void Delete(object handle){Count--;}}
