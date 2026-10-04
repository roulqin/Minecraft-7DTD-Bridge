using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using MC7DTD;
using MC7DTD.Bridge;

var root = Path.GetFullPath(args[0]); var output = Path.Combine(root,args.Length > 2 ? args[2] : "work/phase3_6_1-test"); Directory.CreateDirectory(output);
var harness = args.Length > 1 ? args[1] : "io.mc7dtd.ClientHarness";
var bridgeAssembly = Environment.GetEnvironmentVariable("MC7DTD_TEST_BRIDGE") ?? Path.Combine(root,"bridge-server/bin/Phase361/net10.0/BridgeServer.dll");
var checks = new List<string>(); var children = new List<Process>();
void Check(bool condition,string name) { if(!condition) throw new Exception(name); checks.Add(name); Console.WriteLine("PASS "+name); }
JsonObject Fixture(string action="spawn") => JsonNode.Parse(File.ReadAllText(Path.Combine(root,"docs/examples/entity_state_v2/7dtd_"+action+".json"))).AsObject();
JsonObject Prepare(JsonObject message, CoordinateMapper mapper=null, string role="7dtd") {
 using var doc=JsonDocument.Parse(message.ToJsonString()); return EntityTransportV2.Prepare(doc.RootElement,role,mapper??new CoordinateMapper(1,0,0,0));
}
void Reject(Action<JsonObject> mutate,string expected,CoordinateMapper mapper=null,string role="7dtd") {
 var message=Fixture(); mutate(message); bool ok=false; try{Prepare(message,mapper,role);}catch(InvalidDataException ex){ok=ex.Message==expected;} Check(ok,"reject "+expected+" #"+checks.Count);
}
JsonObject Serialize(NativeEntityMessage message) {
 using var data=new MemoryStream(); new DataContractJsonSerializer(typeof(NativeEntityMessage),new DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true}).WriteObject(data,message);
 return JsonNode.Parse(data.ToArray()).AsObject();
}
Process Start(string executable,params string[] arguments) {
 var info=new ProcessStartInfo(executable){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
 foreach(var argument in arguments)info.ArgumentList.Add(argument);
 var process=Process.Start(info); children.Add(process); return process;
}
async Task Wait(Func<bool> condition,string name) { for(int i=0;i<150;i++){if(condition())return;await Task.Delay(100);}throw new Exception("Timeout "+name); }
try {
 var mapped=Prepare(Fixture(),new CoordinateMapper(2,100,-10,25));
 Check(mapped["position"]["x"].GetValue<double>()==10 && mapped["position"]["y"].GetValue<double>()==64 && mapped["position"]["z"].GetValue<double>()==20,"inverse scale and offsets");
 Check(mapped["position"]["space"].GetValue<string>()=="minecraft","target coordinate space");
 Check(mapped["origin"].ToJsonString()==Fixture()["origin"].ToJsonString() && mapped["rotation"].ToJsonString()==Fixture()["rotation"].ToJsonString(),"origin and rotation unchanged");
 foreach(var action in new[]{"spawn","update","despawn"}) Check(Prepare(Fixture(action))["lifecycle"]["event"].GetValue<string>()==action,"prepare "+action);
 Reject(n=>n["source"]="minecraft","invalid_entity_source");
 Reject(n=>{},"invalid_entity_source",role:"minecraft");
 Reject(n=>n["authority"]="minecraft","invalid_entity_authority");
 Reject(n=>n["origin"]["game"]="minecraft","invalid_entity_origin");
 Reject(n=>n["origin"]["entity_id"]=Guid.NewGuid().ToString(),"invalid_entity_origin");
 Reject(n=>n["entity_id"]="bad", "invalid_entity_origin");
 Reject(n=>{n["entity_id"]="bad";n["origin"]["entity_id"]="bad";},"invalid_entity_id");
 Reject(n=>n["entity_type"]="minecraft:player","invalid_entity_type");
 Reject(n=>n["position"]["space"]="minecraft","invalid_entity_space");
 Reject(n=>n["sequence"]=0,"invalid_entity_sequence");
 Reject(n=>n["lifecycle"]["event"]="announce","invalid_entity_lifecycle");
 Reject(n=>n["extra"]=true,"invalid_entity_fields");
 Reject(n=>n["rotation"]["yaw"]=360,"invalid_entity_rotation");
 Reject(n=>n["metadata"]["health"]=-1,"invalid_entity_metadata");
 Reject(n=>{},"invalid_entity_scale",new CoordinateMapper(0,0,0,0));
 Reject(n=>n["position"]["x"]=double.MaxValue,"coordinate_mapping_overflow",new CoordinateMapper(0.001,0,0,0));
 var duplicate=Fixture().ToJsonString().Replace("\"version\":2","\"version\":2,\"version\":2");
 bool duplicateRejected=false;try{using var doc=JsonDocument.Parse(duplicate);EntityTransportV2.Prepare(doc.RootElement,"7dtd",new CoordinateMapper(1,0,0,0));}catch(InvalidDataException ex){duplicateRejected=ex.Message=="duplicate_entity_field";}
 Check(duplicateRejected,"duplicate JSON key rejected");
 var registry=new NativeEntityRegistry();
 Check(registry.Apply(Prepare(Fixture("update"))).Code=="entity_stream_unbound","update cannot bind session");
 Check(registry.Apply(Prepare(Fixture())).Forward && registry.Count==1,"v2 spawn registers");
 var duplicateSpawn=Fixture();duplicateSpawn["sequence"]=50;
 Check(registry.Apply(Prepare(duplicateSpawn)).Code=="duplicate_spawn","duplicate spawn ignored");
 Check(registry.Apply(Prepare(Fixture("update"))).Forward,"duplicate spawn did not raise sequence high-water mark");
 Check(registry.Apply(Prepare(Fixture("update"))).Code=="stale_entity_sequence","stale update ignored");
 var other=Fixture("update");other["stream_id"]=Guid.NewGuid().ToString();Check(registry.Apply(Prepare(other)).Code=="entity_stream_mismatch","unexpected stream rejected");
 other=Fixture("update");other["entity_type"]="7dtd:animal";Check(registry.Apply(Prepare(other)).Code=="entity_type_mismatch","type cannot change");
 Check(registry.Apply(Prepare(Fixture("despawn"))).Forward && registry.Count==0,"despawn retires record");
 duplicateSpawn["sequence"]=60;Check(registry.Apply(Prepare(duplicateSpawn)).Code=="retired_entity","retired identity cannot respawn");
 registry.Clear();Check(registry.Apply(Prepare(Fixture())).Forward,"controlled session reset permits fresh spawn");
 var limited=new NativeEntityRegistry(1);limited.Apply(Prepare(Fixture()));limited.Apply(Prepare(Fixture("despawn")));
 other=Fixture();var newId=Guid.NewGuid().ToString();other["entity_id"]=newId;other["origin"]["entity_id"]=newId;
 Check(limited.Apply(Prepare(other)).Code=="entity_registry_full","tombstone counts toward capacity");
 var publications=new List<NativeEntityMessage>();long epoch=1;var native=new object();var now=DateTime.UtcNow;
 var publisher=new NativePlayerPublisher(()=>epoch,(m,e)=>{publications.Add(m);return true;});
 void Tick(DateTime time)=>publisher.Tick(native,"td-test",120,118,65,-90,10,0,time);
 Tick(now);Tick(now.AddMilliseconds(499));Check(publications.Count==1,"only one spawn before 500ms");
 Tick(now.AddMilliseconds(500));Check(publications.Count==2 && publications[1].Lifecycle.Event=="update","500ms update");
 Check(publications[0].Rotation.Yaw==270,"native yaw normalized");
 publisher.Leave("world_unloaded");Check(publications.Count==3 && publications[2].Lifecycle.Event=="despawn","world exit sends despawn");
 foreach(var message in publications){var json=Serialize(message);Prepare(json);File.WriteAllText(Path.Combine(output,"sender_"+message.Lifecycle.Event+".json"),json.ToJsonString());}
 Check(Serialize(publications[2])["position"]==null && Serialize(publications[2])["rotation"]==null && Serialize(publications[2])["metadata"].AsObject().Count==0,"despawn emits null state and empty object metadata");
 Tick(now.AddSeconds(1));Check(publications[3].EntityId!=publications[0].EntityId,"new observation gets fresh identity");
 epoch=2;Tick(now.AddSeconds(2));Check(publications[4].Lifecycle.Event=="spawn" && publications[4].StreamId!=publications[3].StreamId,"reconnect sends fresh stream spawn");
 epoch=0;Tick(now.AddSeconds(3));Check(publications.Count==5,"offline sampling emits nothing");

 var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
 var config=Path.Combine(output,"network.json"); File.WriteAllText(config,JsonSerializer.Serialize(new{host="localhost",port}));
 File.WriteAllText(Path.Combine(output,"coordinate.json"),"{\"scale\":2,\"offsetX\":100,\"offsetY\":-10,\"offsetZ\":25}");
 var bridge=Start("dotnet",bridgeAssembly,config);
 var bridgeLogs=new ConcurrentQueue<string>(); bridge.OutputDataReceived+=(_,e)=>{if(e.Data!=null)bridgeLogs.Enqueue(e.Data);};bridge.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)bridgeLogs.Enqueue(e.Data);};bridge.BeginOutputReadLine();bridge.BeginErrorReadLine();
 await Wait(()=>bridgeLogs.Any(l=>l.Contains("listening")),"bridge ready");
 var gson=Directory.GetFiles(Path.Combine(root,"work/gradle-home/caches"),"gson-2.13.2.jar",SearchOption.AllDirectories).First();
 var cp=Path.Combine(root,"minecraft-mod/build/classes/java/test")+";"+Path.Combine(root,"minecraft-mod/build/classes/java/main")+";"+gson;
 var java=Start(Path.Combine("C:/Program Files/Java/jdk-21.0.12/bin","java.exe"),"-cp",cp,harness,config,"120");
 var javaLogs=new ConcurrentQueue<string>();java.OutputDataReceived+=(_,e)=>{if(e.Data!=null)javaLogs.Enqueue(e.Data);};java.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)javaLogs.Enqueue(e.Data);};java.BeginOutputReadLine();java.BeginErrorReadLine();
 var tdLogs=new ConcurrentQueue<string>();using var client=new BridgeClient(config,tdLogs.Enqueue);client.Start();
 await Wait(()=>javaLogs.Any(l=>l=="Minecraft connected") && tdLogs.Any(l=>l=="7DTD connected") && javaLogs.Any(l=>l.Contains("Hello from 7DTD")),"both connected");
 Check(true,"actual C# sender and Java receiver handshake/test messages");
 var livePublisher=new NativePlayerPublisher(()=>client.NativeEpoch,client.PublishNative);var liveNative=new object();
 livePublisher.Tick(liveNative,"td-test",120,118,65,90,0,0,now);
 await Wait(()=>javaLogs.Any(l=>l.Contains("7DTD entity received:") && l.Contains("spawn")),"live spawn");
 livePublisher.Tick(liveNative,"td-test",122,120,67,100,0,0,now.AddMilliseconds(500));
 await Wait(()=>javaLogs.Any(l=>l.Contains("7DTD entity received:") && l.Contains("update")),"live update");
 livePublisher.Leave("world_unloaded");await Wait(()=>javaLogs.Any(l=>l.Contains("7DTD entity received:") && l.Contains("despawn")),"live despawn");
 var received=javaLogs.Where(l=>l.StartsWith("7DTD entity received: ")).Select(l=>JsonNode.Parse(l.Substring("7DTD entity received: ".Length)).AsObject()).ToArray();
 Check(received.Length==3,"spawn update despawn arrive exactly once");
 Check(received[0]["position"]["x"].GetValue<double>()==10 && received[1]["position"]["x"].GetValue<double>()==11,"real WebSocket inverse mapping");
 Check(!bridgeLogs.Any(l=>l.Contains("Entity state forwarded: minecraft -> 7dtd")),"Minecraft logging receiver does not echo v2");
 livePublisher.Tick(liveNative,"td-test",120,118,65,0,0,0,now.AddSeconds(2));await Wait(()=>javaLogs.Count(l=>l.StartsWith("7DTD entity received:"))==4,"second spawn");
 long before=client.NativeEpoch;bridge.Kill(true);bridge.WaitForExit();
 var restarted=Start("dotnet",bridgeAssembly,config);restarted.OutputDataReceived+=(_,e)=>{if(e.Data!=null)bridgeLogs.Enqueue(e.Data);};restarted.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)bridgeLogs.Enqueue(e.Data);};restarted.BeginOutputReadLine();restarted.BeginErrorReadLine();
 await Wait(()=>client.NativeEpoch>before && javaLogs.Count(l=>l=="Minecraft connected")>=2 && tdLogs.Count(l=>l=="7DTD connected")>=2,"restart reconnection");
 // Allow peer_connected to establish the final receiver-ready snapshot epoch.
 await Task.Delay(300); livePublisher.Tick(liveNative,"td-test",120,118,65,0,0,0,now.AddSeconds(5));
 await Wait(()=>javaLogs.Count(l=>l.StartsWith("7DTD entity received:"))==5,"fresh spawn after restart");
 Check(JsonNode.Parse(javaLogs.Last(l=>l.StartsWith("7DTD entity received:")).Substring("7DTD entity received: ".Length))["lifecycle"]["event"].GetValue<string>()=="spawn","Bridge restart recovers with fresh spawn");
 // Receiver reconnect must also trigger a fresh authoritative snapshot.
 before=client.NativeEpoch; int receiverDisconnected=bridgeLogs.Count(l=>l.Contains("Disconnected: minecraft")); java.Kill(true);java.WaitForExit();
 await Wait(()=>bridgeLogs.Count(l=>l.Contains("Disconnected: minecraft"))>receiverDisconnected,"receiver disconnected");
 var javaAgain=Start(Path.Combine("C:/Program Files/Java/jdk-21.0.12/bin","java.exe"),"-cp",cp,harness,config,"120");
 javaAgain.OutputDataReceived+=(_,e)=>{if(e.Data!=null)javaLogs.Enqueue(e.Data);};javaAgain.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)javaLogs.Enqueue(e.Data);};javaAgain.BeginOutputReadLine();javaAgain.BeginErrorReadLine();
 await Wait(()=>client.NativeEpoch>before && javaLogs.Count(l=>l=="Minecraft connected")>=3,"receiver reconnect epoch");
 livePublisher.Tick(liveNative,"td-test",120,118,65,0,0,0,now.AddSeconds(8));
 await Wait(()=>javaLogs.Count(l=>l.StartsWith("7DTD entity received:"))==6,"spawn after receiver reconnect");
 Check(true,"Minecraft reconnect starts fresh snapshot without echo");
 // Validate a forged owner over the real WebSocket endpoint, not only in unit tests.
 int disconnected=bridgeLogs.Count(l=>l.Contains("Disconnected: 7dtd"));client.Dispose();
 await Wait(()=>bridgeLogs.Count(l=>l.Contains("Disconnected: 7dtd"))>disconnected,"sender disconnected");
 using(var forgedSocket=new ClientWebSocket()) {
   forgedSocket.Options.Proxy=null;await forgedSocket.ConnectAsync(new Uri("ws://localhost:"+port+"/ws"),CancellationToken.None);
   async Task SendJson(JsonObject node) { using var timeout=new CancellationTokenSource(5000);await forgedSocket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(node)),WebSocketMessageType.Text,true,timeout.Token); }
   async Task<JsonObject> ReceiveJson() { using var timeout=new CancellationTokenSource(5000);using var data=new MemoryStream();var bytes=new byte[2048];WebSocketReceiveResult frame;do{frame=await forgedSocket.ReceiveAsync(new ArraySegment<byte>(bytes),timeout.Token);data.Write(bytes,0,frame.Count);}while(!frame.EndOfMessage);return JsonNode.Parse(data.ToArray()).AsObject(); }
   await SendJson(new JsonObject{["type"]="7dtd_connect",["client"]="7dtd"});await ReceiveJson();await ReceiveJson();
   var forged=Fixture();forged["authority"]="minecraft";await SendJson(forged);
   var rejected=await ReceiveJson();while(rejected["type"].GetValue<string>()!="error") rejected=await ReceiveJson();Check(rejected["type"].GetValue<string>()=="error" && rejected["code"].GetValue<string>()=="invalid_entity_authority","real endpoint rejects forged authority");
 }
 if (harness == "io.mc7dtd.NativeProxyReceiverHarness") {
   Check(javaLogs.Any(l=>l.StartsWith("Harness proxy created:")), "actual Java projection receives spawn");
   Check(javaLogs.Any(l=>l.StartsWith("Harness proxy updated:")), "actual Java projection receives update");
   Check(javaLogs.Any(l=>l.StartsWith("Harness proxy deleted:")), "actual Java projection removes handle");
   Check(javaLogs.Any(l=>l.Contains("7DTD proxy despawned:") && l.Contains("count=0")), "projection lifecycle ends via despawn");
 }
 File.WriteAllLines(Path.Combine(output,"bridge.log"),bridgeLogs);File.WriteAllLines(Path.Combine(output,"minecraft.log"),javaLogs);File.WriteAllLines(Path.Combine(output,"7dtd-client.log"),tdLogs);
 File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed=checks.Count,checks},new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine("TOTAL "+checks.Count+" passed");
} finally { foreach(var child in children){try{if(!child.HasExited)child.Kill(true);child.WaitForExit();child.Dispose();}catch{}} }
