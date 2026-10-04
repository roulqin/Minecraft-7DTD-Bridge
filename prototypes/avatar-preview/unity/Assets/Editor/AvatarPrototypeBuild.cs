using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class AvatarPrototypeBuild {
    static Dictionary<string,Transform> bones;
    static Transform Bone(string name,string parent,Vector3 position) {var go=new GameObject(name);go.transform.SetParent(bones[parent],false);go.transform.localPosition=position;bones[name]=go.transform;return go.transform;}
    public static void Build() {
        var output=Environment.GetEnvironmentVariable("AVATAR_PROTOTYPE_OUTPUT");Directory.CreateDirectory(output);Directory.CreateDirectory("Assets/Generated");
        var root=new GameObject("AlexSlimPreview");bones=new Dictionary<string,Transform>{{"Root",root.transform}};
        Bone("Hips","Root",new Vector3(0,.9f,0));Bone("Spine","Hips",new Vector3(0,.2f,0));Bone("Chest","Spine",new Vector3(0,.2f,0));Bone("Neck","Chest",new Vector3(0,.2f,0));Bone("Head","Neck",new Vector3(0,.15f,0));
        foreach(var side in new[]{"Left","Right"}) {float sign=side=="Left" ? -1:1;Bone(side+"Shoulder","Chest",new Vector3(sign*.15f,.08f,0));Bone(side+"UpperArm",side+"Shoulder",new Vector3(sign*.15f,0,0));Bone(side+"LowerArm",side+"UpperArm",new Vector3(sign*.3f,0,0));Bone(side+"Hand",side+"LowerArm",new Vector3(sign*.25f,0,0));Bone(side+"UpperLeg","Hips",new Vector3(sign*.1f,0,0));Bone(side+"LowerLeg",side+"UpperLeg",new Vector3(0,-.42f,0));Bone(side+"Foot",side+"LowerLeg",new Vector3(0,-.42f,.04f));}
        var human=new List<HumanBone>();foreach(var pair in bones)if(pair.Key!="Root")human.Add(new HumanBone{boneName=pair.Key,humanName=pair.Key,limit=new HumanLimit{useDefaultValues=true}});
        var skeleton=new List<SkeletonBone>();foreach(var pair in bones)skeleton.Add(new SkeletonBone{name=pair.Key,position=pair.Value.localPosition,rotation=pair.Value.localRotation,scale=pair.Value.localScale});
        var avatar=AvatarBuilder.BuildHumanAvatar(root,new HumanDescription{human=human.ToArray(),skeleton=skeleton.ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0,hasTranslationDoF=false});
        if(!avatar.isValid || !avatar.isHuman)throw new Exception("Humanoid Avatar invalid");avatar.name="AlexSlimHumanoid";AssetDatabase.CreateAsset(avatar,"Assets/Generated/AlexSlim.avatar");
        // Dedicated visual pivots: Generic transform clips test Animator playback independently of retargeting.
        var pose=new GameObject("PreviewPose");pose.transform.SetParent(root.transform,false);
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        CreatePart(pose.transform,"Head",new Vector3(0,1.62f,0),new Vector3(.4f,.4f,.4f),new Vector2(0,0),8,8,8);
        CreatePart(pose.transform,"Body",new Vector3(0,1.12f,0),new Vector3(.4f,.6f,.2f),new Vector2(16,16),8,12,4);
        // Arms in resting pose; the separate Humanoid skeleton is in the required T pose.
        CreatePart(pose.transform,"RightArm",new Vector3(.275f,1.12f,0),new Vector3(.15f,.6f,.2f),new Vector2(40,16),3,12,4);
        CreatePart(pose.transform,"LeftArm",new Vector3(-.275f,1.12f,0),new Vector3(.15f,.6f,.2f),new Vector2(32,48),3,12,4);
        CreatePart(pose.transform,"RightLeg",new Vector3(.1f,.52f,0),new Vector3(.2f,.6f,.2f),new Vector2(0,16),4,12,4);
        CreatePart(pose.transform,"LeftLeg",new Vector3(-.1f,.52f,0),new Vector3(.2f,.6f,.2f),new Vector2(16,48),4,12,4);
        var controller=AnimatorController.CreateAnimatorControllerAtPath("Assets/Generated/Preview.controller");
        foreach(var name in new[]{"Idle","Walk"}) {
            var clip=new AnimationClip{name=name,legacy=false,frameRate=30};
            clip.SetCurve("PreviewPose",typeof(Transform),"localPosition.y",new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,name=="Idle" ? .015f:.04f),new Keyframe(1,0)));
            if(name=="Walk")foreach(var limb in new[]{"LeftLeg","RightLeg","LeftArm","RightArm"}){float sign=limb.StartsWith("Left") ? 1:-1;clip.SetCurve("PreviewPose/"+limb,typeof(Transform),"localEulerAnglesRaw.x",new AnimationCurve(new Keyframe(0,-20*sign),new Keyframe(.5f,20*sign),new Keyframe(1,-20*sign)));}
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            AssetDatabase.CreateAsset(clip,"Assets/Generated/"+name+".anim");controller.AddMotion(clip);
        }
        var animator=root.AddComponent<Animator>();animator.avatar=avatar;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Generated/AvatarPreview.prefab");UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        var build=new AssetBundleBuild{assetBundleName="avatar_preview",assetNames=new[]{"Assets/Generated/AvatarPreview.prefab"}};
        BuildPipeline.BuildAssetBundles(output,new[]{build},BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
        File.WriteAllText(Path.Combine(output,"build-evidence.json"),"{\"avatarValid\":true,\"avatarHuman\":true,\"variant\":\"alex_slim\",\"armWidth\":3,\"animationKind\":\"generic_transform\",\"states\":[\"Idle\",\"Walk\"]}");
        Debug.Log("AVATAR_PROTOTYPE_BUILD PASS");
    }
    static void CreatePart(Transform parent,string name,Vector3 position,Vector3 size,Vector2 origin,int w,int h,int d) {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
        var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();var half=size*.5f;
        // Four vertices per face permit atlas seams. Pixel coordinates use top-left atlas origin.
        Face(v,uv,tris,new[]{new Vector3(-half.x,-half.y,half.z),new Vector3(half.x,-half.y,half.z),new Vector3(half.x,half.y,half.z),new Vector3(-half.x,half.y,half.z)},origin+new Vector2(d,d),w,h);
        Face(v,uv,tris,new[]{new Vector3(half.x,-half.y,-half.z),new Vector3(-half.x,-half.y,-half.z),new Vector3(-half.x,half.y,-half.z),new Vector3(half.x,half.y,-half.z)},origin+new Vector2(2*d+w,d),w,h);
        Face(v,uv,tris,new[]{new Vector3(-half.x,-half.y,-half.z),new Vector3(-half.x,-half.y,half.z),new Vector3(-half.x,half.y,half.z),new Vector3(-half.x,half.y,-half.z)},origin+new Vector2(0,d),d,h);
        Face(v,uv,tris,new[]{new Vector3(half.x,-half.y,half.z),new Vector3(half.x,-half.y,-half.z),new Vector3(half.x,half.y,-half.z),new Vector3(half.x,half.y,half.z)},origin+new Vector2(d+w,d),d,h);
        Face(v,uv,tris,new[]{new Vector3(-half.x,half.y,half.z),new Vector3(half.x,half.y,half.z),new Vector3(half.x,half.y,-half.z),new Vector3(-half.x,half.y,-half.z)},origin+new Vector2(d,0),w,d);
        Face(v,uv,tris,new[]{new Vector3(-half.x,-half.y,-half.z),new Vector3(half.x,-half.y,-half.z),new Vector3(half.x,-half.y,half.z),new Vector3(-half.x,-half.y,half.z)},origin+new Vector2(d+w,0),w,d);
        var mesh=new Mesh{name=name+"SlimMesh"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,"Assets/Generated/"+name+".asset");obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        var material=new Material(Shader.Find("Unlit/Texture")){name="PreviewSkin"};var materialPath="Assets/Generated/PreviewSkin.mat";if(!File.Exists(materialPath))AssetDatabase.CreateAsset(material,materialPath);else{UnityEngine.Object.DestroyImmediate(material);material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);}obj.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    static void Face(List<Vector3> v,List<Vector2> uv,List<int> tris,Vector3[] face,Vector2 p,int w,int h) {int start=v.Count;v.AddRange(face);uv.AddRange(new[]{new Vector2(p.x/64,1-(p.y+h)/64),new Vector2((p.x+w)/64,1-(p.y+h)/64),new Vector2((p.x+w)/64,1-p.y/64),new Vector2(p.x/64,1-p.y/64)});tris.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
}
