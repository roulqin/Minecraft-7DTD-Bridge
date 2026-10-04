using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using MC7DTD;
using MC7DTD.Bridge;

var root=Path.GetFullPath(args[0]);var output=Path.Combine(root,"work/phase3_8_1-test");Directory.CreateDirectory(output);
var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);Console.WriteLine("PASS "+name);}
JsonObject Load(string p)=>JsonNode.Parse(File.ReadAllText(Path.Combine(root,p))).AsObject();
var entity=Load("docs/examples/entity_state_v2/7dtd_spawn.json");entity["entity_type"]="7dtd:player";
var initial=Load("docs/examples/equipment_components/initial.json");foreach(var f in new[]{"source","authority","origin","entity_id","world_id","dimension","entity_type","stream_id"})initial[f]=entity[f].DeepClone();
var registry=new NativeEntityRegistry();registry.Apply(entity);var health=new HealthComponents();var eq=new EquipmentComponents(health);
var permissions=ComponentPermissions.Load(Path.Combine(root,"config"));var equipmentPermissions=new EquipmentPermissions(Path.Combine(root,"config"));
var key=new EntityKey("7dtd",entity["world_id"].GetValue<string>(),entity["dimension"].GetValue<string>(),entity["entity_id"].GetValue<string>());
EntityTransition Apply(JsonObject m,string role="7dtd",EquipmentPermissions grant=null){using var d=JsonDocument.Parse(m.ToJsonString());return eq.Apply(d.RootElement,role,registry,permissions,grant??equipmentPermissions);}
JsonObject Patch(long rev,long baseline,JsonObject delta){var m=initial.DeepClone().AsObject();m["revision"]=rev;m["base_revision"]=baseline;m["mode"]="patch";m["components"]=delta;return m;}
void Reject(JsonObject m,string name,string role="7dtd",EquipmentPermissions grant=null){var before=health.Get(key);var old=eq.Get(key)?.ToJsonString();bool denied=false;try{Apply(m,role,grant);}catch(Exception ex)when(ex is InvalidDataException or InvalidOperationException or FormatException){denied=true;}Check(denied && before==health.Get(key) && old==eq.Get(key)?.ToJsonString(),"atomic reject "+name);}
Check(Apply(initial).Forward && eq.Get(key)["slots"].AsObject().Count==5,"initial full equipment snapshot");
var oldHealth=health.Get(key).Components.DeepClone();
Apply(Patch(2,1,new JsonObject{["equipment"]=new JsonObject{["slots"]=new JsonObject{["held_item"]=new JsonObject{["item_id"]="7dtd:gunHandgunT1Pistol"}}}}));
Check(eq.Get(key)["slots"]["head"]!=null && eq.Get(key)["slots"]["held_item"]["item_id"].GetValue<string>()=="7dtd:gunHandgunT1Pistol","change held slot preserves armor");
Check(JsonNode.DeepEquals(oldHealth,health.Get(key).Components) && health.Get(key).Revision==2,"equipment-only patch shares revision and preserves Health Identity");
Apply(Patch(3,2,new JsonObject{["equipment"]=new JsonObject{["slots"]=new JsonObject{["held_item"]=null,["head"]=null}}}));
Check(eq.Get(key)["slots"]["held_item"]==null && eq.Get(key)["slots"]["head"]==null && eq.Get(key)["slots"].AsObject().Count==5,"empty hand and unequip armor");
Apply(Patch(4,3,new JsonObject{["health"]=new JsonObject{["current"]=92,["max"]=100}}));
Check(health.Get(key).Current==92 && eq.Get(key)["slots"].AsObject().Count==5,"health-only patch preserves equipment");
var presentation=new MC7DTD.Bridge.PresentationComponent(Path.Combine(root,"config/entity_types.json"));presentation.Commit(presentation.Resolve(entity));
var inspector=new EntityInspector(new EntityRegistry(),registry,health,presentation,eq);inspector.ObserveAccepted(entity);
var inspected=inspector.List().Single();Check(inspected["components"]["equipment"]["slots"].AsObject().Count==5,"Inspector equipment view");
inspected["components"]["equipment"]["slots"]["body"]=new JsonObject();Check(eq.Get(key)["slots"]["body"]==null,"Inspector copy cannot mutate equipment");
var missingDir=Path.Combine(output,"missing-permissions");Directory.CreateDirectory(missingDir);var deniedPolicy=new EquipmentPermissions(missingDir);
Reject(Patch(5,4,new JsonObject{["equipment"]=null}),"missing remove permission",grant:deniedPolicy);
Reject(Patch(5,4,new JsonObject{["equipment"]=null}),"role",role:"minecraft");
Reject(Patch(5,99,new JsonObject{["equipment"]=null}),"shared baseline");
var malformedMode=Patch(5,4,new JsonObject{["equipment"]=null});malformedMode["mode"]="unknown";Reject(malformedMode,"invalid mode equipment-only patch");
var badInteger=Patch(5,4,new JsonObject{["equipment"]=null});badInteger["base_revision"]=true;Reject(badInteger,"boolean baseline");
var nullSnapshot=initial.DeepClone().AsObject();nullSnapshot["revision"]=5;nullSnapshot["components"]["equipment"]=null;Reject(nullSnapshot,"snapshot component null");
var incompleteSnapshot=initial.DeepClone().AsObject();incompleteSnapshot["revision"]=5;incompleteSnapshot["components"]["equipment"]["slots"].AsObject().Remove("feet");Reject(incompleteSnapshot,"incomplete snapshot");
var bad=Patch(5,4,new JsonObject{["equipment"]=new JsonObject{["slots"]=new JsonObject{["body"]=null}},["health"]=new JsonObject{["current"]=101,["max"]=100}});Reject(bad,"invalid health with equipment");
foreach(var item in new[]{"minecraft:stone","7dtd:a\n",new string('a',129)})Reject(Patch(5,4,new JsonObject{["equipment"]=new JsonObject{["slots"]=new JsonObject{["body"]=new JsonObject{["item_id"]=item}}}}),"bad item id");
Reject(Patch(5,4,new JsonObject{["equipment"]=new JsonObject{["slots"]=new JsonObject{["legs"]=null}}}),"unknown slot");
Reject(Patch(5,4,new JsonObject{["equipment"]=new JsonObject{["slots"]=new JsonObject()}}),"empty slots");
bad=Patch(5,4,new JsonObject{["equipment"]=null});bad["stream_id"]=Guid.NewGuid().ToString("D");Reject(bad,"stream");
bad=Patch(5,4,new JsonObject{["equipment"]=null});bad["entity_sequence"]=2;Reject(bad,"entity sequence");
bad=Patch(5,4,new JsonObject{["equipment"]=null});bad["authority"]="minecraft";Reject(bad,"Authority unchanged");
Apply(Patch(5,4,new JsonObject{["equipment"]=null}));Check(eq.Get(key)==null && health.Get(key).Current==92,"whole equipment remove preserves health");
Reject(Patch(6,5,new JsonObject{["equipment"]=new JsonObject{["slots"]=new JsonObject{["body"]=null}}}),"partial add after remove");
Apply(Patch(6,5,new JsonObject{["equipment"]=initial["components"]["equipment"].DeepClone()}));
Check(eq.Get(key)!=null,"complete add after remove");
Check(!Apply(Patch(6,5,new JsonObject{["equipment"]=null})).Forward && eq.Get(key)!=null,"stale revision ignored");
Reject(Patch(6,5,new JsonObject{["equipment"]=new JsonObject{["slots"]=new JsonObject{["legs"]=null}}}),"invalid stale content");
bad=initial.DeepClone().AsObject();bad["revision"]=7;bad["components"]=new JsonObject{["health"]=new JsonObject{["current"]=91,["max"]=100}};
Reject(bad,"snapshot omission remove permission",grant:deniedPolicy);Apply(bad);Check(eq.Get(key)==null,"snapshot omission clears equipment");
eq.Retire(entity);health.Retire(entity);Check(eq.Get(key)==null && health.Count==0,"retire clears shared and equipment records");

