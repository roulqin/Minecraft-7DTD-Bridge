using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace MC7DTD.RuntimeTests
{
 public sealed partial class RuntimeRunner
 {
  IEnumerator ActionRuntime(string root)
  {
   AvatarRenderer renderer=null;
   try {
    var a=new AvatarActionState();
    a.Observe(0,0,true,1);Check(a.State=="idle","Idle Detect");
    a.Observe(2,0,true,2);Check(a.State=="walking","Walk Detect");
    a.Observe(8,0,true,3);Check(a.State=="running","Run Detect");
    a.Observe(2,3,false,4);Check(a.State=="jumping"&&!a.Grounded,"Jump Detect");
    a.Observe(2,-3,false,5);Check(a.State=="falling","Fall Detect");
    a.Observe(2,0,true,6);Check(a.State=="walking","Landing Walk Detect");
    a.Observe(0,0,true,7,true);Check(a.State=="sneaking"&&a.SneakingAvailable,"Optional Sneaking Interface");
    a.Observe(0,0,true,8);Check(!a.SneakingAvailable&&a.State=="idle","Missing Sneak Safe");
    a.Observe(double.NaN,0,true,9);Check(a.Timestamp==8,"Invalid Action Input Ignored");
    a.Observe(8,0,true,1);Check(a.State=="idle","Old Action Time Ignored");
    a.Reset();a.ObserveTransform(0,0,65,true,0,false);a.ObserveTransform(0,3,66,true,.5,false);
    Check(a.State=="jumping","Transform Jump Detect");
    a.ObserveTransform(0,0,66,true,.6,false);Check(!a.Grounded,"Apex Remains Airborne");
    a.ObserveTransform(0,-3,65,true,1,false);a.ObserveTransform(0,0,65,true,1.1,false);Check(a.Grounded&&a.State=="idle","Transform Landing Detect");
    a.ObserveTransform(0,10,100,true,2,true);Check(a.Grounded&&a.State=="idle","Teleport Not Jump");
    a.ObserveTransform(0,0,100,false,3,false);Check(a.State=="falling"&&!a.Grounded,"No Ground Not Idle");
    a.Reset();Check(a.State=="idle"&&a.Timestamp==0&&a.Speed==0,"Action Remove Cleanup");
    renderer=new AvatarRenderer(root,s=>Log.Out(s),new AvatarMotionSettings());
    var h=renderer.CreateAvatar("action-native",0,10000,0);
    Check(h.Root!=null&&h.Action.Timestamp>=0,"Action Spawn");
    foreach(var test in new[]{Tuple.Create(0d,0d,true,"Idle"),Tuple.Create(2d,0d,true,"Walk"),Tuple.Create(8d,0d,true,"Run"),Tuple.Create(0d,3d,false,"Jump"),Tuple.Create(0d,-3d,false,"Fall")}){
     h.Animation.SetVelocity(test.Item1,test.Item2,Time.realtimeSinceStartup);
     h.Action.Observe(test.Item1,test.Item2,test.Item3,Time.realtimeSinceStartup);
     h.Pose.UpdateAnimation();for(int i=0;i<30;i++)h.Animator.Update(.05f);
     Check(IsAnimation(h.Animator,test.Item4),test.Item4+" Action Transition");
     System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetEnvironmentVariable("AVATAR_RUNTIME_EVIDENCE"),"action-inspector-"+test.Item4.ToLowerInvariant()+".json"),renderer.Inspect(h).ToString());
    }
    var info=renderer.Inspect(h)["avatar_action"];
    Check((string)info["state"]=="falling"&&!(bool)info["grounded"]&&(string)info["animation"]=="fall"&&h.X==0&&h.Y==10000,"Action Inspector Facts Unchanged");
    h.Animation.SetVelocity(2,0,Time.realtimeSinceStartup);h.Action.Observe(2,0,true,Time.realtimeSinceStartup);h.Pose.UpdateAnimation();
    for(int i=0;i<30;i++)h.Animator.Update(.05f);
    Check(IsAnimation(h.Animator,"Walk"),"Native Landing Walk Transition");
    renderer.RemoveAvatar(h);Check(h.Action.Timestamp==0&&h.Action.Speed==0&&h.Action.State=="idle"&&h.Removed,"Native Action Remove Cleanup");
   }catch(Exception ex){results.Add("ACTION ERROR "+ex);}
   finally{renderer?.Dispose();}
   yield return null;
  }
 }
}
