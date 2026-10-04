#nullable enable
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;
using System.Xml;

namespace MC7DTD
{
    [DataContract]
    public sealed class DebugConfig
    {
        [DataMember(Name="debug_name_tag",IsRequired=true)] public bool NameTag;
        [DataMember(Name="debug_logging",IsRequired=true)] public bool Logging;
        public static DebugConfig Load(string path, Action<string>? warning=null)
        {
            if (!File.Exists(path)) return new DebugConfig();
            try
            {
                var bytes=File.ReadAllBytes(path);if(bytes.Length>4096)throw new SerializationException("Config too large");
                // Use the framework JSON reader, retaining JSON types and rejecting duplicates/unknown fields.
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,XmlDictionaryReaderQuotas.Max))
                {
                    reader.MoveToContent();if(reader.LocalName!="root" || reader.GetAttribute("type")!="object")throw new SerializationException();
                    reader.ReadStartElement();var result=new DebugConfig();var seen=new HashSet<string>();
                    while(reader.MoveToContent()==XmlNodeType.Element)
                    {
                        var name=reader.LocalName;
                        if((name!="debug_name_tag" && name!="debug_logging" && name!="debug_navigation") || !seen.Add(name) || reader.GetAttribute("type")!="boolean")throw new SerializationException();
                        bool value=reader.ReadElementContentAsBoolean();if(name=="debug_name_tag")result.NameTag=value;else if(name=="debug_logging")result.Logging=value;
                    }
                    reader.ReadEndElement();if(!seen.Contains("debug_name_tag") || !seen.Contains("debug_logging"))throw new SerializationException();
                    return result;
                }
            }
            catch(Exception ex) { warning?.Invoke("Debug config invalid; debug display disabled: "+ex.GetType().Name); return new DebugConfig(); }
        }
    }
}
