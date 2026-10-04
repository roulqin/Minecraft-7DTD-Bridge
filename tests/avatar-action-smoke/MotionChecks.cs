using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MC7DTD.RuntimeTests
{
 public sealed partial class RuntimeRunner
 {
  sealed class GroundFixture : IAvatarGroundProbe
  {
   public bool Hit=true; public float Height=10;public int Calls;
   public bool TryHeight(Vector3 p,AvatarMotionSettings s,out float y){Calls++;y=Height;return Hit;}
  }
  void MotionChecks(string root)
  {
   var settings=new AvatarMotionSettings {Delay=.5,GroundAlignment=false};
   var motion=new AvatarMotionController(settings);motion.Receive(Vector3.zero,0,0,0);motion.Evaluate(0);
   motion.Receive(new Vector3(10,0,0),0,0,.5);motion.Evaluate(.75);
   Check(Math.Abs(motion.Position.x-5)<.001,"Position Lerp");
   var wrap=new AvatarMotionController(settings);wrap.Receive(Vector3.zero,359,0,0);wrap.Evaluate(0);wrap.Receive(Vector3.zero,1,0,.5);wrap.Evaluate(.75);
   Check(Math.Abs(Mathf.DeltaAngle(wrap.ViewYaw,0))<.01,"Rotation Slerp Shortest Arc");
   motion.Receive(new Vector3(50,0,0),90,30,.8);motion.Evaluate(.8);
   Check(motion.Position.x==50&&motion.Status=="snap_teleport","Teleport Bypass");
   Check(motion.HorizontalSpeed==0&&motion.VerticalSpeed==0,"Teleport Suppresses Velocity");
   var hold=new AvatarMotionController(settings);hold.Receive(Vector3.zero,0,0,0);hold.Evaluate(0);hold.Receive(new Vector3(5,0,0),0,0,.5);
   for(double t=.01;t<2;t+=.01)hold.Evaluate(t);
   Check(Math.Abs(hold.Position.x-5)<.001&&hold.Status=="holding","Missing Sample Holds Position");
   var state=new AvatarAnimationState();state.SetVelocity(hold.HorizontalSpeed,hold.VerticalSpeed,2);
   Check(state.State=="idle","Missing Sample Stops Animation");
   for(double t=2;t<5;t+=.05)hold.Evaluate(t);
   hold.Receive(new Vector3(6,0,0),0,0,5);hold.Evaluate(5);
   Check(hold.Position.x<6&&hold.Position.x>=5,"Delayed Small Update Recovers Smoothly");
   var capacity=new AvatarMotionController(settings);for(int i=0;i<100;i++)capacity.Receive(new Vector3(i*.01f,0,0),0,0,i*.5);
   Check(capacity.SampleCount<=32&&capacity.SampleCount>1,"Interpolation Buffer Bounded");
   int count=capacity.SampleCount;capacity.Receive(new Vector3(900,0,0),0,0,-1);
   Check(capacity.SampleCount==count,"Old Local Timestamp Ignored");
   var combined=new AvatarMotionController(settings);combined.Receive(Vector3.zero,0,0,0);combined.Receive(new Vector3(1,0,0),30,20,.01);combined.Evaluate(.01);
   Check(combined.SampleCount==1&&combined.Position.x==1&&Math.Abs(combined.ViewYaw-30)<.001,"Same Frame Pose Coalesced");
   var look=new AvatarMotionController(new AvatarMotionSettings{Delay=.1});look.Receive(Vector3.zero,0,0,0);look.Evaluate(0);look.Receive(Vector3.zero,30,0,.5);look.Evaluate(.6);
   float headAngle=Quaternion.Angle(Quaternion.identity,look.HeadRotation);
   Check(headAngle>0&&headAngle<30,"Head Smooth");
   Check(Math.Abs(look.BodyYaw)<.001,"Small Head Turn Keeps Body");
   look.Receive(Vector3.zero,90,20,1);look.Evaluate(1.1);
   Check(look.BodyYaw>0&&look.BodyYaw<90,"Body Follow");
   look.Receive(Vector3.zero,270,20,1.2);look.Evaluate(1.3);
   Check(Math.Abs(look.HeadYaw)<=75,"Head Yaw Limit");
   state.SetVelocity(0,0,0);Check(state.State=="idle","Motion Idle");
   state.SetVelocity(3,0,0);Check(state.State=="walking"&&state.AnimatorSpeed<=1,"Motion Walk");
   state.SetVelocity(6,0,0);Check(state.State=="running"&&state.AnimatorSpeed>1,"Motion Run");
   state.SetVelocity(10,0,0);Check(state.State=="sprinting"&&state.AnimatorSpeed>1,"Motion Sprinting Derived");
   state.SetVelocity(3,4,0);Check(state.State=="jumping"&&state.Airborne,"Motion Jumping Derived");
   state.SetVelocity(3,-4,0);Check(state.State=="falling"&&state.Airborne,"Motion Falling Derived");
   state.SetVelocity(2,0,0);float slow=state.PlaybackSpeed;state.SetVelocity(4,0,0);
   Check(state.PlaybackSpeed>slow&&state.State=="walking","Animator Playback Matches Speed");
   var groundSettings=new AvatarMotionSettings();var probe=new GroundFixture();var ground=new AvatarGroundAlignment(groundSettings,probe);
   var aligned=ground.Apply(new Vector3(0,65,0),65,false,0);
   Check(Math.Abs(aligned.y-10)<.001&&ground.Offset==-55,"Ground Hit");
   var noGround=new AvatarGroundAlignment(groundSettings,new GroundFixture{Hit=false});
   aligned=noGround.Apply(new Vector3(0,65,0),65,false,0);
   Check(aligned.y==65&&noGround.Offset==0&&noGround.Status=="no_ground","No Ground Fallback");
   var disabledProbe=new GroundFixture();var disabled=new AvatarGroundAlignment(AvatarMotionSettings.Disabled,disabledProbe);
   Check(disabled.Apply(new Vector3(0,65,0),65,false,0).y==65&&disabledProbe.Calls==0,"Ground Disable Bypasses Probe");
   var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.position=new Vector3(9000,9000,9000);cube.transform.localScale=new Vector3(4,1,4);Physics.SyncTransforms();
   try {
    var nativeProbe=new UnityAvatarGroundProbe();float height;
    Check(nativeProbe.TryHeight(new Vector3(9000,9003,9000),groundSettings,out height)&&Math.Abs(height-9000.5)<.01,"Native Raycast Ground Hit");
    Check(!nativeProbe.TryHeight(new Vector3(9500,9003,9500),groundSettings,out height),"Native Raycast No Ground");
   }finally{cube.GetComponent<Collider>().enabled=false;UnityEngine.Object.Destroy(cube);}
   var config=AvatarMotionSettings.Load(root,s=>{});Check(config.Enabled&&Math.Abs(config.Delay-.55)<.001,"Motion Config Load");
   var path=Path.Combine(root,"config/avatar_motion.json");Directory.CreateDirectory(Path.GetDirectoryName(path));string original=File.Exists(path)?File.ReadAllText(path):null;
   try{File.WriteAllText(path,"{\"interpolation_delay\":-99}");config=AvatarMotionSettings.Load(root,s=>{});Check(config.Enabled&&config.Delay==.55,"Invalid Motion Config Defaults");}
   finally{if(original==null)File.Delete(path);else File.WriteAllText(path,original);}
   probe.Height=11;aligned=ground.Apply(new Vector3(2,65,0),65,false,.1);
   Check(aligned.y>10&&aligned.y<11,"Slope Ground Offset Smooth");
   var air=new AvatarGroundAlignment(groundSettings,new GroundFixture());air.Apply(new Vector3(0,65,0),65,false,0);
   Check(Math.Abs(air.Apply(new Vector3(0,66,0),66,true,.1).y-11)<.001,"Ground Alignment Preserves Airborne Lift");
  }
  void RuntimeMotionCheck(bool ok,string name)
  { results.Add(name+" "+(ok?"PASS":"FAIL"));Log.Out("[AvatarActionSmoke] "+results[results.Count-1]); }
  IEnumerator MotionRuntime(string root)
  {
   var renderer=new AvatarRenderer(root,s=>Log.Out(s),new AvatarMotionSettings{GroundAlignment=false});
   var h=renderer.CreateAvatar("polished-runtime",0,10000,0);
   yield return new WaitForSecondsRealtime(.5f);
   renderer.UpdateAvatar(h,1,10000,0,60,35);
   yield return new WaitForSecondsRealtime(.3f);
   float shown=h.Root.transform.position.x+Origin.position.x;
   RuntimeMotionCheck(shown>0&&shown<1,"Runtime Interpolated Position");
   RuntimeMotionCheck(Mathf.Abs(Mathf.DeltaAngle(h.Root.transform.eulerAngles.x,0))<.01&&Quaternion.Angle(h.Head.localRotation,Quaternion.identity)>1,"Runtime Head Rotation Body Upright");
   renderer.UpdateAvatar(h,40,10000,0,90,0);
   yield return null;
   RuntimeMotionCheck(Math.Abs(h.Root.transform.position.x+Origin.position.x-40)<.01,"Runtime Teleport Immediate");
   renderer.RemoveAvatar(h);
   RuntimeMotionCheck(h.Motion.SampleCount==0&&h.Ground.Offset==0&&h.Animation.State=="idle","Runtime Remove Clears Motion");
   h=renderer.CreateAvatar("reconnected-runtime",3,10000,4);
   RuntimeMotionCheck(h.Motion.SampleCount==1&&h.Animation.State=="idle"&&h.Root!=null,"Runtime Reconnect Fresh Motion");
   var scene=new AvatarProxyScene(renderer);var snapshot=scene.Create("snapshot-yaw",3,10000,4);scene.Rotate(snapshot,120,25,0);
   var initial=Array.Find(renderer.Active,a=>a.Name=="snapshot-yaw");
   RuntimeMotionCheck(Quaternion.Angle(initial.Root.transform.rotation,Quaternion.Euler(0,-120,0))<.01&&Quaternion.Angle(initial.Head.localRotation,Quaternion.Euler(25,0,0))<.01,"Runtime Initial Snapshot Full Rotation");scene.Delete(snapshot);
   renderer.Dispose();
   var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.position=new Vector3(9000,9000,9000)-Origin.position;cube.transform.localScale=new Vector3(4,1,4);Physics.SyncTransforms();
   var grounded=new AvatarRenderer(root,s=>Log.Out(s),new AvatarMotionSettings());
   var actor=grounded.CreateAvatar("grounded-runtime",9000,9003,9000);
   RuntimeMotionCheck(actor.Y==9003&&Math.Abs(actor.Root.transform.position.y+Origin.position.y-9000.5)<.01&&actor.Ground.Offset<0,"Runtime Ground Visual Only Facts Preserved");
   grounded.Dispose();cube.GetComponent<Collider>().enabled=false;UnityEngine.Object.Destroy(cube);
   yield return null;
  }
 }
}
