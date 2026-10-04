using System;
using System.Collections.Generic;
using UnityEngine;

namespace MC7DTD
{
 public sealed class AvatarMotionController
 {
  struct Sample { public Vector3 Position; public float Yaw,Pitch; public double Time; }
  readonly List<Sample> samples=new List<Sample>();
  readonly AvatarMotionSettings settings;
  double lastFrame=double.NaN;
  bool snap;
  public Vector3 Position { get; private set; }
  public float BodyYaw { get; private set; }
  public float ViewYaw { get; private set; }
  public float Pitch { get; private set; }
  public float HeadYaw => Mathf.Clamp(Mathf.DeltaAngle(BodyYaw,ViewYaw),-settings.MaxHeadYaw,settings.MaxHeadYaw);
  public Quaternion HeadRotation { get; private set; } = Quaternion.identity;
  public double HorizontalSpeed { get; private set; }
  public double VerticalSpeed { get; private set; }
  public string Status { get; private set; }="buffering";
  public int SampleCount => samples.Count;
  public void SnapInitialPose(){if(samples.Count>0){snap=true;Status="buffering";}}
  public AvatarMotionController(AvatarMotionSettings options){settings=options;}
  public void Receive(Vector3 position,float yaw,float pitch,double now)
  {
   if(!Finite(position.x)||!Finite(position.y)||!Finite(position.z)||!Finite(yaw)||!Finite(pitch)||!Finite(now))return;
   var next=new Sample{Position=position,Yaw=yaw,Pitch=Mathf.Clamp(pitch,-90,90),Time=now};
   if(samples.Count==0){samples.Add(next);snap=true;Status="buffering";return;}
   var previous=samples[samples.Count-1];
   if(now<previous.Time)return;
   if((position-previous.Position).sqrMagnitude<.000001f&&Mathf.Abs(Mathf.DeltaAngle(previous.Yaw,yaw))<.001f&&Mathf.Abs(previous.Pitch-pitch)<.001f)return;
   if(Vector3.Distance(position,previous.Position)>settings.TeleportDistance){samples.Clear();samples.Add(next);snap=true;Status="snap_teleport";return;}
   // Move and Rotate arrive separately in the same game frame; coalesce that pose.
   if(now-previous.Time<.025){samples[samples.Count-1]=next;return;}
   if(now-previous.Time>settings.RecoveryGap){
    // Hold rather than extrapolate during a gap. Resume from the displayed pose, never fly through it.
    samples.Clear();previous.Position=Position;previous.Yaw=ViewYaw;previous.Pitch=Pitch;
    previous.Time=now-settings.Delay;samples.Add(previous);Status="recovering";
   }
   samples.Add(next);while(samples.Count>32)samples.RemoveAt(0);
  }
  public void Evaluate(double now)
  {
   if(samples.Count==0||!Finite(now))return;
   if(!double.IsNaN(lastFrame)&&now-lastFrame>1){snap=true;Status="resync";}
   double elapsed=double.IsNaN(lastFrame)?0:Math.Max(0,now-lastFrame);
   double dt=Math.Min(elapsed,.1);lastFrame=now;
   var latest=samples[samples.Count-1];
   if(snap){
    Position=latest.Position;BodyYaw=ViewYaw=latest.Yaw;Pitch=latest.Pitch;
    HeadRotation=Quaternion.Euler(Pitch,0,0);HorizontalSpeed=VerticalSpeed=0;snap=false;return;
   }
   double target=now-settings.Delay;
   while(samples.Count>2&&samples[1].Time<=target)samples.RemoveAt(0);
   var a=samples[0];var b=a;
   for(int i=1;i<samples.Count;i++){b=samples[i];if(b.Time>=target)break;a=b;}
   float fraction=b.Time>a.Time?(float)Math.Max(0,Math.Min(1,(target-a.Time)/(b.Time-a.Time))):0;
   var next=Vector3.Lerp(a.Position,b.Position,fraction);
   var yawRotation=Quaternion.Slerp(Quaternion.Euler(0,-a.Yaw,0),Quaternion.Euler(0,-b.Yaw,0),fraction);
   ViewYaw=Mathf.Repeat(-yawRotation.eulerAngles.y,360);Pitch=Mathf.Lerp(a.Pitch,b.Pitch,fraction);
   var delta=next-Position;
   double blend=1-Math.Exp(-12*elapsed);
   double horizontal=elapsed>.001?Math.Sqrt(delta.x*delta.x+delta.z*delta.z)/elapsed:0;
   double vertical=elapsed>.001?delta.y/elapsed:0;
   HorizontalSpeed+=(horizontal-HorizontalSpeed)*blend;VerticalSpeed+=(vertical-VerticalSpeed)*blend;Position=next;
   if(HorizontalSpeed>.1||Mathf.Abs(Mathf.DeltaAngle(BodyYaw,ViewYaw))>settings.BodyThreshold)
    BodyYaw=Mathf.LerpAngle(BodyYaw,ViewYaw,1-Mathf.Exp(-settings.BodySharpness*(float)dt));
   HeadRotation=Quaternion.Slerp(HeadRotation,Quaternion.Euler(Pitch,-HeadYaw,0),1-Mathf.Exp(-settings.HeadSharpness*(float)dt));
   Status=target>latest.Time?"holding":samples.Count>1?"active":"buffering";
  }
  public void Clear(){samples.Clear();lastFrame=double.NaN;HorizontalSpeed=VerticalSpeed=0;Status="cleared";}
  static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
 }
}
