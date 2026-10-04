using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using MC7DTD;
using MC7DTD.Bridge;
var root=Path.GetFullPath(args[0]);var output=Path.Combine(root,"work/phase3_7_2-test");Directory.CreateDirectory(output);
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);Console.WriteLine("PASS "+label);}
JsonObject Load(string path)=>JsonNode.Parse(File.ReadAllText(Path.Combine(root,path))).AsObject();
var entity=Load("docs/examples/entity_state_v2/7dtd_spawn.json");entity["entity_type"]="7dtd:player";
var initial=Load("docs/examples/entity_components/7dtd_snapshot.json");foreach(var k in new[]{"source","authority","origin","entity_id","world_id","dimension","entity_type","stream_id"})initial[k]=entity[k].DeepClone();
initial["components"]=new JsonObject{["health"]=new JsonObject{["current"]=90,["max"]=100},["name"]=new JsonObject{["text"]="原生名字",["display_name"]="七日杀玩家"},["custom_metadata"]=new JsonObject{["tag.label"]="测试",["note"]=null}};
var registry=new NativeEntityRegistry();registry.Apply(entity);var components=new HealthComponents();var policyJson=Load("config/component_permissions.json");
ComponentPermissions Policy(JsonObject p){using var a=JsonDocument.Parse(p.ToJsonString());using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"config/entity_types.json")));return new(a.RootElement,b.RootElement);}
var policy=Policy(policyJson);var key=new EntityKey("7dtd",entity["world_id"].GetValue<string>(),entity["dimension"].GetValue<string>(),entity["entity_id"].GetValue<string>());
EntityTransition Apply(JsonObject m,ComponentPermissions p=null,string role="7dtd"){using var d=JsonDocument.Parse(m.ToJsonString());return components.Apply(d.RootElement,role,registry,p??policy);}
JsonObject Patch(long revision,long baseline,JsonObject delta){var m=initial.DeepClone().AsObject();m["revision"]=revision;m["base_revision"]=baseline;m["mode"]="patch";m["components"]=delta;return m;}
void Reject(JsonObject m,string label,ComponentPermissions p=null,string role="7dtd"){var before=components.Get(key);bool denied=false;try{Apply(m,p,role);}catch(InvalidDataException){denied=true;}Check(denied && components.Get(key)==before,"atomic rejection "+label);}
Check(Apply(initial).Forward,"initial all components");
var patch=Patch(2,1,new JsonObject{["name"]=new JsonObject{["text"]="原生名字",["display_name"]="已更新"}});Apply(patch);
Check(components.Get(key).Current==90 && components.Get(key).Components["custom_metadata"]["tag.label"].GetValue<string>()=="测试","name patch preserves health and tags");
patch=Patch(3,2,new JsonObject{["custom_metadata"]=new JsonObject{["tag.new"]="标签",["nullValue"]=null}});Apply(patch);
Check(!components.Get(key).Components["custom_metadata"].AsObject().ContainsKey("tag.label") && components.Get(key).Components["custom_metadata"].AsObject().ContainsKey("nullValue"),"metadata whole map replacement, ordinary null remains");
foreach(var deniedName in new[]{"name","custom_metadata"})foreach(var grant in new[]{"publish","store"}) {
 var denied=policyJson.DeepClone().AsObject();denied["types"][0]["components"][deniedName][grant]=false;
 Reject(Patch(4,3,new JsonObject{[deniedName]=null}),"remove permission "+deniedName+" "+grant,Policy(denied));
 denied["types"][0]["components"].AsObject().Remove(deniedName);
 Reject(Patch(4,3,new JsonObject{[deniedName]=null}),"missing grant "+deniedName+" "+grant,Policy(denied));
 var snapshot=initial.DeepClone().AsObject();snapshot["revision"]=4;snapshot["components"]=new JsonObject{["health"]=new JsonObject{["current"]=90,["max"]=100}};
 Reject(snapshot,"snapshot omission permission "+deniedName+" "+grant,Policy(denied));
}
foreach(var name in new[]{"", "a\nb",new string('x',129)})Reject(Patch(4,3,new JsonObject{["name"]=new JsonObject{["text"]="ok",["display_name"]=name}}),"invalid display name");
Reject(Patch(4,3,new JsonObject{["name"]=new JsonObject{["display_name"]="no canonical name"}}),"missing text");
Reject(Patch(4,3,new JsonObject{["name"]=new JsonObject{["text"]="ok",["authority"]="minecraft"}}),"name authority injection");
foreach(var value in new JsonNode[]{JsonValue.Create(true),JsonValue.Create(3),new JsonObject(),JsonValue.Create("x\ny"),null})Reject(Patch(4,3,new JsonObject{["custom_metadata"]=new JsonObject{["tag.test"]=value}}),"invalid tag value");
Reject(Patch(4,3,new JsonObject{["custom_metadata"]=new JsonObject{["nested"]=new JsonObject()}}),"nested metadata");
var forged=Patch(4,3,new JsonObject{["name"]=null});forged["authority"]="minecraft";Reject(forged,"owner");Reject(patch,"source session",role:"minecraft");
Reject(Patch(4,99,new JsonObject{["name"]=null}),"wrong base revision");
var stale=initial.DeepClone().AsObject();Check(!Apply(stale).Forward && components.Get(key).Revision==3,"old snapshot ignored");
var unicode=string.Concat(Enumerable.Repeat("😀",128));Apply(Patch(4,3,new JsonObject{["name"]=new JsonObject{["text"]=unicode}}));Check(components.Get(key).Revision==4,"128 Unicode scalar names");
Reject(Patch(5,4,new JsonObject{["name"]=new JsonObject{["text"]=unicode+"😀"}}),"129 Unicode scalar names");
Apply(Patch(5,4,new JsonObject{["name"]=null,["custom_metadata"]=null}));Check(components.Get(key).Current==90 && components.Get(key).Components.Count==1,"identity remove keeps health");
components.Retire(entity);Check(components.Count==0,"despawn retires shared component state");

