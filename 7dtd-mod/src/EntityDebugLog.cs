using System;
using System.Collections.Generic;
using System.Globalization;

namespace MC7DTD
{
    public sealed class EntityDebugLog
    {
        private readonly DebugConfig config;
        private readonly Action<string> log;
        private readonly Dictionary<string,double?> scales=new Dictionary<string,double?>();
        public EntityDebugLog(DebugConfig config,Action<string> log) { this.config=config;this.log=log; }
        public void Observe(string key,string id,string type,string action,PresentationComponent presentation)
        {
            if(!config.Logging)return;
            if(action=="despawn"){scales.Remove(key);return;}
            if(action=="spawn")
                log("[Entity Debug]\nSpawn Proxy\nid: "+id+"\ntype: "+type+"\npresentation:\n"+(presentation ?? PresentationComponent.Default()));
            else if(scales.TryGetValue(key,out var previous) && previous!=presentation?.Scale)
                log("[Entity Debug] Presentation Update entity="+id+" scale: "+Number(previous)+" -> "+Number(presentation?.Scale));
            scales[key]=presentation?.Scale;
        }
        private static string Number(double? value)=>value.HasValue ? value.Value.ToString("R",CultureInfo.InvariantCulture) : "removed";
        public void Clear()=>scales.Clear();
    }
}
