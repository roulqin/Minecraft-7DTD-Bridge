using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MC7DTD.RuntimeTests
{
 public sealed class RuntimeSmoke : IModApi
 {
  public void InitMod(Mod mod) { var host=new GameObject("AvatarRuntimeTestOnly");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<RuntimeRunner>(); }
 }
 public sealed partial class RuntimeRunner : MonoBehaviour
 {
  readonly List<string> results=new List<string>();
  void Check(bool ok,string name) { results.Add(name+" "+(ok?"PASS":"FAIL"));Log.Out("[EquipmentExpansionSmoke] "+results.Last());if(!ok)throw new Exception(name); }
  static WireMessage Message(string action) => new WireMessage {Type="entity_state",Version=1,Source="minecraft",EntityId="3b7424bc-4917-4f19-8f46-dd2882f09825",EntityType="minecraft:player",WorldId="avatar-fixture",Dimension="minecraft:overworld",StreamId="avatar-fixture-stream",Lifecycle=new EntityLifecycle{Event=action},Position=action=="despawn"?null:new EntityPosition{X=10,Y=10000,Z=20,Space="7dtd"},Rotation=action=="despawn"?null:new EntityRotation{Yaw=0,Pitch=0,Roll=0},Components=new PresentationComponents{Presentation=PresentationComponent.Default()}};
  IEnumerator Start()
  {
   yield return new WaitForSecondsRealtime(15);
   var root=Environment.GetEnvironmentVariable("AVATAR_RUNTIME_FIXTURE");
   var evidence=Environment.GetEnvironmentVariable("AVATAR_RUNTIME_EVIDENCE");
   var renderer=new AvatarRenderer(root,s=>Log.Out("[EquipmentExpansionSmoke] "+s),AvatarMotionSettings.Disabled);
   var controller=new MarkerController(new AvatarProxyScene(renderer),s=>Log.Out("[EquipmentExpansionSmoke] "+s),"minecraft:player","Player proxy");
   try {
    var spawn=Message("spawn");var facts=spawn.Components.Presentation.ToString();controller.Enqueue(spawn);controller.Tick(true);
    var h=renderer.Active.Single();Check(h.Root!=null&&h.Status=="active","Spawn Avatar");
    Check((string)renderer.Inspect(h)["entity_id"]==spawn.EntityId,"Entity ID Preserved");
    Check(h.Texture!=null&&h.Texture.width==64&&h.Texture.filterMode==FilterMode.Point,"Load PNG");
    var renderers=h.Root.GetComponentsInChildren<SkinnedMeshRenderer>();
    Check(renderers.Length==12&&renderers.Count(r=>r.name=="Layer2")==6&&renderers.All(r=>r.sharedMaterial.mainTexture==h.Texture),"Layer2");
    MotionChecks(root);PolishChecks(h, evidence);
    var update=Message("update");update.Position.X=15;update.Position.Z=25;update.Rotation.Yaw=90;update.Rotation.Pitch=35;update.Rotation.Roll=20;
    controller.Enqueue(update);controller.Tick(true);
    Check(renderer.Active.Single()==h&&controller.Count==1,"Update Avatar");
    Check(Vector3.Distance(h.Root.transform.position,new Vector3(15,10000,25)-Origin.position)<.001f,"Position Follow");
    Check(Quaternion.Angle(h.Root.transform.rotation,Quaternion.Euler(0,-90,0))<.01f,"Yaw Follow");
    h.Animator.Update(.2f);h.Pose.Apply();
    Check(Quaternion.Angle(h.Head.localRotation,Quaternion.Euler(35,0,0))<.01f&&Mathf.Abs(h.Root.transform.eulerAngles.x)<.01f,"Pitch Head Only");
    Check(IsAnimation(h.Animator,"Idle")&&!h.Animator.applyRootMotion,"Teleport Idle Root Motion Disabled");
    h.Equipment.Update(h.RightHand,"7dtd:woodenClub");var old=h.Equipment.Visual;
    Check(old!=null&&old.transform.parent==h.RightHand&&h.RightHand.name=="rig_hand_right","Right Hand Anchor");
    Check(!old.GetComponentInChildren<Collider>().enabled,"Equipment No Collision");
    h.Equipment.Update(h.RightHand,"7dtd:torch");Check(!old.activeSelf&&h.Equipment.Item=="7dtd:torch","Local Equipment Replace");
    h.Equipment.Update(h.RightHand,null);Check(h.Equipment.Visual==null,"Local Equipment Remove");
    h.Equipment.Update(h.RightHand,"unknown:item");Check(h.Equipment.Visual!=null && h.Equipment.Attachment.RendererId=="legacy_fallback","Unknown Local Item Safe");
    Check(spawn.Components.Presentation.ToString()==facts&&update.Components.Presentation.ToString()==facts,"Presentation Unchanged");
    controller.Enqueue(Message("despawn"));controller.Tick(true);Check(renderer.Active.Length==0&&h.Removed&&h.Materials.Count==0&&h.Texture==null,"Remove Avatar");
    renderer.RemoveAvatar(h);Check(renderer.Active.Length==0,"Repeated Remove Safe");
    controller.Enqueue(Message("spawn"));controller.Tick(true);Check(renderer.Active.Single().Root!=null,"Respawn Resources Reload");
    h=renderer.Active.Single();h.Equipment.Update(h.RightHand,"7dtd:ironAxe");controller.Reset();controller.Tick(true);
    Check(renderer.Active.Length==0&&h.Equipment.Visual==null,"Disconnect Cleanup");
    controller.Enqueue(Message("spawn"));controller.Tick(true);controller.Tick(false);Check(renderer.Active.Length==0,"World Change Cleanup");
    controller.Enqueue(Message("spawn"));controller.Tick(false);Check(renderer.Active.Length==0,"No World No Avatar");
    var config=Path.Combine(root,"assets/avatar/avatar_config.json");var original=File.ReadAllText(config);
    File.WriteAllText(config,original.Replace("skins/player_default.png","skins/missing.png"));
    h=renderer.CreateAvatar("skin-fallback",0,10000,0);Check(h.Root!=null&&h.Status=="fallback"&&h.Reason=="skin_missing_or_invalid"&&h.Texture==null,"Missing Skin Fallback");renderer.RemoveAvatar(h);File.WriteAllText(config,original);
    var other=renderer.CreateAvatar("shared-a",0,10000,0);h=renderer.CreateAvatar("shared-b",0,10000,0);renderer.RemoveAvatar(other);
    Check(h.Root!=null&&h.Root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh!=null,"Shared Resource Lifetime");renderer.RemoveAvatar(h);
    var bundle=Path.Combine(root,"assets/avatar/bundles/windows/minecraft_avatar_v1");File.Move(bundle,bundle+".missing");
    h=renderer.CreateAvatar("resource-fallback",0,10000,0);Check(h.Fallback!=null&&h.Status=="fallback","Resource Missing Fallback");renderer.RemoveAvatar(h);File.Move(bundle+".missing",bundle);
    Check(renderer.Active.Length==0,"All Renderer Cache Cleanup");
   }catch(Exception ex){results.Add("RUNTIME ERROR "+ex);Log.Error("[EquipmentExpansionSmoke] "+ex);}
   finally { renderer.Dispose();Directory.CreateDirectory(evidence);File.WriteAllLines(Path.Combine(evidence,"7dtd-runtime-results.txt"),results); }
   var lateRenderer=new AvatarRenderer(root,s=>Log.Out(s),AvatarMotionSettings.Disabled);
   var late=lateRenderer.CreateAvatar("late-pose",0,10000,0);var pendingRoot=late.Root;
   lateRenderer.UpdateAvatar(late,0,10000,0,70,-40);
   yield return null;
   yield return new WaitForSecondsRealtime(.5f);
   lateRenderer.UpdateAvatar(late,1,10000,0,70,-40);
   yield return new WaitForSecondsRealtime(.15f);
   Check(late.Animation.State=="walking"&&IsAnimation(late.Animator,"Walk"),"Transform Drives Native Walk");
   yield return new WaitForSecondsRealtime(.35f);
   lateRenderer.UpdateAvatar(late,4,10000,0,70,-40);
   yield return new WaitForSecondsRealtime(.15f);
   Check(late.Animation.State=="running"&&IsAnimation(late.Animator,"Run"),"Transform Drives Native Run");
   yield return new WaitForSecondsRealtime(.9f);
   Check(late.Animation.State=="idle"&&IsAnimation(late.Animator,"Idle"),"Stopped Transform Drives Native Idle");
   var diagnostic=lateRenderer.Inspect(late)["avatar_renderer"];
   Check((bool)diagnostic["render_objects_active"]&&(string)diagnostic["material"]=="cutout"&&(string)diagnostic["animation"]=="idle"&&(string)diagnostic["animator"]=="idle","Inspector Confirms Runtime Objects");
   try {
    Check(Quaternion.Angle(late.Head.localRotation,Quaternion.Euler(-40,0,0))<.01f,"LateUpdate Pitch After Animator");
    var rejected=System.Threading.Tasks.Task.Run(()=>{try{lateRenderer.CreateAvatar("worker",0,0,0);return false;}catch(InvalidOperationException){return true;}}).GetAwaiter().GetResult();
    Check(rejected,"Worker Thread Rejected");
    var debug=Path.Combine(root,"debug-test.json");File.WriteAllText(debug,"{\"debug_navigation\":false}");Check(!AvatarLocalHeldCommand.NavigationEnabled(debug),"Local Fixture Debug Disabled");
    File.WriteAllText(debug,"{\"debug_navigation\":true}");Check(AvatarLocalHeldCommand.NavigationEnabled(debug),"Local Fixture Debug Enabled");
    File.WriteAllText(debug,"{\"debug_navigation\":false,\"debug_navigation\":true}");Check(!AvatarLocalHeldCommand.NavigationEnabled(debug),"Local Fixture Invalid Debug Rejected");
   }catch(Exception ex){results.Add("RUNTIME ERROR "+ex);}
   finally{lateRenderer.Dispose();}
   yield return null;
   try{Check(pendingRoot==null,"Unity Deferred Object Destroy");}catch(Exception ex){results.Add("RUNTIME ERROR "+ex);}
   File.WriteAllLines(Path.Combine(evidence,"7dtd-runtime-results.txt"),results);yield return MotionRuntime(root);
   File.WriteAllLines(Path.Combine(evidence,"7dtd-runtime-results.txt"),results);yield return ActionRuntime(root);File.WriteAllLines(Path.Combine(evidence,"7dtd-runtime-results.txt"),results);yield return RefinementRuntime(root);File.WriteAllLines(Path.Combine(evidence,"7dtd-runtime-results.txt"),results);yield return AttachmentRuntime(root);File.WriteAllLines(Path.Combine(evidence,"7dtd-runtime-results.txt"),results);yield return ExpansionRuntime(root);File.WriteAllLines(Path.Combine(evidence,"7dtd-runtime-results.txt"),results);Application.Quit();
  }
 }
}
