using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MC7DTD
{
    // Optional world-space text attached to the existing passive proxy. No gameplay object or UI canvas.
    public sealed class DebugProxyScene:IRotatingProxyScene
    {
        private sealed class Handle { public object Inner;public GameObject Tag;public Font Font; }
        private readonly IMarkerScene inner;
        private readonly DebugConfig config;
        private readonly Action<string> log;
        public DebugProxyScene(IMarkerScene inner,DebugConfig config,Action<string> log){this.inner=inner;this.config=config;this.log=log;}
        public object Create(string name,double x,double y,double z)
        {
            var handle=new Handle{Inner=inner.Create(name,x,y,z)};
            if(config.NameTag)
            {
                try
                {
                    handle.Tag=new GameObject("MC7DTD-debug-name");
                    var text=handle.Tag.AddComponent<TextMesh>();
                    var match=Regex.Match(name,@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");
                    text.text="MC_"+(match.Success ? match.Value : name);
                    text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=0.025f;text.fontSize=32;
                    handle.Font=Font.CreateDynamicFontFromOSFont("Arial",32);text.font=handle.Font;
                    handle.Tag.GetComponent<Renderer>().sharedMaterial=handle.Font.material;
                }
                catch(Exception ex){RemoveTag(handle);if(config.Logging)log("[Entity Debug] Name tag unavailable: "+ex.GetType().Name);}
            }
            Move(handle,x,y,z);return handle;
        }
        public void Move(object value,double x,double y,double z)
        {
            var handle=(Handle)value;inner.Move(handle.Inner,x,y,z);
            if(handle.Tag!=null){handle.Tag.transform.position=new Vector3((float)x,(float)y+2.2f,(float)z)-Origin.position;if(Camera.main!=null)handle.Tag.transform.rotation=Camera.main.transform.rotation;}
        }
        public void Rotate(object value,double yaw,double pitch,double roll){if(inner is IRotatingProxyScene rotation)rotation.Rotate(((Handle)value).Inner,yaw,pitch,roll);}
        private static void RemoveTag(Handle handle){if(handle.Tag!=null){handle.Tag.SetActive(false);UnityEngine.Object.Destroy(handle.Tag);handle.Tag=null;}if(handle.Font!=null){UnityEngine.Object.Destroy(handle.Font);handle.Font=null;}}
        public void Delete(object value){var handle=(Handle)value;RemoveTag(handle);inner.Delete(handle.Inner);}
    }
}
