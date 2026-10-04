using System;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MC7DTD
{
    // Local fixture input only. This does not claim Minecraft -> 7DTD equipment synchronization.
    public sealed class LocalAvatarEquipmentAdapter
    {
        public string Item { get; private set; }
        public GameObject Visual { get; private set; }
        Material material;
        public EquipmentAttachmentRuntime Attachment { get; private set; }
        Transform avatarRoot;
        readonly Dictionary<string,EquipmentAttachmentRuntime> slots=new Dictionary<string,EquipmentAttachmentRuntime>(StringComparer.Ordinal);
        public Action Changed;
        public bool Holding => slots.TryGetValue("right_hand",out var right)&&right.Visual!=null || slots.TryGetValue("left_hand",out var left)&&left.Visual!=null;
        public EquipmentAttachmentRuntime Slot(string socket)=>slots.TryGetValue(socket,out var value)?value:null;
        public JObject InspectSlots(){var result=new JObject();foreach(var p in slots)result[p.Key]=p.Value.Style==null?JValue.CreateNull():(JToken)p.Value.Inspect();return result;}
        public void Configure(Transform root, EquipmentStyleResolver resolver)
        { Remove(); slots.Clear();avatarRoot=root;foreach(var p in resolver.Sockets)slots.Add(p.Key,new EquipmentAttachmentRuntime(resolver,p.Key));Attachment=slots["right_hand"]; }
        public void SetSlot(string socket,string item)
        {if(!slots.TryGetValue(socket,out var runtime))throw new ArgumentException("Unknown socket");runtime.Update(avatarRoot,item);Item=Attachment.Style?.ItemId;Visual=Attachment.Visual;Changed?.Invoke();}
        public void Update(Transform rightHand, string item)
        {
            if(Attachment!=null){SetSlot("right_hand",item);return;}
            if (Item == item && Visual != null) return;
            Remove(); if (item == null || rightHand == null) return;
            if (item != "7dtd:woodenClub" && item != "7dtd:torch" && item != "7dtd:ironAxe") return;
            try {
                var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                if (shader == null) return;
                material = new Material(shader) { color = item == "7dtd:torch" ? Color.yellow : item == "7dtd:ironAxe" ? Color.gray : new Color(.45f,.25f,.1f) };
                Visual = GameObject.CreatePrimitive(PrimitiveType.Cube); Visual.name = "LocalHeldItem-" + item;
                Visual.transform.SetParent(rightHand, false); Visual.transform.localPosition = new Vector3(.08f,0,.12f);
                Visual.transform.localScale = new Vector3(.12f,.12f,.5f);
                var collider=Visual.GetComponent<Collider>();collider.enabled=false;UnityEngine.Object.Destroy(collider);
                Visual.GetComponent<Renderer>().sharedMaterial=material; Item=item;
            } catch { Remove(); }
        }
        public void Remove()
        { if(Attachment!=null){foreach(var value in slots.Values)value.Remove();}else if(Visual!=null){Visual.SetActive(false);UnityEngine.Object.Destroy(Visual);}if(material!=null)UnityEngine.Object.Destroy(material);Visual=null;material=null;Item=null;Changed?.Invoke(); }
    }
}
