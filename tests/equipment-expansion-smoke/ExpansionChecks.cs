using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace MC7DTD.RuntimeTests
{
 public sealed partial class RuntimeRunner
 {
  IEnumerator ExpansionRuntime(string root)
  {
   AvatarRenderer renderer=null;
   try {
    renderer=new AvatarRenderer(root,s=>Log.Out(s),AvatarMotionSettings.Disabled);var h=renderer.CreateAvatar("expansion-native",0,10000,0);var equipment=h.Equipment;
    Check(equipment.InspectSlots().Count==6&&equipment.InspectSlots().Properties().All(p=>p.Value.Type==Newtonsoft.Json.Linq.JTokenType.Null),"Expansion Six Empty Slots");
    var resolver=new EquipmentStyleResolver(root);var provider=new EquipmentRendererProvider();
    foreach(var style in new[]{"minecraft","voxel","realistic"})foreach(var model in new[]{"club","axe","pistol","rifle","torch","pickaxe","shovel","helmet","backpack"}) {
     var visual=provider.Create(style,model);
     Check(visual.Root!=null&&visual.Root.name==style+"-"+model&&visual.Root.GetComponentsInChildren<Renderer>().Length>0&&visual.Materials.All(m=>m.shader!=null)&&(style!="realistic"||visual.Meshes.Count>0),"Expansion Create "+style+" "+model);
     visual.Attach(resolver.Sockets["right_hand"],h.RightHand);var saved=visual.Root;
     visual.Update(new Vector3(.1f,0,.15f),Quaternion.Euler(0,20,0),Vector3.one*.5f);
     Check(saved.transform.parent==h.RightHand&&Math.Abs(saved.transform.localScale.x-.5f)<.001&&Vector3.Distance(saved.transform.localPosition,new Vector3(.1f,0,.15f))<.001,"Expansion Update Attach "+style+" "+model);
     visual.Detach();Check(!saved.activeSelf&&saved.transform.parent==null&&visual.Status=="detached","Expansion Detach "+style+" "+model);
     visual.Attach(resolver.Sockets["right_hand"],h.RightHand);visual.Destroy();
     Check(!saved.activeSelf&&visual.Root==null&&visual.Materials.Count==0&&visual.Meshes.Count==0&&visual.Status=="destroyed","Expansion Destroy "+style+" "+model);
    }
    foreach(var socket in resolver.Sockets.Keys){equipment.SetSlot(socket,"realistic:axe");Check(equipment.Slot(socket).Visual.transform.parent.name==resolver.Sockets[socket].Bone,"Expansion Socket Bind "+socket);}
    Check(equipment.InspectSlots().Properties().All(p=>(bool)p.Value["attached"]),"Expansion Six Concurrent Slots");
    var back=equipment.Slot("back").Visual;var left=equipment.Slot("left_hand").Visual;var previous=equipment.Visual;
    equipment.Update(h.RightHand,"minecraft:pistol");Check(!previous.activeSelf&&equipment.Visual!=previous&&equipment.Slot("back").Visual==back&&equipment.Slot("left_hand").Visual==left,"Expansion Slot Isolation");
    var noCollision=equipment.InspectSlots().Properties().All(p=>equipment.Slot(p.Name).Visual.GetComponentsInChildren<Collider>().All(c=>!c.enabled));Check(noCollision,"Expansion All Slots No Collision");
    bool rejected=false;try{equipment.SetSlot("invalid","voxel:club");}catch(ArgumentException){rejected=true;}Check(rejected&&equipment.Slot("back").Visual==back,"Expansion Invalid Socket Preserves Slots");
    foreach(var name in resolver.Sockets.Keys){var owned=equipment.Slot(name).Visual;equipment.SetSlot(name,null);Check(!owned.activeSelf&&equipment.InspectSlots()[name].Type==Newtonsoft.Json.Linq.JTokenType.Null,"Expansion Slot Remove "+name);}
    Check(!equipment.Holding&&h.EquipmentAnimation!=null,"Expansion Empty Animation Profile");
    var a=h.Animator;var arm=Bone(h.Root,"rig_arm_right_upper");a.SetBool("isGrounded",true);a.SetFloat("speed",0);for(int i=0;i<20;i++)a.Update(.05f);Check(ArmsDown(h.Root),"Expansion Empty Idle Natural");
    equipment.Update(h.RightHand,"voxel:club");a.SetFloat("speed",0);for(int i=0;i<20;i++)a.Update(.05f);Check(ArmsDown(h.Root),"Expansion Held Idle Natural");
    a.SetFloat("speed",2);float maxWalk=0;for(int i=0;i<40;i++){a.Update(.025f);maxWalk=Math.Max(maxWalk,Math.Abs(Mathf.DeltaAngle(arm.localEulerAngles.x,0)));}
    Check(maxWalk>1&&maxWalk<=9,"Expansion Held Walk Pose");
    a.SetFloat("speed",5);float maxRun=0;for(int i=0;i<40;i++){a.Update(.025f);maxRun=Math.Max(maxRun,Math.Abs(Mathf.DeltaAngle(arm.localEulerAngles.x,0)));}
    Check(maxRun>0&&maxRun<=5,"Expansion Held Run Reduced Swing");
    var handOffset=equipment.Visual.transform.localPosition;a.SetBool("isGrounded",false);a.SetFloat("verticalVelocity",3);a.SetTrigger("jumpStart");for(int i=0;i<20;i++)a.Update(.05f);
    Check(Vector3.Distance(equipment.Visual.transform.position,h.RightHand.TransformPoint(handOffset))<.001,"Expansion Held Jump Stable");
    equipment.SetSlot("left_hand","realistic:pistol");equipment.SetSlot("back","realistic:rifle");equipment.SetSlot("head","voxel:helmet");
    Check(equipment.Holding&&(bool)equipment.InspectSlots()["back"]["active"]&&(bool)equipment.InspectSlots()["head"]["active"],"Expansion Dual Held Back Head Coexist");
    equipment.SetSlot("right_hand",null);Check(equipment.Holding,"Expansion Left Hand Keeps Held Profile");equipment.SetSlot("left_hand",null);Check(!equipment.Holding,"Expansion Back Head Do Not Change Arms");
    a.ResetTrigger("jumpStart");a.SetBool("isGrounded",true);a.SetFloat("verticalVelocity",0);a.SetFloat("speed",5);for(int i=0;i<40;i++)a.Update(.05f);
    float restoredSwing=0;for(int i=0;i<40;i++){a.Update(.025f);restoredSwing=Math.Max(restoredSwing,Math.Abs(Mathf.DeltaAngle(arm.localEulerAngles.x,0)));}
    Check(restoredSwing>30,"Expansion Empty Restores Original Run Clip");
    a.SetFloat("speed",0);for(int i=0;i<30;i++)a.Update(.05f);Check(ArmsDown(h.Root),"Expansion Empty Returns Natural Idle");
    File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("AVATAR_RUNTIME_EVIDENCE"),"expansion-inspector.json"),renderer.Inspect(h).ToString());
    var backpack=equipment.Slot("back").Visual;renderer.RemoveAvatar(h);Check(equipment.InspectSlots().Properties().All(p=>p.Value.Type==Newtonsoft.Json.Linq.JTokenType.Null)&&!backpack.activeSelf,"Expansion Despawn All Slots Cleanup");
    h=renderer.CreateAvatar("expansion-respawn",0,10000,0);h.Equipment.SetSlot("back","realistic:backpack");h.Equipment.Update(h.RightHand,"minecraft:pistol");Check(h.Equipment.Slot("back").Visual!=backpack&&h.Equipment.Holding,"Expansion Respawn New Objects");
    renderer.Dispose();Check(h.Equipment.InspectSlots().Properties().All(p=>p.Value.Type==Newtonsoft.Json.Linq.JTokenType.Null),"Expansion Disconnect Cleanup");
   }catch(Exception ex){results.Add("RUNTIME ERROR "+ex);}
   finally{renderer?.Dispose();}
   yield return null;
  }
 }
}
