using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MC7DTD.Bridge;

/// <summary>Optional sidecar strategy; legacy Health/Identity validators and State API stay intact.</summary>
public sealed class EquipmentComponents(HealthComponents legacy)
{
    public static readonly string[] Slots = ["head", "body", "hands", "feet", "held_item"];
    readonly Dictionary<EntityKey, JsonObject> equipment = new();
    public JsonObject? Get(EntityKey key) => equipment.GetValueOrDefault(key)?.DeepClone().AsObject();
    public void Clear() => equipment.Clear();
    public void Retire(JsonObject e) => equipment.Remove(Key(e));
    static EntityKey Key(JsonObject e) => new(e["source"]!.GetValue<string>(),e["world_id"]!.GetValue<string>(),e["dimension"]!.GetValue<string>(),e["entity_id"]!.GetValue<string>());
    static void Fail(string code) => throw new InvalidDataException(code);
    static long Integer(JsonElement root,string field,long min)
    {
        if(!root.TryGetProperty(field,out var value) || value.ValueKind!=JsonValueKind.Number || !value.TryGetInt64(out var number) || number<min || number>9007199254740991L)Fail("invalid_component_integer");
        return root.GetProperty(field).GetInt64();
    }
    public static void Validate(JsonElement value, bool complete)
    {
        if(value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count()!=1 || !value.TryGetProperty("slots",out var slots) || slots.ValueKind!=JsonValueKind.Object) Fail("invalid_equipment");
        slots=value.GetProperty("slots");
        var seen=new HashSet<string>();
        foreach(var slot in slots.EnumerateObject()) {
            if(!Slots.Contains(slot.Name) || !seen.Add(slot.Name)) Fail("invalid_equipment_slot");
            if(slot.Value.ValueKind==JsonValueKind.Null)continue;
            if(slot.Value.ValueKind!=JsonValueKind.Object || slot.Value.EnumerateObject().Count()!=1 || !slot.Value.TryGetProperty("item_id",out var item) || item.ValueKind!=JsonValueKind.String) Fail("invalid_equipment_item");
            var id=slot.Value.GetProperty("item_id").GetString()!;
            if(id.Length>128 || !Regex.IsMatch(id,@"\A7dtd:[A-Za-z0-9_.-]+\z"))Fail("invalid_equipment_item");
        }
        if(seen.Count==0 || complete && seen.Count!=Slots.Length)Fail("incomplete_equipment");
    }
    public EntityTransition Apply(JsonElement original,string role,NativeEntityRegistry registry,ComponentPermissions permissions,EquipmentPermissions equipmentPermissions)
    {
        // Duplicate keys in the original must be checked before JsonNode can normalize them.
        Unique(original);
        string[] fields=["type","version","entity_state_version","source","authority","origin","entity_id","world_id","dimension","entity_type","stream_id","entity_sequence","revision","base_revision","mode","components"];
        if(original.ValueKind!=JsonValueKind.Object || original.EnumerateObject().Count()!=fields.Length || fields.Any(f=>!original.TryGetProperty(f,out _)))Fail("invalid_component_fields");
        foreach(var field in new[]{"source","world_id","dimension","entity_id","entity_type","mode"})if(original.GetProperty(field).ValueKind!=JsonValueKind.String)Fail("invalid_component_fields");
        if(original.GetProperty("components").ValueKind!=JsonValueKind.Object)Fail("invalid_component_fields");
        var m=JsonNode.Parse(original.GetRawText())!.AsObject();
        var key=Key(m);var previous=legacy.Get(key);
        var components=original.GetProperty("components");
        var mode=original.GetProperty("mode").GetString();
        if(mode is not ("snapshot" or "patch"))Fail("invalid_component_mode");
        var snapshot=mode=="snapshot";
        bool supplied=components.TryGetProperty("equipment",out var value);
        if(supplied || snapshot && equipment.ContainsKey(key)) {
            if(role!="7dtd" || m["entity_type"]!.GetValue<string>()!="7dtd:player" || !equipmentPermissions.Allows("7dtd:player"))Fail("equipment_permission_denied");
        }
        JsonObject? candidate=Get(key);
        if(supplied) {
            if(value.ValueKind==JsonValueKind.Null) { if(snapshot)Fail("invalid_equipment_snapshot");candidate=null; }
            else {
                Validate(value,snapshot || candidate==null);
                candidate=snapshot || candidate==null ? JsonNode.Parse(value.GetRawText())!.AsObject() : candidate;
                if(!snapshot)foreach(var slot in value.GetProperty("slots").EnumerateObject())candidate["slots"]![slot.Name]=JsonNode.Parse(slot.Value.GetRawText());
            }
        } else if(snapshot)candidate=null;
        var projected=m.DeepClone().AsObject();projected["components"]!.AsObject().Remove("equipment");
        // Equipment-only patch: validate its real dependency first, then ask the unchanged
        // legacy reducer to accept a complete, identical legacy view at the shared revision.
        if(!snapshot && supplied && projected["components"]!.AsObject().Count==0) {
            long revision=Integer(original,"revision",1),baseline=Integer(original,"base_revision",1);
            if(previous==null || revision>previous.Revision && baseline!=previous.Revision)Fail("component_baseline_mismatch");
            projected["mode"]="snapshot";projected["base_revision"]=0;
            projected["components"]=previous!.Components.DeepClone();
        }
        using var document=JsonDocument.Parse(projected.ToJsonString());
        var accepted=legacy.Apply(document.RootElement,role,registry,permissions);
        if(!accepted.Forward)return accepted;
        if(candidate==null)equipment.Remove(key);else equipment[key]=candidate;
        return supplied ? new(true,"equipment_applied") : accepted;
    }
    static void Unique(JsonElement e) {
        if(e.ValueKind==JsonValueKind.Object){var seen=new HashSet<string>();foreach(var p in e.EnumerateObject()){if(!seen.Add(p.Name))Fail("duplicate_component_field");Unique(p.Value);}}
        else if(e.ValueKind==JsonValueKind.Array)foreach(var v in e.EnumerateArray())Unique(v);
    }
}

public sealed class EquipmentPermissions
{
    readonly HashSet<string> allowed=new();
    public EquipmentPermissions(string directory)
    {
        var path=Path.Combine(directory,"equipment_permissions.json");
        if(!File.Exists(path))return; // Missing optional permission defaults to deny.
        using var policy=JsonDocument.Parse(File.ReadAllText(path));
        using var catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"entity_types.json")));
        if(policy.RootElement.GetProperty("version").GetInt32()!=1)throw new InvalidDataException("invalid_equipment_policy");
        var seen=new HashSet<string>();
        foreach(var entry in policy.RootElement.GetProperty("types").EnumerateArray()) {
            var type=entry.GetProperty("entity_type").GetString()!;
            if(!seen.Add(type) || type!="7dtd:player" || entry.GetProperty("source").GetString()!="7dtd" || entry.GetProperty("target").GetString()!="minecraft")throw new InvalidDataException("invalid_equipment_route");
            var enabled=catalog.RootElement.GetProperty("types").EnumerateArray().Any(t=>t.GetProperty("entity_type").GetString()==type && t.GetProperty("enabled").GetBoolean() && t.GetProperty("target_mapping").GetProperty("game").GetString()=="minecraft");
            var grant=entry.GetProperty("equipment");
            if(enabled && grant.GetProperty("publish").ValueKind==JsonValueKind.True && grant.GetProperty("store").ValueKind==JsonValueKind.True)allowed.Add(type);
        }
    }
    public bool Allows(string type)=>allowed.Contains(type);
}