long epoch=1;var events=new List<JsonObject>();var player=new object();var now=DateTime.UtcNow;
var publisher=new NativePlayerPublisher(()=>epoch,(m,e)=>{events.Add(JsonNode.Parse(ComponentMessage.Json(m)).AsObject());return true;},null,(m,e)=>{events.Add(JsonNode.Parse(m.Serialize()).AsObject());return true;});
var identity=new IdentitySample{Enabled=true,NativeName="native",DisplayName="初始",Tags=new Dictionary<string,string>{{"tag.label","one"}}};
void Tick(DateTime t,double health=100)=>publisher.Tick(player,"td-identity",1,70,1,0,0,0,t,health,100,identity);
Tick(now);Check(events.Count==2 && events[^1]["components"].AsObject().Count==3,"publisher single combined snapshot");
Tick(now.AddMilliseconds(500));Check(events.Count==3,"unchanged identity not resent");
identity.DisplayName="更新";Tick(now.AddSeconds(1));Check(events[^1]["components"].AsObject().Count==1 && events[^1]["components"]["name"]!=null,"identity-only partial update");
Tick(now.AddSeconds(2),88);Check(events[^1]["components"].AsObject().Count==1 && events[^1]["components"]["health"]!=null && events[^1]["base_revision"].GetValue<long>()==2,"health and identity share revision");
identity.Enabled=false;Tick(now.AddSeconds(3),88);Check(events[^1]["components"].AsObject().Count==2 && events[^1]["components"]["name"]==null,"publisher identity remove");
identity.Enabled=true;Tick(now.AddSeconds(4),88);Check(events[^1]["mode"].GetValue<string>()=="patch" && events[^1]["components"].AsObject().Count==2,"re-enable uses shared baseline");
publisher.Leave("world_unloaded");Check(events[^2]["components"].AsObject().Count==3 && events[^2]["components"].AsObject().All(p=>p.Value==null) && events[^1]["lifecycle"]["event"].GetValue<string>()=="despawn","all remove then despawn");
epoch=2;Tick(now.AddSeconds(5));Check(events[^1]["mode"].GetValue<string>()=="snapshot" && events[^1]["revision"].GetValue<long>()==1,"reconnect combined snapshot");
File.WriteAllText(Path.Combine(output,"sender-events.json"),JsonSerializer.Serialize(events,new JsonSerializerOptions{WriteIndented=true}));
var labelPath=Path.Combine(output,"labels-test.json");var labelLogs=new List<string>();var labels=new IdentityLabels(labelPath,labelLogs.Add);
File.WriteAllText(labelPath,"{\"enabled\":true,\"display_name\":null,\"tags\":{\"tag.label\":\"one\"}}");
var sampled=labels.Sample("native",now,"Native Display");Check(sampled.NameJson.Contains("Native Display"),"label default uses native display name");
File.WriteAllText(labelPath,"{\"enabled\":true,\"display_name\":\"overlay\",\"tags\":{\"tag.label\":\"two\"}}");
sampled=labels.Sample("native",now.AddMilliseconds(200),"Native Display");Check(sampled.DisplayName==null,"config reads bounded to 500ms");
sampled=labels.Sample("native",now.AddMilliseconds(500),"Native Display");Check(sampled.DisplayName=="overlay" && sampled.Tags["tag.label"]=="two","config update sampled");
File.WriteAllText(labelPath,"invalid-json");sampled=labels.Sample("native",now.AddSeconds(1));Check(sampled.DisplayName=="overlay" && labelLogs.Count==1,"invalid config keeps last valid identity");
File.WriteAllText(labelPath,"{\"enabled\":false,\"display_name\":null,\"tags\":{}}");sampled=labels.Sample("native",now.AddSeconds(2));Check(!sampled.Enabled,"config disables identity observation");

