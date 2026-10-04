using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace MC7DTD.RuntimeTests
{
 public sealed partial class RuntimeRunner
 {
  void PolishChecks(AvatarRenderer.Handle h, string evidence)
  {
   Check(h.Materials.All(m=>m.shader.name=="MC7DTD/AvatarSkin"&&m.shader.isSupported),"Cutout Shader");
   Check(h.Materials.All(m=>m.FindPass("AVATAR_DEPTH")>=0),"Avatar Camera Depth Pass");
   Check(h.Materials.All(m=>Math.Abs(m.GetFloat("_Cutoff")-.5)<.001),"Base Alpha Cutoff");
   Check(h.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name=="Layer2").All(r=>r.sharedMaterial.renderQueue==2451),"Layer2 Alpha");
   var state=new AvatarAnimationState();state.Observe(0,0,0);Check(state.State=="idle","Animation Idle");
   state.Observe(1,0,.5);Check(state.State=="walking"&&state.AnimatorSpeed==.5f,"Animation Walk");
   state.Observe(4,0,1);Check(state.State=="running"&&state.AnimatorSpeed>1f,"Animation Run");
   for(int i=0;i<30;i++)state.Observe(4,0,1.1+i*.01);Check(state.State=="running","Duplicate Pose Keeps Animation");
   state.Tick(1.86);Check(state.State=="idle","Animation Stop Timeout");
   state.Observe(1000,0,2);Check(state.State=="idle","Teleport Does Not Run");
   state.Observe(1000,0,2.5);Check(state.State=="idle","Stationary Horizontal Pose Idle");
   foreach(var name in new[]{"Idle","Walk","Run"}) {
    h.Animator.SetFloat("speed",name=="Idle"?0:name=="Walk"?2f:5);
    for(int i=0;i<20;i++)h.Animator.Update(.05f);
    h.Pose.Pitch=35;h.Pose.Apply();
    Check(IsAnimation(h.Animator,name)&&!h.Animator.applyRootMotion&&Quaternion.Angle(h.Head.localRotation,Quaternion.Euler(35,0,0))<.01f,
      "Native Animator "+name+" Head Pitch Preserved");
   }
   h.Animator.SetFloat("speed",0);for(int i=0;i<20;i++)h.Animator.Update(.05f);h.Animation.Reset();
   Check(new AvatarInspectorCommand().getCommands().Contains("mc7dtd_entity_inspect"),"Local Inspector Alias");
   PixelChecks(h.Materials[0],evidence);
  }
  void PixelChecks(Material source,string evidence)
  {
   var previous=RenderTexture.active;
   var scene=new GameObject("IsolatedCutoutGPUFixture");scene.transform.position=new Vector3(9000,9000,9000);
   var texture=new Texture2D(4,1,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
   texture.SetPixels(new[]{new Color(1,1,1,0),new Color(1,1,1,.25f),new Color(1,1,1,.75f),Color.white});texture.Apply();
   var material=new Material(source){mainTexture=texture,renderQueue=2450};
   var redTexture=new Texture2D(1,1);redTexture.SetPixel(0,0,Color.red);redTexture.Apply();
   var behindMaterial=new Material(source){mainTexture=redTexture,renderQueue=2451};
   var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32);target.Create();
   var depthTarget=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32);depthTarget.Create();
   var depthCapture=new CommandBuffer { name="AvatarDepthTestCapture" };
   var pixels=new Texture2D(64,64,TextureFormat.RGBA32,false);
   try {
    var cameraObject=new GameObject("GPUCamera");cameraObject.transform.SetParent(scene.transform,false);cameraObject.transform.localPosition=new Vector3(0,0,-3);
    var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;camera.nearClipPlane=.1f;camera.farClipPlane=10;
    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.green;camera.cullingMask=1<<31;camera.targetTexture=target;camera.depthTextureMode=DepthTextureMode.Depth;camera.renderingPath=RenderingPath.Forward;
    depthCapture.Blit(BuiltinRenderTextureType.Depth,depthTarget);
    camera.AddCommandBuffer(CameraEvent.AfterDepthTexture,depthCapture);
    var front=GameObject.CreatePrimitive(PrimitiveType.Quad);front.layer=31;front.transform.SetParent(scene.transform,false);front.GetComponent<Collider>().enabled=false;front.GetComponent<MeshRenderer>().sharedMaterial=material;
    camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();
    var partial=pixels.GetPixel(40,32);var hole=pixels.GetPixel(24,32);
    Check(partial.r>.95f&&partial.b>.95f,"GPU Partial Alpha Fully Opaque");
    Check(hole.g>.95f&&hole.r<.05f,"GPU Transparent Pixels Clipped");
    File.WriteAllBytes(Path.Combine(evidence,"cutout-alpha-gpu.png"),ImageConversion.EncodeToPNG(pixels));
    RenderTexture.active=depthTarget;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();
    Check(Math.Abs(pixels.GetPixel(40,32).r-pixels.GetPixel(24,32).r)>.1f,"GPU Camera Depth Includes Cutout Avatar");
    File.WriteAllBytes(Path.Combine(evidence,"cutout-camera-depth-gpu.png"),ImageConversion.EncodeToPNG(pixels));
    var back=GameObject.CreatePrimitive(PrimitiveType.Quad);back.layer=31;back.transform.SetParent(scene.transform,false);back.transform.localPosition=new Vector3(0,0,.2f);back.GetComponent<Collider>().enabled=false;back.GetComponent<MeshRenderer>().sharedMaterial=behindMaterial;
    camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();
    var opaque=pixels.GetPixel(40,32);var through=pixels.GetPixel(24,32);
    Check(opaque.g>.95f&&opaque.b>.95f&&through.r>.95f&&through.g<.05f,"GPU Depth Write And Layer Ordering");
    File.WriteAllBytes(Path.Combine(evidence,"cutout-depth-gpu.png"),ImageConversion.EncodeToPNG(pixels));
   } finally {
    RenderTexture.active=previous;scene.SetActive(false);UnityEngine.Object.Destroy(scene);target.Release();depthTarget.Release();depthCapture.Release();
    foreach(var asset in new UnityEngine.Object[]{texture,material,redTexture,behindMaterial,target,depthTarget,pixels})UnityEngine.Object.Destroy(asset);
   }
  }
 }
}
