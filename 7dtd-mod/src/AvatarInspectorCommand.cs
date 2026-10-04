using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MC7DTD
{
    public sealed class AvatarInspectorCommand : ConsoleCmdAbstract
    {
        public override string[] getCommands() => new[] { "mc7dtd_avatar_inspect", "mc7dtd_entity_inspect" };
        public override string getDescription() => "Inspect local Minecraft Avatar renderer state.";
        public override bool IsExecuteOnClient => true;
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            var renderer=AvatarRenderer.Current;
            if(renderer==null){SdtdConsole.Instance.Output("Avatar renderer unavailable");return;}
            if(args.Count>1){SdtdConsole.Instance.Output("Usage: mc7dtd_avatar_inspect [entity_id]");return;}
            var states=renderer.Active.Where(h=>args.Count==0 || (string)renderer.Inspect(h)["entity_id"]==args[0]).Select(renderer.Inspect);
            SdtdConsole.Instance.Output(new JArray(states).ToString(Formatting.Indented));
        }
    }
    public sealed class AvatarLocalHeldCommand : ConsoleCmdAbstract
    {
        public override string[] getCommands() => new[] { "mc7dtd_avatar_test_held" };
        public override string getDescription() => "DEBUG local held marker fixture only; not synchronized equipment.";
        public override bool IsExecuteOnClient => true;
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            if(args.Count!=2&&args.Count!=3){SdtdConsole.Instance.Output("Usage: mc7dtd_avatar_test_held <entity_id> <item_id|null> [right_hand|left_hand|back|head|body|waist] (LOCAL TEST ONLY)");return;}
            try {
                var project=Environment.GetEnvironmentVariable("MC7DTD_ROOT") ?? @"D:\wenjian\minecraft\7-M";
                var path=Path.Combine(project,"config","debug.json");
                if(!NavigationEnabled(path)){SdtdConsole.Instance.Output("Debug navigation disabled");return;}
                var renderer=AvatarRenderer.Current;
                var matches=renderer?.Active.Where(h=>(string)renderer.Inspect(h)["entity_id"]==args[0]).ToArray();
                if(matches==null||matches.Length!=1){SdtdConsole.Instance.Output("Entity not found or ambiguous: "+args[0]);return;}
                var item=args[1]=="null"?null:args[1];
                if(item!=null&&(item.Length>128||!System.Text.RegularExpressions.Regex.IsMatch(item,@"\A[a-z0-9_.-]+:[A-Za-z0-9_.-]+\z"))){SdtdConsole.Instance.Output("Invalid local fixture item id");return;}
                if(args.Count==3)matches[0].Equipment.SetSlot(args[2],item);else matches[0].Equipment.Update(matches[0].RightHand,item);
                SdtdConsole.Instance.Output("LOCAL TEST ONLY: "+renderer.Inspect(matches[0]).ToString(Formatting.Indented));
            } catch(Exception ex){SdtdConsole.Instance.Output("Local fixture rejected: "+ex.GetType().Name);}
        }
        public static bool NavigationEnabled(string path)
        {
            try {
                var text=File.ReadAllText(path);if(text.Length>4096)return false;
                using(var reader=new JsonTextReader(new StringReader(text))) {
                    var config=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                    return config["debug_navigation"]?.Type==JTokenType.Boolean&&(bool)config["debug_navigation"];
                }
            }catch{return false;}
        }
    }
}