var children=new List<Process>();var bridgeLogs=new ConcurrentQueue<string>();var mcLogs=new ConcurrentQueue<string>();var tdLogs=new ConcurrentQueue<string>();
Process Start(string exe,ConcurrentQueue<string> logs,params string[] arguments){var info=new ProcessStartInfo(exe){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.Environment["TEMP"]=Path.Combine(root,"work");info.Environment["TMP"]=Path.Combine(root,"work");foreach(var a in arguments)info.ArgumentList.Add(a);var p=Process.Start(info);children.Add(p);p.OutputDataReceived+=(_,e)=>{if(e.Data!=null)logs.Enqueue(e.Data);};p.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)logs.Enqueue(e.Data);};p.BeginOutputReadLine();p.BeginErrorReadLine();return p;}
async Task Wait(Func<bool> condition,string label){for(int i=0;i<150;i++){if(condition())return;await Task.Delay(100);}throw new Exception("Timeout "+label);}
try {
 var tcp=new TcpListener(IPAddress.Loopback,0);tcp.Start();int port=((IPEndPoint)tcp.LocalEndpoint).Port;tcp.Stop();var config=Path.Combine(output,"network.json");File.WriteAllText(config,JsonSerializer.Serialize(new{host="localhost",port}));File.WriteAllText(Path.Combine(output,"coordinate.json"),"{\"scale\":1,\"offsetX\":0,\"offsetY\":0,\"offsetZ\":0}");
 Start("dotnet",bridgeLogs,Environment.GetEnvironmentVariable("MC7DTD_TEST_BRIDGE") ?? Path.Combine(root,"bridge-server/bin/Phase372/net10.0/BridgeServer.dll"),config);await Wait(()=>bridgeLogs.Any(l=>l.Contains("listening")),"bridge");
 var gson=Directory.GetFiles(Path.Combine(root,"work/gradle-home/caches"),"gson-2.13.2.jar",SearchOption.AllDirectories).First();var cp=Path.Combine(root,"minecraft-mod/build/classes/java/test")+";"+Path.Combine(root,"minecraft-mod/build/classes/java/main")+";"+gson;
 Start("C:/Program Files/Java/jdk-21.0.12/bin/java.exe",mcLogs,"-cp",cp,"io.mc7dtd.HealthReceiverHarness",config,"120");using var client=new BridgeClient(config,tdLogs.Enqueue);client.Start();await Wait(()=>mcLogs.Any(l=>l.Contains("Hello from 7DTD")),"connected");
 var live=new NativePlayerPublisher(()=>client.NativeEpoch,client.PublishNative,client.PublishHealth,client.PublishComponents);identity.Enabled=true;identity.DisplayName="Live-Initial";
 void LiveTick(DateTime t)=>live.Tick(player,"td-live",1,70,1,0,0,0,t,95,100,identity);
 LiveTick(now);await Wait(()=>mcLogs.Any(l=>l.Contains("identity received")&&l.Contains("Live-Initial")),"live initial");Check(true,"actual C# to Bridge to Java initial identity");
 identity.DisplayName="Live-Updated";identity.Tags["tag.label"]="updated";LiveTick(now.AddSeconds(1));await Wait(()=>mcLogs.Any(l=>l.Contains("identity received")&&l.Contains("Live-Updated")),"live partial");Check(true,"actual WebSocket partial identity");
 identity.Enabled=false;LiveTick(now.AddSeconds(2));await Wait(()=>mcLogs.Any(l=>l.Contains("identity received")&&l.Contains("removed=true")),"live remove");Check(true,"actual WebSocket identity remove preserves health");
 live.Leave("world_unloaded");await Wait(()=>mcLogs.Any(l=>l.Contains("entity received")&&l.Contains("despawn")),"despawn");Check(!tdLogs.Any(l=>l.Contains("Bridge error"))&&!mcLogs.Any(l=>l.Contains("rejected:")),"live mixed path no rejected message");
 File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("TOTAL "+checks.Count+" passed");
}finally{foreach(var p in children){try{if(!p.HasExited)p.Kill(true);p.WaitForExit();p.Dispose();}catch{}}File.WriteAllLines(Path.Combine(output,"bridge.log"),bridgeLogs);File.WriteAllLines(Path.Combine(output,"minecraft.log"),mcLogs);File.WriteAllLines(Path.Combine(output,"7dtd-client.log"),tdLogs);}
