using System;
using System.Collections.Generic;
using UnityEngine;
namespace MC7DTD
{
    public sealed class EquipmentRendererProvider
    {
        readonly Dictionary<string,IEquipmentVisualRenderer> renderers=new Dictionary<string,IEquipmentVisualRenderer>(StringComparer.Ordinal);
        public EquipmentRendererProvider(){Register(new MinecraftEquipmentRenderer());Register(new SevenDtdEquipmentRenderer());Register(new LegacyFallbackRenderer());Register(new CatalogEquipmentRenderer("minecraft","card"));Register(new CatalogEquipmentRenderer("voxel","voxel"));Register(new CatalogEquipmentRenderer("realistic","mesh"));}
        public void Register(IEquipmentVisualRenderer renderer){renderers[renderer.Id]=renderer;}
        public EquipmentVisual Create(string renderer,string model)=>renderers[renderer].Create(model);
    }
    // Recognizable local resources. Source game is intentionally absent from this API.
    public sealed class CatalogEquipmentRenderer:IEquipmentVisualRenderer
    {
        readonly string mode;
        public string Id {get;}
        public CatalogEquipmentRenderer(string id,string mode){Id=id;this.mode=mode;}
        public EquipmentVisual Create(string model)
        {
            var visual=new EquipmentVisual{Root=new GameObject(Id+"-"+model)};
            try {
                var wood=new Color(.4f,.22f,.1f);var steel=Color.gray;
                switch(model){
                    case "club":Part(visual,new Vector3(0,0,.23f),new Vector3(.1f,.09f,.5f),wood);Part(visual,new Vector3(0,0,.38f),new Vector3(.17f,.12f,.24f),wood);break;
                    case "axe":case "pickaxe":case "shovel":
                        Part(visual,new Vector3(0,0,.25f),new Vector3(.05f,.05f,.55f),wood);
                        Part(visual,new Vector3(model=="axe"?.09f:0,0,.5f),model=="shovel"?new Vector3(.16f,.045f,.2f):new Vector3(model=="pickaxe"?.32f:.25f,.05f,.12f),steel);break;
                    case "pistol":case "rifle":
                        Part(visual,new Vector3(0,0,.12f),new Vector3(.07f,.1f,model=="rifle"?.7f:.25f),new Color(.18f,.18f,.2f));
                        Part(visual,new Vector3(0,-.1f,.04f),new Vector3(.065f,.2f,.07f),wood);
                        if(model=="rifle")Part(visual,new Vector3(0,-.01f,-.24f),new Vector3(.09f,.15f,.23f),wood);break;
                    case "torch":Part(visual,new Vector3(0,0,.15f),new Vector3(.07f,.07f,.35f),wood);Part(visual,new Vector3(0,0,.34f),new Vector3(.11f,.1f,.1f),Color.yellow);break;
                    case "helmet":Part(visual,Vector3.zero,new Vector3(.55f,.12f,.55f),steel);break;
                    case "backpack":Part(visual,Vector3.zero,new Vector3(.4f,.5f,.18f),new Color(.2f,.35f,.15f));Part(visual,new Vector3(0,0,-.12f),new Vector3(.25f,.2f,.09f),wood);break;
                    default:throw new InvalidOperationException("model_missing");
                }
                return visual;
            }catch{visual.Remove();throw;}
        }
        void Part(EquipmentVisual visual,Vector3 offset,Vector3 scale,Color color)
        {
            if(mode=="voxel"){
                int steps=Math.Max(1,(int)Math.Ceiling(scale.z/.08f));
                for(int i=0;i<steps;i++)visual.Part(PrimitiveType.Cube,offset+new Vector3(0,0,-scale.z/2+scale.z*(i+.5f)/steps),new Vector3(scale.x,scale.y,scale.z/steps),color);return;
            }
            if(mode=="card"){visual.Part(PrimitiveType.Cube,offset,new Vector3(scale.x,.02f,scale.z),color);return;}
            // Owned Unity mesh; not a native 7DTD inventory/prefab asset.
            var part=new GameObject("UnityMeshPart");part.transform.SetParent(visual.Root.transform,false);part.transform.localPosition=offset;part.transform.localScale=scale;
            var mesh=new Mesh{name="EquipmentBoxMesh"};visual.Meshes.Add(mesh);
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,1,2,6,1,6,5,3,0,4,3,4,7};mesh.RecalculateNormals();mesh.RecalculateBounds();part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var shader=Shader.Find("Unlit/Color")??Shader.Find("Standard");if(shader==null)throw new InvalidOperationException("shader_missing");var material=new Material(shader){color=color};visual.Materials.Add(material);part.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
