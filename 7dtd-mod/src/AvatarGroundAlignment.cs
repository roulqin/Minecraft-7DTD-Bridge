using System;
using System.Linq;
using UnityEngine;

namespace MC7DTD
{
 public interface IAvatarGroundProbe { bool TryHeight(Vector3 position,AvatarMotionSettings settings,out float height); }
 public sealed class UnityAvatarGroundProbe : IAvatarGroundProbe
 {
  public bool TryHeight(Vector3 position,AvatarMotionSettings settings,out float height)
  {
   height=0;
   var hits=Physics.RaycastAll(position+Vector3.up*settings.ProbeHeight,Vector3.down,settings.ProbeDistance,~0,QueryTriggerInteraction.Ignore);
   foreach(var hit in hits.OrderBy(h=>h.distance)){
    if(hit.collider==null||hit.collider.GetComponentInParent<Entity>()!=null)continue;
    height=hit.point.y;return true;
   }
   return false;
  }
 }
 public sealed class AvatarGroundAlignment
 {
  readonly AvatarMotionSettings settings;readonly IAvatarGroundProbe probe;
  double lastFrame=double.NaN,lastProbe=-1000,groundedSourceY;
  bool initialized,hit;
  float ground;
  Vector3 probePosition;
  public float Offset {get;private set;}
  public string Status {get;private set;}="unresolved";
  public AvatarGroundAlignment(AvatarMotionSettings options,IAvatarGroundProbe groundProbe=null){settings=options;probe=groundProbe??new UnityAvatarGroundProbe();}
  public Vector3 Apply(Vector3 localPosition,double sourceY,bool airborne,double now,bool forceSnap=false)
  {
   if(!settings.Enabled||!settings.GroundAlignment){Offset=0;Status="disabled";return localPosition;}
   float dt=double.IsNaN(lastFrame)?0:(float)Math.Max(0,Math.Min(now-lastFrame,.1));lastFrame=now;
   if(!initialized||forceSnap||now-lastProbe>=.1||(localPosition-probePosition).sqrMagnitude>16){lastProbe=now;probePosition=localPosition;hit=probe.TryHeight(localPosition,settings,out ground);}
   if(!initialized||!airborne)groundedSourceY=sourceY;
   float lift=airborne?(float)Math.Max(0,Math.Min(8,sourceY-groundedSourceY)):0;
   float target=hit?ground-localPosition.y+lift:0;
   Offset=!initialized||forceSnap?target:Mathf.Lerp(Offset,target,1-Mathf.Exp(-settings.GroundSharpness*dt));
   initialized=true;Status=hit?"ground_hit":"no_ground";
   return localPosition+Vector3.up*Offset;
  }
  public void Clear(){initialized=false;hit=false;Offset=0;lastFrame=double.NaN;lastProbe=-1000;Status="cleared";}
 }
}
