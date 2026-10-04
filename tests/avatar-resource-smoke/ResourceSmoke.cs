using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MC7DTD.ResourceTests {
 public sealed class ResourceSmoke : IModApi {
  public void InitMod(Mod mod){ResourceRunner.BundlePath=Path.Combine(mod.Path,"minecraft_avatar_v1");ResourceRunner.Evidence=Environment.GetEnvironmentVariable("AVATAR_RESOURCE_EVIDENCE");var host=new GameObject("ResourceSmokeOnly");UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<ResourceRunner>();Log.Out("[AvatarResourceSmoke] Resource-only test; no world or Entity/Equipment/Network integration");}
 }
 public sealed class ResourceRunner : MonoBehaviour {
  public static string BundlePath,Evidence;
  readonly List<string> results=new List<string>();
  void Check(bool ok,string label){string result=label+" "+(ok?"PASS":"FAIL");results.Add(result);Log.Out("[AvatarResourceSmoke] "+result);if(!ok)throw new Exception(label);}
  IEnumerator Start(){yield return new WaitForSecondsRealtime(15);AssetBundle bundle=null;GameObject root=null;
   try{
    bundle=AssetBundle.LoadFromFile(BundlePath);Check(bundle!=null,"7DTD AssetBundle Load");
    var prefab=bundle.LoadAsset<GameObject>("Assets/Avatar/Generated/MinecraftAvatarPrefab.prefab");Check(prefab!=null,"7DTD Prefab Load");root=Instantiate(prefab);root.transform.position=new Vector3(0,10000,0);
    var human=bundle.LoadAsset<Avatar>("Assets/Avatar/Generated/MinecraftAvatarHumanoid.asset");Check(human!=null&&human.isValid&&human.isHuman,"7DTD Humanoid Avatar");
    var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>();Check(renderers.Length==12&&renderers.All(r=>r.sharedMesh.vertexCount==24&&r.bones.Length==19),"7DTD Mesh Load");
    Check(renderers.All(r=>r.sharedMaterial.shader.isSupported&&r.sharedMaterial.mainTexture.width==64&&r.sharedMaterial.mainTexture.height==64),"7DTD Material Skin Shader");
    Check(root.transform.Find("root").childCount==6,"7DTD Public Skeleton");
    var animator=root.GetComponent<Animator>();animator.Rebind();animator.Update(0);
    foreach(var test in new[]{Tuple.Create(0f,"Idle"),Tuple.Create(.5f,"Walk"),Tuple.Create(2f,"Run"),Tuple.Create(0f,"Idle")}){
     animator.SetFloat("speed",test.Item1);for(int i=0;i<30;i++)animator.Update(.05f);Check(animator.GetCurrentAnimatorStateInfo(0).IsName(test.Item2),"7DTD "+test.Item2+" Controller");
    }
   }catch(Exception ex){results.Add("RESOURCE ERROR "+ex);Log.Error("[AvatarResourceSmoke] "+ex);}
   finally{if(root!=null){root.SetActive(false);Destroy(root);}if(bundle!=null)bundle.Unload(false);Directory.CreateDirectory(Evidence);File.WriteAllLines(Path.Combine(Evidence,"7dtd-resource-results.txt"),results);}
   yield return null;Application.Quit();
  }
 }
}
