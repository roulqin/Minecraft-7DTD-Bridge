using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace MC7DTD.RuntimeTests
{
 public sealed partial class RuntimeRunner
 {
  sealed class BrokenRenderer:IEquipmentVisualRenderer {public string Id=>"7dtd_renderer";public EquipmentVisual Create(string model){throw new Exception("fixture failure");}}
  IEnumerator AttachmentRuntime(string root)
  {
   AvatarRenderer renderer=null;
   try {
    var resolver=new EquipmentStyleResolver(root);
    Check(resolver.ConfigStatus=="loaded"&&resolver.Resolve("7dtd:iron_axe").Style=="realistic"&&resolver.Resolve("minecraft:diamond_sword").Style=="blocky","Style Resolver");
    renderer=new AvatarRenderer(root,s=>Log.Out(s),AvatarMotionSettings.Disabled);var h=renderer.CreateAvatar("attachment-native",0,10000,0);var rt=h.Equipment.Attachment;
    Check(resolver.Sockets.Count==6&&resolver.Sockets.Values.All(s=>s.Resolve(h.Root.transform)!=null),"Six Sockets Resolve");
    h.Equipment.Update(h.RightHand,"7dtd:iron_axe");var axe=rt.Visual;
    Check(axe!=null&&rt.RendererId=="7dtd_renderer"&&axe.name=="RealisticAxe","7DTD Style Renderer");
    Check(axe.transform.parent==h.RightHand&&Vector3.Distance(axe.transform.localPosition,resolver.Sockets["right_hand"].Offset)<.001f,"Socket Attach");
    Check(Quaternion.Angle(axe.transform.localRotation,Quaternion.Euler(resolver.Sockets["right_hand"].Rotation))<.001f&&Math.Abs(axe.transform.localScale.x-.8f)<.001,"Socket Rotation Scale");
    h.Equipment.Update(h.RightHand,"7dtd:iron_axe");Check(rt.Visual==axe,"Repeated Attach Idempotent");
    h.Equipment.Update(h.RightHand,"minecraft:diamond_sword");var sword=rt.Visual;
    Check(sword!=null&&rt.RendererId=="minecraft_renderer"&&sword.name=="BlockySword","Minecraft Style Renderer");
    Check(!axe.activeSelf&&sword!=axe,"Renderer Switch Removes Old");
    Check(!sword.GetComponentsInChildren<Collider>().Any(c=>c.enabled),"Equipment Collision Disabled");
    var local=sword.transform.localPosition;var a=h.Animator;
    foreach(var pair in new[]{new {Name="Idle",Speed=0f},new {Name="Walk",Speed=2f},new {Name="Run",Speed=5f}}){
     a.SetBool("isGrounded",true);a.SetFloat("speed",pair.Speed);for(int i=0;i<20;i++)a.Update(.05f);
     var before=sword.transform.position;a.Update(.12f);
     Check(sword.transform.parent==h.RightHand&&Vector3.Distance(sword.transform.position,h.RightHand.TransformPoint(local))<.001f&&(pair.Speed==0||Vector3.Distance(before,sword.transform.position)>.001),"Animation Follow "+pair.Name);
    }
    a.SetBool("isGrounded",false);a.SetFloat("verticalVelocity",3);a.SetTrigger("jumpStart");for(int i=0;i<15;i++)a.Update(.05f);
    Check(sword.transform.parent==h.RightHand&&Vector3.Distance(sword.transform.position,h.RightHand.TransformPoint(local))<.001,"Animation Follow Jump");
    a.SetFloat("verticalVelocity",-3);for(int i=0;i<15;i++)a.Update(.05f);Check(sword.transform.parent==h.RightHand&&Vector3.Distance(sword.transform.position,h.RightHand.TransformPoint(local))<.001,"Animation Follow Fall");
    var inspect=renderer.Inspect(h)["equipment_attachment"];Check((bool)inspect["attached"]&&(string)inspect["style"]=="blocky"&&(string)inspect["socket"]=="right_hand","Attachment Inspector");
    File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("AVATAR_RUNTIME_EVIDENCE"),"attachment-inspector.json"),renderer.Inspect(h).ToString());
    h.Equipment.Update(h.RightHand,null);Check(!sword.activeSelf&&rt.Visual==null&&!(bool)rt.Inspect()["attached"],"Detach");
    h.Equipment.Update(h.RightHand,"other:unknown");Check(rt.RendererId=="legacy_fallback"&&rt.Visual!=null,"Unknown Item Fallback");
    rt.Register(new BrokenRenderer());h.Equipment.Update(h.RightHand,"7dtd:iron_axe");Check(rt.RendererId=="legacy_fallback"&&rt.Reason=="renderer_or_model_unavailable","Renderer Failure Fallback");
    var missing=new GameObject("MissingBone");rt.Update(missing.transform,"minecraft:diamond_sword");Check(rt.Visual==null&&rt.Reason=="socket_missing","Missing Socket Safe");UnityEngine.Object.Destroy(missing);
    rt.Update(h.Root.transform,"minecraft:diamond_sword");var other=renderer.CreateAvatar("second-style",2,10000,0);other.Equipment.Update(other.RightHand,"7dtd:iron_axe");
    Check(other.Equipment.Attachment.RendererId=="7dtd_renderer"&&rt.RendererId=="minecraft_renderer"&&other.Equipment.Visual!=rt.Visual,"Multi Renderer Coexist");
    foreach(var socket in resolver.Sockets.Values){var attached=new GameObject("SocketFixture");socket.Attach(attached,socket.Resolve(h.Root.transform));Check(attached.transform.parent.name==socket.Bone,"Socket "+socket.Name);UnityEngine.Object.Destroy(attached);}
    var owned=rt.Visual;renderer.RemoveAvatar(h);Check(rt.Visual==null&&!owned.activeSelf,"Attachment Cleanup");renderer.RemoveAvatar(other);
    h=renderer.CreateAvatar("attachment-reconnect",0,10000,0);h.Equipment.Update(h.RightHand,"minecraft:diamond_sword");Check((bool)h.Equipment.Attachment.Inspect()["attached"],"Reconnect Fresh Binding");renderer.RemoveAvatar(h);
    var config=Path.Combine(root,"config/equipment_attachment.json");var original=File.ReadAllText(config);
    try{File.WriteAllText(config,"{\"items\":{},\"items\":{},\"sockets\":{}}");var bad=new EquipmentStyleResolver(root);Check(bad.ConfigStatus=="invalid_fallback"&&bad.Resolve("any:item").Renderer=="legacy_fallback","Invalid Config Fallback");}
    finally{File.WriteAllText(config,original);}
   }catch(Exception ex){results.Add("RUNTIME ERROR "+ex);}
   finally{renderer?.Dispose();}
   yield return null;
  }
 }
}
