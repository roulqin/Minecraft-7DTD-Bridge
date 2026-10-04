using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace MC7DTD.RuntimeTests
{
 public sealed partial class RuntimeRunner
 {
  static bool IsAnimation(Animator a,string name){var actual=AvatarAnimatorDriver.State(a);return actual==name.ToLowerInvariant() || name=="Jump"&&(actual=="jump_start"||actual=="jump_loop");}
  static Transform Bone(GameObject root,string name)=>root.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
  static bool ArmsDown(GameObject root){return new[]{"left","right"}.All(s=>Vector3.Dot(Bone(root,"rig_arm_"+s+"_upper").TransformDirection(s=="left"?Vector3.left:Vector3.right),root.transform.up)<-.96f);}
  IEnumerator RefinementRuntime(string root)
  {
   AvatarRenderer renderer=null;
   try {
    renderer=new AvatarRenderer(root,s=>Log.Out(s),AvatarMotionSettings.Disabled);var h=renderer.CreateAvatar("refinement-native",0,10000,0);var a=h.Animator;
    a.SetBool("isGrounded",true);a.SetFloat("speed",0);for(int i=0;i<30;i++)a.Update(.05f);Check(ArmsDown(h.Root),"Idle Pose");
    a.SetFloat("speed",2);for(int i=0;i<20;i++)a.Update(.05f);var arm=Bone(h.Root,"rig_arm_left_upper");var before=arm.localRotation;a.Update(.2f);Check(IsAnimation(a,"Walk")&&Quaternion.Angle(before,arm.localRotation)>1,"Walk Pose");
    a.SetFloat("speed",5);for(int i=0;i<20;i++)a.Update(.05f);before=arm.localRotation;a.Update(.1f);Check(IsAnimation(a,"Run")&&Quaternion.Angle(before,arm.localRotation)>1,"Run Pose");
    a.SetFloat("speed",3.5f);a.Update(.1f);Check(Math.Abs(AvatarAnimatorDriver.Blend(a)-.5)<.01,"Continuous Ground Blend");
    var action=new AvatarActionState();var velocity=new AvatarAnimationState();var driver=new AvatarAnimatorDriver();
    for(int cycle=0;cycle<4;cycle++){
     action.Observe(0,3,false,cycle*3+1);driver.Update(a,action,velocity,true);a.Update(.05f);
     if(cycle==0)Check(a.IsInTransition(0)||a.GetCurrentAnimatorStateInfo(0).IsName("JumpStart"),"Jump Start Transition");
     for(int i=0;i<12;i++){driver.Update(a,action,velocity,true);a.Update(.05f);}
     if(cycle==0)Check(a.GetCurrentAnimatorStateInfo(0).IsName("JumpLoop"),"Jump Loop Transition");
     action.Observe(0,-3,false,cycle*3+2);driver.Update(a,action,velocity,true);for(int i=0;i<15;i++)a.Update(.05f);
     if(cycle==0)Check(a.GetCurrentAnimatorStateInfo(0).IsName("Fall"),"Fall Transition");
     action.Observe(0,0,true,cycle*3+3);driver.Update(a,action,velocity,true);a.Update(.1f);
     if(cycle==0)Check(a.IsInTransition(0)||a.GetCurrentAnimatorStateInfo(0).IsName("Land"),"Land Transition");
     for(int i=0;i<20;i++)a.Update(.05f);
     Check(a.GetCurrentAnimatorStateInfo(0).IsName("Ground")&&ArmsDown(h.Root),"Land Recovery Cycle "+cycle);
    }
    Check(driver.JumpStarts==4,"Jump Edge Only");Check(ArmsDown(h.Root),"No Arm Residual");
    action.Observe(0,3,false,20);driver.Update(a,action,velocity,true);for(int i=0;i<15;i++)a.Update(.05f);
    Check(ArmsDown(h.Root)&&Math.Abs(Mathf.DeltaAngle(Bone(h.Root,"rig_leg_left_upper").localEulerAngles.x,0))<.1,"Stationary Jump Straight Pose");
    action.Observe(8,3,false,21);driver.Update(a,action,velocity,true);for(int i=0;i<10;i++)a.Update(.05f);
    before=arm.localRotation;a.Update(.12f);Check(AvatarAnimatorDriver.Blend(a)>.99&&Quaternion.Angle(before,arm.localRotation)>1,"Running Jump Keeps Gait");
    action.Observe(0,-3,false,22);driver.Update(a,action,velocity,true);for(int i=0;i<15;i++)a.Update(.05f);Check(ArmsDown(h.Root),"Stationary Fall Straight Pose");
    action.Observe(0,0,true,23);driver.Update(a,action,velocity,true);for(int i=0;i<20;i++)a.Update(.05f);Check(ArmsDown(h.Root),"Moving Jump Landing Recovers");
    Check(a.layerCount==1&&Math.Abs(a.GetLayerWeight(0)-1)<.001,"Layer Reset");
    var debug=renderer.Inspect(h)["avatar_animation_debug"];Check((string)debug["pose"]=="normal"&&(string)debug["animator_state"]=="idle","Refinement Inspector Actual Pose");
    File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("AVATAR_RUNTIME_EVIDENCE"),"refinement-inspector.json"),renderer.Inspect(h).ToString());
    var ownedDriver=h.Pose.Driver;renderer.RemoveAvatar(h);Check(ownedDriver.JumpStarts==0,"Driver Remove Cleanup");
    var path=Path.Combine(root,"config/interpolation_profile.json");Directory.CreateDirectory(Path.GetDirectoryName(path));string original=File.Exists(path)?File.ReadAllText(path):null;
    try{
     File.WriteAllText(path,"{\"mode\":\"smooth\"}");var profile=AvatarMotionSettings.Load(root,s=>{});Check(profile.Profile=="smooth"&&profile.Delay==.5,"Interpolation Config Load");
     File.WriteAllText(path,"{\"mode\":\"normal\"}");profile=AvatarMotionSettings.Load(root,s=>{});Check(profile.Delay==.3,"Normal Profile Switch");
     File.WriteAllText(path,"{\"mode\":\"fast\"}");profile=AvatarMotionSettings.Load(root,s=>{});Check(profile.Delay==.15,"Fast Profile Switch");
     File.WriteAllText(path,"{\"mode\":\"normal\",\"buffer_ms\":350}");Check(AvatarMotionSettings.Load(root,s=>{}).Delay==.35,"Profile Buffer Override");
     File.WriteAllText(path,"{\"mode\":\"bad\"}");Check(AvatarMotionSettings.Load(root,s=>{}).Delay==.55,"Invalid Profile Safe Fallback");
    }finally{if(original==null)File.Delete(path);else File.WriteAllText(path,original);}
   }catch(Exception ex){results.Add("REFINEMENT ERROR "+ex);}
   finally{renderer?.Dispose();}
   yield return null;
  }
 }
}
