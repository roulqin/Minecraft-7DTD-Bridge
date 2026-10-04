using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace MC7DTD
{
 public sealed class AvatarMotionSettings
 {
  public bool Enabled=true, GroundAlignment=true;
  public double Delay=.55, TeleportDistance=12, RecoveryGap=2;
  public string Profile="legacy";
  public float HeadSharpness=12, BodySharpness=6, BodyThreshold=35, MaxHeadYaw=75;
  public float ProbeHeight=16, ProbeDistance=128, GroundSharpness=10;
  public static AvatarMotionSettings Disabled => new AvatarMotionSettings { Enabled=false, GroundAlignment=false };
  public static AvatarMotionSettings Load(string root,Action<string> log)
  {
   var result=new AvatarMotionSettings();var path=Path.Combine(root,"config","avatar_motion.json");
   if(!File.Exists(path))return LoadProfile(root,result,log);
   try {
    if(new FileInfo(path).Length>4096)throw new InvalidDataException();
    var j=JObject.Parse(File.ReadAllText(path),new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
    if(j["enabled"]!=null){if(j["enabled"].Type!=JTokenType.Boolean)throw new InvalidDataException();result.Enabled=(bool)j["enabled"];}
    if(j["ground_alignment"]!=null){if(j["ground_alignment"].Type!=JTokenType.Boolean)throw new InvalidDataException();result.GroundAlignment=(bool)j["ground_alignment"];}
    result.Delay=Number(j,"interpolation_delay",.55,.1,1);
    result.TeleportDistance=Number(j,"teleport_distance",12,2,100);
    result.RecoveryGap=Number(j,"recovery_gap",2,1,10);
    result.HeadSharpness=(float)Number(j,"head_sharpness",12,1,40);
    result.BodySharpness=(float)Number(j,"body_sharpness",6,1,30);
    result.BodyThreshold=(float)Number(j,"body_threshold",35,5,60);
    result.MaxHeadYaw=(float)Number(j,"max_head_yaw",75,60,90);
    result.ProbeHeight=(float)Number(j,"ground_probe_height",16,2,64);
    result.ProbeDistance=(float)Number(j,"ground_probe_distance",128,16,256);
    result.GroundSharpness=(float)Number(j,"ground_sharpness",10,1,30);
    return LoadProfile(root,result,log);
   }catch(Exception){log?.Invoke("Avatar motion config invalid; using local defaults");return new AvatarMotionSettings();}
  }
  static AvatarMotionSettings LoadProfile(string root,AvatarMotionSettings result,Action<string> log)
  {
   var path=Path.Combine(root,"config","interpolation_profile.json");if(!File.Exists(path))return result;
   try {
    if(new FileInfo(path).Length>4096)throw new InvalidDataException();
    var j=JObject.Parse(File.ReadAllText(path),new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
    if(j["mode"]?.Type!=JTokenType.String)throw new InvalidDataException();
    var mode=(string)j["mode"];double milliseconds=mode=="smooth"?500:mode=="normal"?300:mode=="fast"?150:throw new InvalidDataException();
    milliseconds=Number(j,"buffer_ms",milliseconds,100,1000);result.Profile=mode;result.Delay=milliseconds/1000;return result;
   }catch(Exception){log?.Invoke("Interpolation profile invalid; retaining safe motion buffer");return result;}
  }
  static double Number(JObject j,string key,double fallback,double min,double max)
  { if(j[key]==null)return fallback;if(j[key].Type!=JTokenType.Float&&j[key].Type!=JTokenType.Integer)throw new InvalidDataException();var v=(double)j[key];if(double.IsNaN(v)||double.IsInfinity(v)||v<min||v>max)throw new InvalidDataException();return v; }
 }
}
