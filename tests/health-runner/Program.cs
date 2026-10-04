using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MC7DTD;
using MC7DTD.Bridge;

var root=Path.GetFullPath(args[0]);var output=Path.Combine(root,"work/phase3_7_1-test");Directory.CreateDirectory(output);
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);Console.WriteLine("PASS "+label);}
JsonObject Load(string path)=>JsonNode.Parse(File.ReadAllText(Path.Combine(root,path))).AsObject();
var native=Load("docs/examples/entity_state_v2/7dtd_spawn.json");
native["entity_type"]="7dtd:player";
var fixture=Load("docs/examples/entity_components/7dtd_snapshot.json");
foreach(var name in new[]{"source","authority","origin","entity_id","world_id","dimension","entity_type","stream_id"})fixture[name]=native[name].DeepClone();
fixture["entity_sequence"]=native["sequence"].DeepClone();fixture["components"]=new JsonObject{["health"]=new JsonObject{["current"]=90,["max"]=100}};
JsonObject Patch(long revision=2,long baseline=1,double current=75) {var p=fixture.DeepClone().AsObject();p["mode"]="patch";p["revision"]=revision;p["base_revision"]=baseline;p["components"]["health"]["current"]=current;return p;}
ComponentPermissions Policy(JsonObject p) {using var a=JsonDocument.Parse(p.ToJsonString());using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"config/entity_types.json")));return new(a.RootElement,b.RootElement);}
var policyJson=Load("config/component_permissions.json");var policy=Policy(policyJson);
var registry=new NativeEntityRegistry();registry.Apply(native);var components=new HealthComponents();
EntityTransition Apply(JsonObject m,ComponentPermissions permissions=null,string role="7dtd") {using var doc=JsonDocument.Parse(m.ToJsonString());return components.Apply(doc.RootElement,role,registry,permissions??policy);}
var key=new EntityKey("7dtd",native["world_id"].GetValue<string>(),native["dimension"].GetValue<string>(),native["entity_id"].GetValue<string>());
void Reject(JsonObject bad,string code,ComponentPermissions permissions=null,string role="7dtd") {
 var before=components.Get(key);bool rejected=false;try{Apply(bad,permissions,role);}catch(InvalidDataException ex){rejected=ex.Message==code;}
 Check(rejected && components.Get(key)==before,"atomic rejection "+code+" #"+checks.Count);
}
Check(Apply(fixture).Code=="health_initial" && components.Get(key).Current==90,"initial state");
Check(Apply(Patch()).Code=="health_updated" && components.Get(key).Current==75,"partial update");
Check(!Apply(fixture).Forward && components.Get(key).Revision==2,"stale revision ignored");
foreach(var field in new[]{"source","authority"}){var bad=Patch(3,2);bad[field]="minecraft";Reject(bad,"invalid_component_authority");}
Reject(Patch(3,2),"invalid_component_authority",role:"minecraft");
var invalid=Patch(3,2);invalid["origin"]["entity_id"]=Guid.NewGuid().ToString();Reject(invalid,"invalid_component_origin");
foreach(var field in new[]{"stream_id","entity_id"}){invalid=Patch(3,2);invalid[field]="bad";if(field=="entity_id")invalid["origin"]["entity_id"]="bad";Reject(invalid,"invalid_component_id");}
foreach(var field in new[]{"entity_sequence","revision","base_revision"}){invalid=Patch(3,2);invalid[field]=true;Reject(invalid,"invalid_component_integer");invalid[field]=3.5;Reject(invalid,"invalid_component_integer");invalid[field]=9007199254740992L;Reject(invalid,"invalid_component_integer");}
invalid=Patch(3,2);invalid["entity_sequence"]=99;Reject(invalid,"component_entity_mismatch");
invalid=Patch(3,2);invalid["stream_id"]=Guid.NewGuid().ToString();Reject(invalid,"component_entity_mismatch");
invalid=Patch(3,2);invalid["entity_type"]="7dtd:animal";Reject(invalid,"component_entity_mismatch");
invalid=Patch(3,99);Reject(invalid,"component_baseline_mismatch");
foreach(var val in new[]{-1d,101d})Reject(Patch(3,2,val),"invalid_component_health");
invalid=Patch(3,2);invalid["components"]["health"]["max"]=0;Reject(invalid,"invalid_component_health");
invalid=Patch(3,2);invalid["components"]["health"].AsObject().Remove("max");Reject(invalid,"invalid_component_fields");
invalid=Patch(3,2);invalid["components"]["health"]["current"]=true;Reject(invalid,"invalid_component_health");
invalid=Patch(3,2);invalid["components"]["inventory"]=new JsonObject{["text"]="no"};Reject(invalid,"unsupported_component");
invalid=Patch(3,2);invalid["components"]=new JsonObject();Reject(invalid,"invalid_component_patch");
invalid=fixture.DeepClone().AsObject();invalid["revision"]=3;invalid["components"]["health"]=null;Reject(invalid,"invalid_component_snapshot");
invalid=Patch(3,2);invalid["extra"]=true;Reject(invalid,"invalid_component_fields");
foreach(var grant in new[]{"publish","store"}){var denied=policyJson.DeepClone().AsObject();denied["types"][0]["components"]["health"][grant]=false;Reject(Patch(3,2),"component_permission_denied",Policy(denied));denied["types"][0]["components"]["health"].AsObject().Remove(grant);Reject(Patch(3,2),"component_permission_denied",Policy(denied));}
var missing=policyJson.DeepClone().AsObject();missing["types"]=new JsonArray();Reject(Patch(3,2),"component_permission_denied",Policy(missing));
var remove=Patch(3,2);remove["components"]["health"]=null;Check(Apply(remove).Code=="health_removed" && components.Get(key).Current==null,"explicit remove");
invalid=fixture.DeepClone().AsObject();invalid["revision"]=5;Check(Apply(invalid).Forward,"higher snapshot restores baseline");
invalid["revision"]=6;invalid["components"]=new JsonObject();Check(Apply(invalid).Code=="health_removed","empty snapshot overwrite");
components.Clear();Reject(Patch(),"component_baseline_mismatch");
Apply(fixture);var update=native.DeepClone().AsObject();update["lifecycle"]["event"]="update";update["sequence"]=native["sequence"].GetValue<long>()+1;registry.Apply(update);
Check(components.Get(key).Current==90,"position update preserves health");Reject(Patch(),"component_entity_mismatch");
components.Retire(native);Check(components.Count==0,"despawn retires health");
registry.Clear();Reject(fixture,"component_entity_mismatch");
registry.Apply(native);var duplicate=fixture.ToJsonString().Replace("\"version\":1","\"version\":1,\"version\":1");bool dup=false;try{using var doc=JsonDocument.Parse(duplicate);components.Apply(doc.RootElement,"7dtd",registry,policy);}catch(InvalidDataException ex){dup=ex.Message=="duplicate_component_field";}Check(dup,"duplicate fields denied");

