using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEditor;
using UnityEditor.Animations;

public sealed class BoneSpec {
 public string name,parent,human; public Vector3 position;
 public BoneSpec(string n,string p,Vector3 v,string h){name=n;parent=p;position=v;human=h;}
}
public sealed class MeshSpec {
 public string name,part,bone,layer;public float[] vertices,uv;public int[] triangles;
 public MeshSpec(string n,string p,string b,string l,float[] v,float[] u,int[] t){name=n;part=p;bone=b;layer=l;vertices=v;uv=u;triangles=t;}
}
public sealed class CurveSpec {
 public string path,property;public float[] keys;
 public CurveSpec(string p,string r,float[] k){path=p;property=r;keys=k;}
}
public sealed class ClipSpec {
 public string name;public CurveSpec[] curves;
 public ClipSpec(string n,CurveSpec[] c){name=n;curves=c;}
}

/// <summary>Editor-only resource authoring and validation. No game Mod or Renderer Runtime.</summary>
public static class AvatarResourceBuild {
 const string Generated="Assets/Avatar/Generated";
 const string PrefabPath=Generated+"/MinecraftAvatarPrefab.prefab";
 const float IdleUpper=.10000000894069672f,WalkUpper=1.0000001192092896f;
 static readonly List<string> results=new List<string>();
 static string evidence,export;
 static void Check(bool ok,string label){results.Add(label+" "+(ok?"PASS":"FAIL"));Debug.Log(results.Last());if(!ok)throw new Exception(label+" failed");}
 [MenuItem("MC7DTD/Produce Avatar Resources")]
 public static void Build(){
  evidence=Environment.GetEnvironmentVariable("AVATAR_RESOURCE_EVIDENCE");export=Environment.GetEnvironmentVariable("AVATAR_RESOURCE_OUTPUT");
  if(string.IsNullOrEmpty(evidence)||string.IsNullOrEmpty(export))throw new Exception("Set AVATAR_RESOURCE_EVIDENCE and AVATAR_RESOURCE_OUTPUT");
  Directory.CreateDirectory(evidence);Directory.CreateDirectory(export);Directory.CreateDirectory(Generated);AssetDatabase.Refresh();results.Clear();File.WriteAllText(Path.Combine(evidence,"animation-deltas.txt"),"");
  try{CreateResources();Verify();BuildBundle();File.WriteAllLines(Path.Combine(evidence,"unity-resource-results.txt"),results);Debug.Log("AVATAR_RESOURCE_PRODUCTION PASS");}
  catch(Exception ex){results.Add("PRODUCTION ERROR "+ex);File.WriteAllLines(Path.Combine(evidence,"unity-resource-results.txt"),results);throw;}
 }
 static void CreateResources(){
  var root=new GameObject("AvatarRoot");var map=new Dictionary<string,Transform>{{"AvatarRoot",root.transform}};
  try{
   var rig=new GameObject("InternalRig");rig.transform.SetParent(root.transform,false);map["InternalRig"]=rig.transform;
   foreach(var b in AvatarProductionSpec.Bones){var go=new GameObject(b.name);go.transform.SetParent(map[b.parent],false);go.transform.localPosition=b.position;map[b.name]=go.transform;}
   var human=AvatarProductionSpec.Bones.Select(b=>new HumanBone{boneName=b.name,humanName=b.human,limit=new HumanLimit{useDefaultValues=true}}).ToArray();
   var skeleton=map.Select(p=>new SkeletonBone{name=p.Key,position=p.Value.localPosition,rotation=p.Value.localRotation,scale=p.Value.localScale}).ToArray();
   var avatar=AvatarBuilder.BuildHumanAvatar(root,new HumanDescription{human=human,skeleton=skeleton,upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f});
   Check(avatar.isValid&&avatar.isHuman,"Humanoid Avatar");avatar.name="MinecraftAvatarHumanoid";AssetDatabase.CreateAsset(avatar,Generated+"/MinecraftAvatarHumanoid.asset");
   var generic=AvatarBuilder.BuildGenericAvatar(root,"");generic.name="MinecraftAvatarGeneric";Check(generic.isValid,"Generic Animation Avatar");AssetDatabase.CreateAsset(generic,Generated+"/MinecraftAvatarGeneric.asset");
   var skinPath="Assets/Avatar/Source/player_default.png";var importer=(TextureImporter)AssetImporter.GetAtPath(skinPath);importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(skinPath);if(texture.width!=64||texture.height!=64)throw new Exception("Expected 64x64 skin");
   var shader=Shader.Find("MC7DTD/AvatarSkin");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Avatar skin shader missing/invalid");
   var baseMat=new Material(shader){name="MinecraftSkinBase",mainTexture=texture,renderQueue=2450};baseMat.SetFloat("_Cutoff",.5f);AssetDatabase.CreateAsset(baseMat,Generated+"/MinecraftSkinBase.mat");
   var layerMat=new Material(shader){name="MinecraftSkinLayer2",mainTexture=texture,renderQueue=2451};layerMat.SetFloat("_Cutoff",.5f);AssetDatabase.CreateAsset(layerMat,Generated+"/MinecraftSkinLayer2.mat");
   var transforms=AvatarProductionSpec.Bones.Select(b=>map[b.name]).ToArray();
   foreach(var part in AvatarProductionSpec.Meshes.GroupBy(m=>m.part)){
    var node=new GameObject(part.Key);node.transform.SetParent(root.transform,false);
    foreach(var spec in part){
     var layer=new GameObject(spec.layer=="base"?"BaseLayer":"Layer2");layer.transform.SetParent(node.transform,false);
     var mesh=new Mesh{name=spec.name};mesh.vertices=Enumerable.Range(0,spec.vertices.Length/3).Select(i=>new Vector3(spec.vertices[3*i],spec.vertices[3*i+1],spec.vertices[3*i+2])).ToArray();mesh.uv=Enumerable.Range(0,spec.uv.Length/2).Select(i=>new Vector2(spec.uv[2*i],spec.uv[2*i+1])).ToArray();mesh.triangles=spec.triangles;
     int bone=Array.FindIndex(AvatarProductionSpec.Bones,b=>b.name==spec.bone);mesh.boneWeights=Enumerable.Range(0,mesh.vertexCount).Select(i=>new BoneWeight{boneIndex0=bone,weight0=1}).ToArray();mesh.bindposes=transforms.Select(t=>t.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Generated+"/"+spec.name+".asset");
     var renderer=layer.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=transforms;renderer.rootBone=map["rig_hips"];renderer.sharedMaterial=spec.layer=="base"?baseMat:layerMat;renderer.updateWhenOffscreen=true;
    }
   }
   var publicRoot=new GameObject("root");publicRoot.transform.SetParent(root.transform,false);
   foreach(var spec in AvatarProductionSpec.Meshes.Where(m=>m.layer=="base")){
    string name=spec.part=="Body"?"body":spec.part=="Head"?"head":spec.part=="Arm_L"?"arm_left":spec.part=="Arm_R"?"arm_right":spec.part=="Leg_L"?"leg_left":"leg_right";
    var anchor=new GameObject(name);anchor.transform.SetParent(publicRoot.transform,false);anchor.transform.position=map[spec.bone].position;
    var constraint=anchor.AddComponent<ParentConstraint>();constraint.AddSource(new ConstraintSource{sourceTransform=map[spec.bone],weight=1});constraint.SetTranslationOffset(0,Vector3.zero);constraint.SetRotationOffset(0,Vector3.zero);constraint.weight=1;constraint.locked=true;constraint.constraintActive=true;
   }
   var controller=AnimatorController.CreateAnimatorControllerAtPath(Generated+"/MinecraftAvatarAnimator.controller");
   controller.AddParameter("speed",AnimatorControllerParameterType.Float);controller.AddParameter("isGrounded",AnimatorControllerParameterType.Bool);controller.AddParameter("verticalVelocity",AnimatorControllerParameterType.Float);controller.AddParameter("actionState",AnimatorControllerParameterType.Int);controller.AddParameter("jumpStart",AnimatorControllerParameterType.Trigger);
   var parameters=controller.parameters;parameters.Single(p=>p.name=="isGrounded").defaultBool=true;controller.parameters=parameters;
   var machine=controller.layers[0].stateMachine;var clips=new Dictionary<string,AnimationClip>();var states=new Dictionary<string,AnimatorState>();
   foreach(var spec in AvatarProductionSpec.Clips){
    var clip=new AnimationClip{name=spec.name.StartsWith("Held")?spec.name.Substring(4):spec.name,frameRate=32,legacy=false};
    foreach(var c in spec.curves){var keys=Enumerable.Range(0,c.keys.Length/2).Select(i=>new Keyframe(c.keys[2*i],c.keys[2*i+1])).ToArray();var curve=new AnimationCurve(keys);for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(c.path,typeof(Transform),c.property),curve);}
    var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=spec.name!="JumpStart"&&spec.name!="Land";settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,settings);AssetDatabase.CreateAsset(clip,Generated+"/"+spec.name+".anim");clips[spec.name]=clip;
   }
   var ground=machine.AddState("Ground");ground.writeDefaultValues=false;states["Ground"]=ground;machine.defaultState=ground;
   var tree=new BlendTree{name="GroundLocomotion",blendType=BlendTreeType.Simple1D,blendParameter="speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(clips["Idle"],0);tree.AddChild(clips["Walk"],2);tree.AddChild(clips["Run"],5);ground.motion=tree;
   foreach(var name in new[]{"JumpStart","JumpLoop","Fall","Land"}){
    var state=machine.AddState(name);state.writeDefaultValues=false;states[name]=state;
    var airTree=new BlendTree{name=name+"Locomotion",blendType=BlendTreeType.Simple1D,blendParameter="speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(airTree,controller);
    airTree.AddChild(clips[name],0);airTree.AddChild(clips["Walk"],2);airTree.AddChild(clips["Run"],5);state.motion=airTree;if(name=="Land")state.speed=4;
   }
   foreach(var from in new[]{"Ground","Land"}){var t=RefineTransition(states[from],states["JumpStart"]);t.AddCondition(AnimatorConditionMode.If,0,"jumpStart");t.AddCondition(AnimatorConditionMode.IfNot,0,"isGrounded");}
   var loop=RefineTransition(states["JumpStart"],states["JumpLoop"],true);loop.exitTime=.85f;loop.AddCondition(AnimatorConditionMode.IfNot,0,"isGrounded");
   foreach(var from in new[]{"Ground","JumpStart","JumpLoop","Land"}){var t=RefineTransition(states[from],states["Fall"]);t.AddCondition(AnimatorConditionMode.IfNot,0,"isGrounded");t.AddCondition(AnimatorConditionMode.Less,-.15f,"verticalVelocity");}
   foreach(var from in new[]{"JumpStart","JumpLoop","Fall"}){var t=RefineTransition(states[from],states["Land"]);t.AddCondition(AnimatorConditionMode.If,0,"isGrounded");}
   var recover=RefineTransition(states["Land"],ground,true);recover.exitTime=.85f;recover.AddCondition(AnimatorConditionMode.If,0,"isGrounded");
   var animator=root.AddComponent<Animator>();animator.avatar=generic;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
  }finally{UnityEngine.Object.DestroyImmediate(root);}
 }
 static AnimatorStateTransition RefineTransition(AnimatorState from,AnimatorState to,bool exit=false){var t=from.AddTransition(to);t.hasExitTime=exit;t.hasFixedDuration=true;t.duration=.12f;t.canTransitionToSelf=false;t.interruptionSource=TransitionInterruptionSource.SourceThenDestination;t.orderedInterruption=true;return t;}
 static string Dominant(Animator a)=>a.GetCurrentAnimatorClipInfo(0).OrderByDescending(c=>c.weight).First().clip.name;
 static void Verify(){
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);Check(asset!=null,"Prefab Load");var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
  try{
   var renderers=instance.GetComponentsInChildren<SkinnedMeshRenderer>();Check(renderers.Length==12&&renderers.Sum(r=>r.sharedMesh.vertexCount)==288&&renderers.All(r=>r.bones.Length==19),"Avatar Mesh Load");
   var skeleton=instance.transform.Find("root");Check(skeleton!=null&&skeleton.childCount==6&&new[]{"body","head","arm_left","arm_right","leg_left","leg_right"}.All(n=>skeleton.Find(n)?.GetComponent<ParentConstraint>()?.GetSource(0).sourceTransform!=null),"Bone Hierarchy");
   Check(instance.GetComponentsInChildren<Transform>().Any(t=>t.name=="rig_hand_right")&&skeleton.Find("head")!=null,"Equipment Anchor Resource Contract");
   var animator=instance.GetComponent<Animator>();Check(animator.runtimeAnimatorController!=null&&animator.avatar.isValid&&!animator.applyRootMotion,"Animator Resource Load");animator.Rebind();animator.Update(0);
   var controller=(AnimatorController)animator.runtimeAnimatorController;
   Check(controller.layers.Length==1&&controller.layers[0].avatarMask==null,"Single Layer No Avatar Mask");
   Check(controller.layers[0].stateMachine.states.All(s=>!s.state.writeDefaultValues),"Write Defaults Disabled");
   var bindings=AvatarProductionSpec.Clips.Select(s=>string.Join(";",s.curves.Select(c=>c.path+":"+c.property).OrderBy(v=>v))).Distinct().Count();
   Check(bindings==1&&AvatarProductionSpec.Clips.All(s=>s.curves.All(c=>c.property.StartsWith("localEulerAnglesRaw.")&&!c.path.EndsWith("rig_head"))),"Uniform Pose Channels Root Head Excluded");
   foreach(var test in new[]{Tuple.Create(0f,"Idle"),Tuple.Create(2f,"Walk"),Tuple.Create(5f,"Run"),Tuple.Create(0f,"Idle")}){animator.SetFloat("speed",test.Item1);for(int i=0;i<30;i++)animator.Update(.05f);Check(Dominant(animator)==test.Item2,test.Item2+" Blend Tree");}
   animator.SetBool("isGrounded",false);animator.SetFloat("verticalVelocity",3);animator.SetTrigger("jumpStart");for(int i=0;i<20;i++)animator.Update(.05f);Check(animator.GetCurrentAnimatorStateInfo(0).IsName("JumpLoop"),"Jump Loop Transition");Capture(instance,"jump.png");
   animator.SetFloat("verticalVelocity",-3);for(int i=0;i<20;i++)animator.Update(.05f);Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Fall"),"Fall Transition");Capture(instance,"fall.png");
   animator.SetBool("isGrounded",true);animator.SetFloat("speed",0);for(int i=0;i<30;i++)animator.Update(.05f);Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Ground")&&Dominant(animator)=="Idle","Landing Ground Recovery");
   foreach(var state in new[]{"Idle","Walk","Run"}){
    float speed=state=="Idle"?0:state=="Walk"?2f:5;animator.SetFloat("speed",speed);for(int i=0;i<30;i++)animator.Update(.05f);var before=Vertices(renderers);animator.Update(.173f);var after=Vertices(renderers);float delta=before.Select((v,i)=>Vector3.Distance(v,after[i])).Average();Check(delta>.00001f,state+" Mesh Animation");File.AppendAllText(Path.Combine(evidence,"animation-deltas.txt"),state+" "+delta+Environment.NewLine);Capture(instance,state.ToLowerInvariant()+".png");
   }
   foreach(var r in renderers.Where(r=>r.name=="Layer2"))r.enabled=false;var baseOnly=Capture(instance,"base-layer.png");foreach(var r in renderers.Where(r=>r.name=="Layer2"))r.enabled=true;var layers=Capture(instance,"layer2.png");
   Check(renderers.Count(r=>r.name=="BaseLayer")==6&&baseOnly.Any(c=>c.r>30||c.g>30||c.b>30),"Base Layer");Check(baseOnly.Where((c,i)=>!c.Equals(layers[i])).Count()>100,"Layer2");
   VerifyTransparentLayer(instance,renderers);
  }finally{UnityEngine.Object.DestroyImmediate(instance);}
 }
 static Vector3[] Vertices(SkinnedMeshRenderer[] renderers){var list=new List<Vector3>();foreach(var r in renderers){var mesh=new Mesh();r.BakeMesh(mesh);list.AddRange(mesh.vertices);UnityEngine.Object.DestroyImmediate(mesh);}return list.ToArray();}
 static void VerifyTransparentLayer(GameObject instance,SkinnedMeshRenderer[] renderers){
  var layers=renderers.Where(r=>r.name=="Layer2").ToArray();foreach(var r in layers)r.enabled=false;var without=Capture(instance,"alpha-baseline.png");
  var transparent=new Texture2D(64,64,TextureFormat.RGBA32,false);transparent.SetPixels(new Color[64*64]);transparent.Apply();var clones=new List<Material>();
  try{foreach(var r in layers){var m=new Material(r.sharedMaterial){mainTexture=transparent};clones.Add(m);r.sharedMaterial=m;r.enabled=true;}var with=Capture(instance,"alpha-transparent.png");Check(without.SequenceEqual(with),"Layer2 Transparent Pixels");}
  finally{foreach(var m in clones)UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(transparent);}
 }
 static Color32[] Capture(GameObject root,string file){
  var objects=new List<GameObject>();var meshes=new List<Mesh>();var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>();var enabled=renderers.Select(r=>r.enabled).ToArray();
  var cameraObject=new GameObject("ResourceEvidenceCamera");var camera=cameraObject.AddComponent<Camera>();var target=new RenderTexture(512,512,24);Texture2D image=null;var old=RenderTexture.active;
  try{
   foreach(var r in renderers.Where(r=>r.enabled)){var baked=new Mesh();r.BakeMesh(baked);meshes.Add(baked);var frozen=new GameObject("ResourceEvidencePose");objects.Add(frozen);frozen.layer=30;frozen.transform.SetParent(root.transform,false);frozen.AddComponent<MeshFilter>().sharedMesh=baked;frozen.AddComponent<MeshRenderer>().sharedMaterial=r.sharedMaterial;}
   foreach(var r in renderers)r.enabled=false;
   camera.enabled=false;camera.targetTexture=target;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=1.25f;camera.transform.position=root.transform.position+new Vector3(2,1.2f,3);camera.transform.LookAt(root.transform.position+new Vector3(0,1,0));camera.Render();RenderTexture.active=target;image=new Texture2D(512,512,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();File.WriteAllBytes(Path.Combine(evidence,file),image.EncodeToPNG());return image.GetPixels32();
  }finally{RenderTexture.active=old;camera.targetTexture=null;for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(cameraObject);target.Release();UnityEngine.Object.DestroyImmediate(target);if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
 }
 static void BuildBundle(){
  var bundle=new AssetBundleBuild{assetBundleName="minecraft_avatar_v1",assetNames=new[]{PrefabPath,Generated+"/MinecraftAvatarHumanoid.asset",Generated+"/HeldWalk.anim",Generated+"/HeldRun.anim"}};
  var manifest=BuildPipeline.BuildAssetBundles(export,new[]{bundle},BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);Check(manifest!=null&&File.Exists(Path.Combine(export,"minecraft_avatar_v1")),"Windows AssetBundle Build");
  var loaded=AssetBundle.LoadFromFile(Path.Combine(export,"minecraft_avatar_v1"));Check(loaded!=null,"AssetBundle Load");try{var prefab=loaded.LoadAsset<GameObject>(PrefabPath);var instance=UnityEngine.Object.Instantiate(prefab);try{Check(instance.GetComponentsInChildren<SkinnedMeshRenderer>().Length==12&&instance.GetComponent<Animator>().runtimeAnimatorController!=null,"Bundled Prefab Load");}finally{UnityEngine.Object.DestroyImmediate(instance);}}finally{loaded.Unload(true);}
 }
}
