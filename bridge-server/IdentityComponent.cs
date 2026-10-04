using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
namespace MC7DTD.Bridge;

public static class IdentityComponent
{
    public static void ValidateName(JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.Object || !value.TryGetProperty("text",out _)
            || value.EnumerateObject().Any(p=>p.Name is not ("text" or "display_name"))) Fail("invalid_component_name");
        ValidateText(value.GetProperty("text"),128);
        if(value.TryGetProperty("display_name",out var display)) ValidateText(display,128);
    }
    public static void ValidateText(JsonElement value,int limit)
    {
        if(value.ValueKind!=JsonValueKind.String) Fail("invalid_component_name");
        var text=value.GetString()!;
        // Unicode scalar count, and reject unmatched UTF-16 surrogates as well as C0/C1 controls.
        var count=0;
        for(int i=0;i<text.Length;i++) {
            var c=text[i]; if(c<32 || c>=127 && c<=159) Fail("invalid_component_name");
            if(char.IsHighSurrogate(c)){if(i+1>=text.Length||!char.IsLowSurrogate(text[++i]))Fail("invalid_component_name");}
            else if(char.IsLowSurrogate(c))Fail("invalid_component_name");
            count++;
        }
        if(count<1 || count>limit) Fail("invalid_component_name");
    }
    public static void ValidateMetadata(JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.Object || value.EnumerateObject().Count()>32) Fail("invalid_component_metadata");
        foreach(var p in value.EnumerateObject()) {
            if(p.Name.Length is <1 or >64 || !Regex.IsMatch(p.Name,@"\A[a-zA-Z][a-zA-Z0-9_.-]*\z")) Fail("invalid_component_metadata");
            if(p.Name.StartsWith("tag.",StringComparison.Ordinal)) {
                if(p.Name.Length==4 || p.Value.ValueKind!=JsonValueKind.String) Fail("invalid_component_metadata");
                ValidateText(p.Value,256); continue;
            }
            switch(p.Value.ValueKind) {
                case JsonValueKind.String: if(p.Value.GetString()!.EnumerateRunes().Count()>256) Fail("invalid_component_metadata"); break;
                case JsonValueKind.Number: if(!p.Value.TryGetDouble(out var n)||!double.IsFinite(n))Fail("invalid_component_metadata"); break;
                case JsonValueKind.True: case JsonValueKind.False: case JsonValueKind.Null: break;
                default: Fail("invalid_component_metadata"); break;
            }
        }
    }
    static void Fail(string code)=>throw new InvalidDataException(code);
}