long epoch=1;var sent=new List<JsonObject>();var samples=0;
var publisher=new NativePlayerPublisher(()=>epoch,(m,e)=>{sent.Add(JsonNode.Parse(ComponentMessage.Json(m)).AsObject());return true;},null,(m,e)=>{sent.Add(JsonNode.Parse(m.Serialize()).AsObject());return true;});
var slots=new Dictionary<string,string>{{"head","7dtd:armorPrimitiveHood"},{"body",null},{"hands",null},{"feet",null},{"held_item","7dtd:meleeToolRepairT0StoneAxe"}};
var identity=new IdentitySample{Enabled=true,NativeName="EquipmentTest",Tags=new(){{"tag.source","7dtd"}}};var player=new object();var now=DateTime.UtcNow;bool available=true;
void Tick(DateTime t)=>publisher.Tick(player,"td-equipment",1,70,1,0,0,0,t,95,100,identity,()=>{samples++;return available ? slots : null;});
Tick(now);Check(sent[^1]["mode"].GetValue<string>()=="snapshot" && sent[^1]["components"]["equipment"]["slots"].AsObject().Count==5,"publisher spawn snapshot includes equipment");
Tick(now.AddMilliseconds(100));Check(samples==1,"equipment sampling bounded to main-thread publish interval");
slots["held_item"]="7dtd:gunHandgunT1Pistol";Tick(now.AddSeconds(1));Check(sent[^1]["components"].AsObject().Count==1 && sent[^1]["components"]["equipment"]["slots"].AsObject().Count==1,"publisher sends one slot patch only");
slots["held_item"]=null;slots["head"]=null;Tick(now.AddSeconds(2));Check(sent[^1]["components"]["equipment"]["slots"]["held_item"]==null,"publisher sends slot remove");
available=false;Tick(now.AddSeconds(3));Check(sent[^1]["components"].AsObject().Count==1 && sent[^1]["components"]["equipment"]==null && sent[^2]["lifecycle"]["event"].GetValue<string>()=="update","unavailable equipment removes only Equipment on active entity");
available=true;Tick(now.AddSeconds(4));Check(sent[^1]["mode"].GetValue<string>()=="patch" && sent[^1]["components"]["equipment"]["slots"].AsObject().Count==5,"restored observation sends complete equipment add");
publisher.Leave("world_unloaded");Check(sent[^2]["components"].AsObject().Count==4 && sent[^2]["components"]["equipment"]==null && sent[^1]["lifecycle"]["event"].GetValue<string>()=="despawn","publisher equipment remove precedes normal despawn");
epoch=2;Tick(now.AddSeconds(5));Check(sent[^1]["revision"].GetValue<long>()==1 && sent[^1]["mode"].GetValue<string>()=="snapshot","reconnect fresh combined snapshot");
File.WriteAllText(Path.Combine(output,"sender-events.json"),JsonSerializer.Serialize(sent,new JsonSerializerOptions{WriteIndented=true}));