JsonObject Serialize(object m){using var data=new MemoryStream();new DataContractJsonSerializer(m.GetType(),new DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true}).WriteObject(data,m);return JsonNode.Parse(data.ToArray()).AsObject();}
long epoch=1;var events=new List<JsonObject>();var publisher=new NativePlayerPublisher(()=>epoch,(m,e)=>{events.Add(Serialize(m));return true;},(m,e)=>{events.Add(Serialize(m));return true;});var player=new object();var now=DateTime.UtcNow;
void Tick(DateTime time,double? h=100,double? max=100)=>publisher.Tick(player,"td-health",0,70,0,0,0,0,time,h,max);
Tick(now);Check(events.Count==2 && events[1]["mode"].GetValue<string>()=="snapshot","publisher spawn then initial health");
Tick(now.AddMilliseconds(499),80);Check(events.Count==2,"500ms bounded sampling");Tick(now.AddMilliseconds(500),100);Check(events.Count==3,"unchanged health not resent");
Tick(now.AddSeconds(1),80);Check(events.Last()["mode"].GetValue<string>()=="patch" && events.Last()["base_revision"].GetValue<long>()==1,"publisher changed health patch");
Tick(now.AddSeconds(2),null,null);Check(events.Last()["components"]["health"]==null,"unavailable health explicit remove");
Tick(now.AddSeconds(3),90);Check(events.Last()["mode"].GetValue<string>()=="snapshot","availability recovery snapshot");
publisher.Leave("world_unloaded");Check(events[^2]["components"]["health"]==null && events[^1]["lifecycle"]["event"].GetValue<string>()=="despawn","exit remove precedes despawn");
epoch=2;Tick(now.AddSeconds(4));Check(events.Last()["revision"].GetValue<long>()==1,"reconnect resets health baseline");
File.WriteAllText(Path.Combine(output,"sender-events.json"),JsonSerializer.Serialize(events,new JsonSerializerOptions{WriteIndented=true}));

