using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MC7DTD
{
    // All data here is local presentation. No wire component or revision is added.
    public sealed class EquipmentStyle
    {
        public string ItemId, SourceGame, Style, Renderer, Model, Socket;
    }
    public sealed class AvatarSocket
    {
        public string Name, Bone;
        public Vector3 Offset, Rotation;
        public float Scale=1;
        public Transform Resolve(Transform root) => root==null ? null : root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==Bone);
        public void Attach(GameObject visual,Transform bone)
        { visual.transform.SetParent(bone,false);visual.transform.localPosition=Offset;visual.transform.localRotation=Quaternion.Euler(Rotation);visual.transform.localScale=Vector3.one*Scale; }
    }
    public sealed class EquipmentStyleResolver
    {
        readonly Dictionary<string,EquipmentStyle> items=new Dictionary<string,EquipmentStyle>(StringComparer.Ordinal);
        public readonly Dictionary<string,AvatarSocket> Sockets=new Dictionary<string,AvatarSocket>(StringComparer.Ordinal);
        public string ConfigStatus {get;private set;}="default";
        public EquipmentStyleResolver(string projectRoot)
        {
            string[] names={"right_hand","left_hand","head","back","body","waist"},bones={"rig_hand_right","rig_hand_left","rig_head","rig_chest","rig_spine","rig_hips"};
            for(int i=0;i<names.Length;i++)Sockets[names[i]]=new AvatarSocket{Name=names[i],Bone=bones[i],Offset=i<2?new Vector3(i==0?.08f:-.08f,0,.12f):Vector3.zero};
            try {
                var path=Path.Combine(projectRoot,"config","equipment_attachment.json");if(!File.Exists(path))return;
                var text=File.ReadAllText(path);if(text.Length>65536)throw new InvalidDataException();
                var config=JObject.Parse(text,new Newtonsoft.Json.Linq.JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                var newItems=new Dictionary<string,EquipmentStyle>(StringComparer.Ordinal);
                var newSockets=new Dictionary<string,AvatarSocket>(Sockets,StringComparer.Ordinal);
                foreach(var p in ((JObject)config["sockets"]).Properties()) {
                    if(!newSockets.ContainsKey(p.Name))throw new InvalidDataException();var value=(JObject)p.Value;
                    var bone=(string)value["bone"];var scale=(float)value["scale"];if(string.IsNullOrWhiteSpace(bone)||scale<=0||scale>10||float.IsNaN(scale))throw new InvalidDataException();
                    newSockets[p.Name]=new AvatarSocket{Name=p.Name,Bone=bone,Scale=scale,Offset=Vector(value["offset"]),Rotation=Vector(value["rotation"])};
                }
                foreach(var p in ((JObject)config["items"]).Properties()) {
                    var v=(JObject)p.Value;var s=new EquipmentStyle{ItemId=p.Name,SourceGame=(string)v["source_game"],Style=(string)v["style"],Renderer=(string)v["renderer"],Model=(string)v["model"],Socket=(string)v["socket"]};
                    if(p.Name.Length>128||string.IsNullOrWhiteSpace(s.SourceGame)||string.IsNullOrWhiteSpace(s.Style)||string.IsNullOrWhiteSpace(s.Renderer)||string.IsNullOrWhiteSpace(s.Model)||!newSockets.ContainsKey(s.Socket??""))throw new InvalidDataException();
                    newItems.Add(p.Name,s);
                }
                foreach(var p in newItems)items.Add(p.Key,p.Value);Sockets.Clear();foreach(var p in newSockets)Sockets.Add(p.Key,p.Value);ConfigStatus="loaded";
            } catch { items.Clear();ConfigStatus="invalid_fallback"; }
        }
        static Vector3 Vector(JToken v)
        {var r=new Vector3((float)v["x"],(float)v["y"],(float)v["z"]);if(float.IsNaN(r.x)||float.IsNaN(r.y)||float.IsNaN(r.z)||float.IsInfinity(r.x)||float.IsInfinity(r.y)||float.IsInfinity(r.z))throw new InvalidDataException();return r;}
        public EquipmentStyle Resolve(string item)
        {if(item==null)return null;return items.TryGetValue(item,out var result)?result:new EquipmentStyle{ItemId=item,SourceGame="unknown",Style="fallback",Renderer="legacy_fallback",Model="marker",Socket="right_hand"};}
    }
    public interface IEquipmentVisualRenderer
    { string Id {get;} EquipmentVisual Create(string model); }
    public class EquipmentVisualObject
    {
        public GameObject Root;
        public readonly List<Material> Materials=new List<Material>();
        public readonly List<Mesh> Meshes=new List<Mesh>();
        public string Status {get;private set;}="created";
        public void Update(Vector3 offset,Quaternion rotation,Vector3 scale){if(Root==null)return;Root.transform.localPosition=offset;Root.transform.localRotation=rotation;Root.transform.localScale=scale;}
        public void Attach(AvatarSocket socket,Transform bone){if(Root==null||bone==null)throw new InvalidOperationException("socket_missing");socket.Attach(Root,bone);Root.SetActive(true);Status="attached";}
        public void Detach(){if(Root==null)return;Root.SetActive(false);Root.transform.SetParent(null,true);Status="detached";}
        public void Destroy(){Detach();if(Root!=null)UnityEngine.Object.Destroy(Root);foreach(var m in Materials)UnityEngine.Object.Destroy(m);foreach(var mesh in Meshes)UnityEngine.Object.Destroy(mesh);Materials.Clear();Meshes.Clear();Root=null;Status="destroyed";}
        public void Remove(){Destroy();}
        public void Part(PrimitiveType type,Vector3 offset,Vector3 scale,Color color)
        {
            var p=GameObject.CreatePrimitive(type);p.transform.SetParent(Root.transform,false);p.transform.localPosition=offset;p.transform.localScale=scale;
            var collider=p.GetComponent<Collider>();collider.enabled=false;UnityEngine.Object.Destroy(collider);
            var shader=Shader.Find("Unlit/Color")??Shader.Find("Standard");if(shader==null)throw new InvalidOperationException("shader_missing");
            var material=new Material(shader){color=color};Materials.Add(material);p.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
    public sealed class EquipmentVisual:EquipmentVisualObject { }
    // Foundation test assets, deliberately independent of source-game item definitions.
    public sealed class MinecraftEquipmentRenderer:IEquipmentVisualRenderer
    {
        public string Id=>"minecraft_renderer";
        public EquipmentVisual Create(string model)
        {if(model!="diamond_sword")throw new InvalidOperationException("model_missing");var v=new EquipmentVisual{Root=new GameObject("BlockySword")};try{
            for(int i=0;i<8;i++)v.Part(PrimitiveType.Cube,new Vector3(0,0,.05f*i),new Vector3(.075f,.045f,.05f),i<2?new Color(.35f,.2f,.08f):Color.cyan);
            v.Part(PrimitiveType.Cube,new Vector3(0,0,.1f),new Vector3(.23f,.055f,.055f),new Color(0,.5f,.5f));return v;
        }catch{v.Remove();throw;}}
    }
    public sealed class SevenDtdEquipmentRenderer:IEquipmentVisualRenderer
    {
        public string Id=>"7dtd_renderer";
        public EquipmentVisual Create(string model)
        {if(model!="axe")throw new InvalidOperationException("model_missing");var v=new EquipmentVisual{Root=new GameObject("RealisticAxe")};try{
            v.Part(PrimitiveType.Cylinder,new Vector3(0,0,.2f),new Vector3(.045f,.25f,.045f),new Color(.4f,.22f,.1f));
            v.Root.transform.GetChild(0).localRotation=Quaternion.Euler(90,0,0);
            v.Part(PrimitiveType.Cube,new Vector3(.09f,0,.4f),new Vector3(.25f,.055f,.14f),Color.gray);return v;
        }catch{v.Remove();throw;}}
    }
    public sealed class LegacyFallbackRenderer:IEquipmentVisualRenderer
    {
        public string Id=>"legacy_fallback";
        public EquipmentVisual Create(string model)
        {var v=new EquipmentVisual{Root=new GameObject("LegacyEquipmentMarker")};try{v.Part(PrimitiveType.Cube,Vector3.zero,new Vector3(.12f,.12f,.5f),Color.yellow);return v;}catch{v.Remove();throw;}}
    }
    public sealed class EquipmentAttachmentRuntime
    {
        readonly EquipmentStyleResolver resolver;
        readonly EquipmentRendererProvider provider=new EquipmentRendererProvider();
        readonly string socketOverride;
        string currentSocket;
        EquipmentVisual visual;Transform bone;
        public EquipmentStyle Style {get;private set;}
        public string RendererId {get;private set;}
        public string Reason {get;private set;}
        public GameObject Visual=>visual?.Root;
        public EquipmentAttachmentRuntime(EquipmentStyleResolver resolver,string socket=null)
        {this.resolver=resolver;socketOverride=socket;if(socket!=null&&!resolver.Sockets.ContainsKey(socket))throw new ArgumentException("Unknown socket");}
        public void Register(IEquipmentVisualRenderer renderer){provider.Register(renderer);}
        public void Update(Transform root,string item)
        {
            var style=resolver.Resolve(item);if(style==null){Remove();return;}
            var socket=resolver.Sockets[socketOverride??style.Socket];var target=socket.Resolve(root);
            if(Style?.ItemId==item&&Visual!=null&&bone==target)return;
            Remove();Style=style;currentSocket=socket.Name;if(target==null){Reason="socket_missing";return;}
            try{visual=provider.Create(style.Renderer,style.Model);RendererId=style.Renderer;}
            catch{Reason="renderer_or_model_unavailable";try{visual=provider.Create("legacy_fallback","marker");RendererId="legacy_fallback";}catch{Reason="fallback_unavailable";return;}}
            try{visual.Attach(socket,target);bone=target;}catch{visual.Remove();visual=null;Reason="attach_failed";}
        }
        public void Remove(){visual?.Remove();visual=null;bone=null;Style=null;RendererId=null;Reason=null;currentSocket=null;}
        public JObject Inspect()=>new JObject{["item_id"]=Style?.ItemId,["item"]=Style?.Model,["source_game"]=Style?.SourceGame,["style"]=Style?.Style,["renderer"]=RendererId,["socket"]=currentSocket,["status"]=visual?.Status??(Style==null?"empty":"unavailable"),["active"]=Visual!=null&&Visual.activeInHierarchy,["attached"]=Visual!=null&&bone!=null&&Visual.transform.parent==bone,["reason"]=Reason,["config_status"]=resolver.ConfigStatus,["input"]="local_test"};
    }
}