var children=new List<Process>();var bridgeLogs=new ConcurrentQueue<string>();var mcLogs=new ConcurrentQueue<string>();var tdLogs=new ConcurrentQueue<string>();
Process Start(string exe,ConcurrentQueue<string> logs,params string[] arguments){var info=new ProcessStartInfo(exe){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.Environment["TEMP"]=Path.Combine(root,"work");info.Environment["TMP"]=Path.Combine(root,"work");foreach(var a in arguments)info.ArgumentList.Add(a);var p=Process.Start(info);children.Add(p);p.OutputDataReceived+=(_,e)=>{if(e.Data!=null)logs.Enqueue(e.Data);};p.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)logs.Enqueue(e.Data);};p.BeginOutputReadLine();p.BeginErrorReadLine();return p;}
async Task Wait(Func<bool> f,string label){for(int i=0;i<150;i++){if(f())return;await Task.Delay(100);}throw new Exception("Timeout "+label);}
try {
 var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();var config=Path.Combine(output,"network.json");File.WriteAllText(config,JsonSerializer.Serialize(new{host="localhost",port}));File.WriteAllText(Path.Combine(output,"coordinate.json"),"{\"scale\":1,\"offsetX\":0,\"offsetY\":0,\"offsetZ\":0}");File.WriteAllText(Path.Combine(output,"debug.json"),"{\"debug_name_tag\":false,\"debug_logging\":false}");
 Start("dotnet",bridgeLogs,Path.Combine(root,"bridge-server/bin/Phase381/net10.0/BridgeServer.dll"),config);await Wait(()=>bridgeLogs.Any(l=>l.Contains("listening")),"bridge start");
 var gson=Directory.GetFiles(Path.Combine(root,"work/gradle-home/caches"),"gson-2.13.2.jar",SearchOption.AllDirectories).First();var cp=Path.Combine(root,"minecraft-mod/build/classes/java/test")+";"+Path.Combine(root,"minecraft-mod/build/classes/java/main")+";"+gson;
 Start("C:/Program Files/Java/jdk-21.0.12/bin/java.exe",mcLogs,"-cp",cp,"io.mc7dtd.EquipmentHarness",config,"live");
 using var client=new BridgeClient(config,tdLogs.Enqueue);client.Start();await Wait(()=>mcLogs.Any(l=>l.Contains("Hello from 7DTD")),"handshake");
 var live=new NativePlayerPublisher(()=>client.NativeEpoch,client.PublishNative,client.PublishHealth,client.PublishComponents);
 void LiveTick(int second)=>live.Tick(player,"td-live-equipment",1,70,1,0,0,0,now.AddSeconds(second),95,100,identity,()=>slots);
 slots["head"]="7dtd:armorPrimitiveHood";slots["held_item"]="7dtd:meleeToolRepairT0StoneAxe";LiveTick(0);
 await Wait(()=>mcLogs.Any(l=>l.StartsWith("EQUIPMENT_INSPECT ") && l.Contains("meleeToolRepairT0StoneAxe")),"live full snapshot");Check(true,"C# native publisher -> WebSocket Bridge -> Java equipment -> Inspector");
 using var http=new HttpClient();var snapshot=JsonNode.Parse(await http.GetStringAsync($"http://localhost:{port}/debug/entities"));var view=snapshot["entities"].AsArray().Single();Check(view["components"]["equipment"]["slots"]["head"]!=null && view["components"]["health"]["current"].GetValue<double>()==95 && view["components"]["identity"]["name"].GetValue<string>()=="EquipmentTest" && view["components"]["presentation"]["model"].GetValue<string>()=="survivor","Bridge Inspector retains equipment Health Identity Presentation");
 File.WriteAllText(Path.Combine(output,"inspector-snapshot.json"),view.ToJsonString(new(){WriteIndented=true}));
 slots["held_item"]="7dtd:gunHandgunT1Pistol";LiveTick(1);await Wait(()=>mcLogs.Any(l=>l.StartsWith("EQUIPMENT_INSPECT ")&&l.Contains("gunHandgunT1Pistol")),"live held change");Check(true,"live held slot update");
 slots["held_item"]=null;slots["head"]=null;LiveTick(2);await Wait(()=>mcLogs.Any(l=>l.StartsWith("EQUIPMENT_INSPECT ")&&l.Contains("\"held_item\":null")&&l.Contains("\"head\":null")),"live empty slots");Check(true,"live empty hand and armor removal");
 live.Leave("world_unloaded");await Wait(()=>mcLogs.Any(l=>l.Contains("equipment received")&&l.Contains("removed=true"))&&mcLogs.Any(l=>l=="EQUIPMENT_EMPTY"),"live exit cleanup");
 snapshot=JsonNode.Parse(await http.GetStringAsync($"http://localhost:{port}/debug/entities"));Check(snapshot["entities"].AsArray().Count==0,"normal exit clears Bridge and Minecraft views");
 Check(!mcLogs.Any(l=>l.Contains("rejected:"))&&!tdLogs.Any(l=>l.Contains("Bridge error")),"live no component rejection");
 File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("TOTAL "+checks.Count+" passed");
}finally{foreach(var p in children){try{if(!p.HasExited)p.Kill(true);p.WaitForExit();p.Dispose();}catch{}}File.WriteAllLines(Path.Combine(output,"bridge.log"),bridgeLogs);File.WriteAllLines(Path.Combine(output,"minecraft.log"),mcLogs);File.WriteAllLines(Path.Combine(output,"7dtd-client.log"),tdLogs);}