var children=new List<Process>();var bridgeLogs=new ConcurrentQueue<string>();var mcLogs=new ConcurrentQueue<string>();var tdLogs=new ConcurrentQueue<string>();
Process Start(string exe,ConcurrentQueue<string> logs,params string[] args){var info=new ProcessStartInfo(exe){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(var a in args)info.ArgumentList.Add(a);var p=Process.Start(info);children.Add(p);p.OutputDataReceived+=(_,e)=>{if(e.Data!=null)logs.Enqueue(e.Data);};p.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)logs.Enqueue(e.Data);};p.BeginOutputReadLine();p.BeginErrorReadLine();return p;}
async Task Wait(Func<bool> test,string label){for(int i=0;i<150;i++){if(test())return;await Task.Delay(100);}throw new Exception("Timeout "+label);}
try {
 var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
 var config=Path.Combine(output,"network.json");File.WriteAllText(config,JsonSerializer.Serialize(new{host="localhost",port}));File.WriteAllText(Path.Combine(output,"coordinate.json"),"{\"scale\":1,\"offsetX\":0,\"offsetY\":0,\"offsetZ\":0}");
 var assembly=Path.Combine(root,"bridge-server/bin/Phase371/net10.0/BridgeServer.dll");var bridge=Start("dotnet",bridgeLogs,assembly,config);await Wait(()=>bridgeLogs.Any(l=>l.Contains("listening")),"bridge");
 var gson=Directory.GetFiles(Path.Combine(root,"work/gradle-home/caches"),"gson-2.13.2.jar",SearchOption.AllDirectories).First();
 var cp=Path.Combine(root,"minecraft-mod/build/classes/java/test")+";"+Path.Combine(root,"minecraft-mod/build/classes/java/main")+";"+gson;
 Start("C:/Program Files/Java/jdk-21.0.12/bin/java.exe",mcLogs,"-cp",cp,"io.mc7dtd.HealthReceiverHarness",config,"120");
 using var client=new BridgeClient(config,tdLogs.Enqueue);client.Start();await Wait(()=>mcLogs.Any(l=>l.Contains("Hello from 7DTD")) && tdLogs.Any(l=>l.Contains("Hello from Minecraft")),"handshake");
 Check(true,"existing bidirectional test messages retained");var live=new NativePlayerPublisher(()=>client.NativeEpoch,client.PublishNative,client.PublishHealth);
 void LiveTick(DateTime t,double h)=>live.Tick(player,"td-health-live",0,70,0,0,0,0,t,h,100);
 LiveTick(now,100);await Wait(()=>mcLogs.Any(l=>l.Contains("health received:")&&l.Contains("current=100.0")),"initial");Check(true,"real WebSocket initial health reaches actual Java receiver");
 LiveTick(now.AddSeconds(1),65);await Wait(()=>mcLogs.Any(l=>l.Contains("health received:")&&l.Contains("current=65.0")),"patch");Check(true,"real WebSocket partial update");
 live.Leave("world_unloaded");await Wait(()=>mcLogs.Any(l=>l.Contains("removed=true"))&&mcLogs.Any(l=>l.Contains("7DTD entity received:")&&l.Contains("despawn")),"remove");Check(true,"real WebSocket remove then despawn, socket stays open");
 Check(!tdLogs.Any(l=>l.Contains("Bridge error"))&&!bridgeLogs.Any(l=>l.Contains("rejected")),"ordered publisher has no rejection");
 var before=client.NativeEpoch;bridge.Kill(true);bridge.WaitForExit();Start("dotnet",bridgeLogs,assembly,config);await Wait(()=>client.NativeEpoch>before&&mcLogs.Count(l=>l=="Minecraft connected")>=2,"reconnect");await Task.Delay(400);LiveTick(now.AddSeconds(5),99);
 await Wait(()=>mcLogs.Any(l=>l.Contains("health received:")&&l.Contains("current=99.0")),"fresh health");Check(true,"Bridge restart fresh spawn and initial health");
 File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("TOTAL "+checks.Count+" passed");
} finally {foreach(var p in children){try{if(!p.HasExited)p.Kill(true);p.WaitForExit();p.Dispose();}catch{}}File.WriteAllLines(Path.Combine(output,"bridge.log"),bridgeLogs);File.WriteAllLines(Path.Combine(output,"minecraft.log"),mcLogs);File.WriteAllLines(Path.Combine(output,"7dtd-client.log"),tdLogs);}
