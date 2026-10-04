using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MC7DTD
{
    [DataContract]
    public sealed class NameValue
    {
        [DataMember(Name="text")] public string Text;
        [DataMember(Name="display_name",EmitDefaultValue=false)] public string DisplayName;
    }
    [DataContract]
    public sealed class IdentitySample
    {
        [DataMember(Name="enabled",IsRequired=true)] public bool Enabled;
        [DataMember(Name="display_name")] public string DisplayName;
        [DataMember(Name="tags")] public Dictionary<string,string> Tags;
        public string NativeName;
        public string NativeDisplayName;
        public string NameJson => ComponentMessage.Json(new NameValue { Text=NativeName, DisplayName=DisplayName??NativeDisplayName??NativeName });
        public string TagsJson => ComponentMessage.Json(Tags);
        public static void Text(string text,int max)
        {
            if(string.IsNullOrEmpty(text)) throw new InvalidDataException("identity text empty");
            var count=0;
            for(int i=0;i<text.Length;i++) {
                var c=text[i];if(c<32 || c>=127 && c<=159)throw new InvalidDataException("identity text control");
                if(char.IsHighSurrogate(c)){if(i+1>=text.Length||!char.IsLowSurrogate(text[++i]))throw new InvalidDataException("identity text surrogate");}
                else if(char.IsLowSurrogate(c))throw new InvalidDataException("identity text surrogate");count++;
            }
            if(count>max)throw new InvalidDataException("identity text too long");
        }
        public void Validate()
        {
            if(!Enabled)return;Text(NativeName,128);Text(DisplayName??NativeDisplayName??NativeName,128);
            if(Tags==null || Tags.Count>32)throw new InvalidDataException("identity tags missing/too many");
            foreach(var item in Tags) {
                if(item.Key.Length>64 || !System.Text.RegularExpressions.Regex.IsMatch(item.Key,@"\Atag\.[a-zA-Z0-9_.-]+\z"))throw new InvalidDataException("identity tag key");
                Text(item.Value,256);
            }
        }
    }
    // Constructs only the sidecar components object; core v2 DTO is unchanged.
    public sealed class ComponentMessage
    {
        public HealthComponentMessage Envelope;
        public Dictionary<string,string> Values;
        public byte[] Serialize()
        {
            Envelope.Components=new Dictionary<string,HealthValue>();
            var json=Json(Envelope);
            var members=new List<string>();foreach(var item in Values) members.Add(Json(item.Key)+":"+(item.Value??"null"));
            return Encoding.UTF8.GetBytes(json.Replace("\"components\":{}","\"components\":{"+string.Join(",",members)+"}"));
        }
        public static string Json<T>(T value)
        {
            using(var data=new MemoryStream()) {
                new DataContractJsonSerializer(typeof(T),new DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true}).WriteObject(data,value);
                return Encoding.UTF8.GetString(data.ToArray());
            }
        }
    }
    public sealed class IdentityLabels
    {
        readonly string path; readonly Action<string> log;
        DateTime next; IdentitySample labels;
        public IdentityLabels(string path,Action<string> log){this.path=path;this.log=log;}
        public IdentitySample Sample(string nativeName,DateTime now,string nativeDisplayName=null)
        {
            if(now>=next) {
                next=now.AddMilliseconds(500);
                try {
                    var bytes=File.ReadAllBytes(path);if(bytes.Length>8192)throw new InvalidDataException("identity config too large");
                    using(var data=new MemoryStream(bytes)) {
                        var loaded=(IdentitySample)new DataContractJsonSerializer(typeof(IdentitySample),new DataContractJsonSerializerSettings{UseSimpleDictionaryFormat=true}).ReadObject(data);
                        loaded.NativeName=nativeName;loaded.NativeDisplayName=nativeDisplayName;loaded.Validate();labels=loaded;
                    }
                } catch(Exception ex){log("Identity labels invalid; previous observation retained: "+ex.Message);}
            }
            if(labels==null)return null;
            var sampled=new IdentitySample{Enabled=labels.Enabled,NativeName=nativeName,NativeDisplayName=nativeDisplayName,DisplayName=labels.DisplayName,Tags=labels.Tags};
            try{sampled.Validate();return sampled;}catch(Exception ex){log("Identity native sample invalid: "+ex.Message);return labels;}
        }
    }
}
