using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MC7DTD.Bridge;

// Separate observation permissions: never enables proxy damage or combat capabilities.
public sealed class ComponentPermissions
{
    readonly HashSet<string> allowed = new(StringComparer.Ordinal);
    public ComponentPermissions(JsonElement policy, JsonElement catalog)
    {
        if (policy.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("invalid_component_policy");
        var ids = new HashSet<string>();
        foreach (var entry in policy.GetProperty("types").EnumerateArray())
        {
            var type = entry.GetProperty("entity_type").GetString()!;
            if (!ids.Add(type)) throw new InvalidDataException("duplicate_component_policy");
            if (entry.GetProperty("source").GetString() != "7dtd" || entry.GetProperty("target").GetString() != "minecraft"
                || !type.StartsWith("7dtd:", StringComparison.Ordinal)) throw new InvalidDataException("unsupported_component_route");
            var enabled = catalog.GetProperty("types").EnumerateArray().Any(t => t.GetProperty("entity_type").GetString() == type
                && t.GetProperty("enabled").GetBoolean() && t.GetProperty("target_mapping").GetProperty("game").GetString() == "minecraft");
            var components = entry.GetProperty("components");
            foreach (var item in components.EnumerateObject())
            {
                if (item.Name is not ("health" or "name" or "custom_metadata")) throw new InvalidDataException("unsupported_component_permission");
                var grant = item.Value;
                if (enabled && grant.TryGetProperty("publish", out var publish) && publish.ValueKind == JsonValueKind.True
                    && grant.TryGetProperty("store", out var store) && store.ValueKind == JsonValueKind.True) allowed.Add(type + "|" + item.Name);
            }
        }
    }
    public bool Allows(string type, string component = "health") => allowed.Contains(type + "|" + component);
    public static ComponentPermissions Load(string directory)
    {
        using var policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "component_permissions.json")));
        using var types = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "entity_types.json")));
        return new(policy.RootElement, types.RootElement);
    }
}

public sealed class HealthComponents
{
    public sealed record State(long Revision, double? Current, double? Max, JsonObject Components);
    readonly Dictionary<EntityKey, State> records = new();
    public int Count => records.Count;
    public State? Get(EntityKey key) => records.GetValueOrDefault(key);
    public void Clear() => records.Clear();
    public void Retire(JsonObject entity) => records.Remove(Key(entity));
    static EntityKey Key(JsonObject m) => new(m["source"]!.GetValue<string>(), m["world_id"]!.GetValue<string>(), m["dimension"]!.GetValue<string>(), m["entity_id"]!.GetValue<string>());

    // Called under the same lifecycle gate as NativeEntityRegistry.Apply and forwarding.
    public EntityTransition Apply(JsonElement root, string role, NativeEntityRegistry entities, ComponentPermissions permissions)
    {
        Unique(root);
        Fields(root, "type", "version", "entity_state_version", "source", "authority", "origin", "entity_id", "world_id", "dimension", "entity_type", "stream_id", "entity_sequence", "revision", "base_revision", "mode", "components");
        if (Text(root,"type") != "entity_components" || Integer(root,"version",1,1) != 1 || Integer(root,"entity_state_version",2,2) != 2) Fail("invalid_component_version");
        if (role != "7dtd" || Text(root,"source") != role || Text(root,"authority") != role) Fail("invalid_component_authority");
        var origin = root.GetProperty("origin"); Fields(origin,"game","world_id","dimension","entity_id");
        if (Text(origin,"game") != role || new[]{"world_id","dimension","entity_id"}.Any(k => Text(origin,k) != Text(root,k))) Fail("invalid_component_origin");
        foreach (var k in new[]{"stream_id","entity_id"}) { var id=Text(root,k); if (!Guid.TryParseExact(id,"D",out _) || id != id.ToLowerInvariant()) Fail("invalid_component_id"); }
        var type=Text(root,"entity_type"); var dimension=Text(root,"dimension"); var world=Text(root,"world_id");
        if (type.Length>128 || dimension.Length>128 || !Regex.IsMatch(type,@"\A7dtd:[a-z0-9_./-]+\z") || !Regex.IsMatch(dimension,@"\A7dtd:[a-z0-9_./-]+\z")
            || world.Length is <1 or >128 || !Regex.IsMatch(world,@"\A[a-zA-Z0-9_.-]+\z")) Fail("invalid_component_identity");
        var sequence=Integer(root,"entity_sequence",1); var revision=Integer(root,"revision",1); var baseline=Integer(root,"base_revision",0);
        var key=new EntityKey(role,world,dimension,Text(root,"entity_id"));
        if (!entities.MatchesActive(key,type,Text(root,"stream_id"),sequence)) Fail("component_entity_mismatch");
        var mode=Text(root,"mode"); if (mode is not ("snapshot" or "patch")) Fail("invalid_component_mode");
        var components=root.GetProperty("components");
        if (components.ValueKind != JsonValueKind.Object || components.EnumerateObject().Any(p=>p.Name is not ("health" or "name" or "custom_metadata"))) Fail("unsupported_component");
        records.TryGetValue(key,out var old);
        var affected = components.EnumerateObject().Select(p=>p.Name);
        if (mode=="snapshot" && old!=null) affected = affected.Concat(old.Components.Select(p=>p.Key));
        if (affected.Any(name=>!permissions.Allows(type,name))) Fail("component_permission_denied");
        bool hasHealth=components.TryGetProperty("health",out var health);
        double? current=null, max=null;
        if (hasHealth && health.ValueKind != JsonValueKind.Null)
        { Fields(health,"current","max"); current=Number(health,"current"); max=Number(health,"max"); if(current<0 || max<=0 || current>max) Fail("invalid_component_health"); }
        foreach (var item in components.EnumerateObject())
        {
            if (item.Value.ValueKind==JsonValueKind.Null) { if(mode=="snapshot") Fail("invalid_component_snapshot"); continue; }
            if(item.Name=="name") IdentityComponent.ValidateName(item.Value);
            if(item.Name=="custom_metadata") IdentityComponent.ValidateMetadata(item.Value);
        }
        if (mode=="snapshot" && baseline!=0) Fail("invalid_component_snapshot");
        if (mode=="patch" && (!components.EnumerateObject().Any() || baseline<1)) Fail("invalid_component_patch");
        // Stale packets still undergo identity, permission and content checks.
        if (old != null && revision<=old.Revision) return new(false,"stale_component_revision");
        if (mode=="patch" && (old==null || baseline!=old.Revision)) Fail("component_baseline_mismatch");
        var result = mode=="snapshot" ? new JsonObject() : old!.Components.DeepClone().AsObject();
        foreach (var item in components.EnumerateObject())
            if (item.Value.ValueKind==JsonValueKind.Null) result.Remove(item.Name); else result[item.Name]=JsonNode.Parse(item.Value.GetRawText());
        current=result["health"]?["current"]?.GetValue<double>(); max=result["health"]?["max"]?.GetValue<double>();
        records[key]=new(revision,current,max,result);
        var identityChanged = components.TryGetProperty("name",out _) || components.TryGetProperty("custom_metadata",out _) || mode=="snapshot" && old?.Components.Any(p=>p.Key!="health")==true;
        return new(true,identityChanged ? "identity_applied" : current.HasValue ? old?.Current==null ? "health_initial" : "health_updated" : "health_removed");
    }
    static string Text(JsonElement v,string k) { if(v.ValueKind!=JsonValueKind.Object || !v.TryGetProperty(k,out var p) || p.ValueKind!=JsonValueKind.String) throw new InvalidDataException("invalid_component_fields"); return p.GetString()!; }
    static long Integer(JsonElement v,string k,long min,long max=9007199254740991) { if(!v.TryGetProperty(k,out var p)||p.ValueKind!=JsonValueKind.Number||!p.TryGetInt64(out var n)||n<min||n>max) throw new InvalidDataException("invalid_component_integer");return n; }
    static double Number(JsonElement v,string k) { var p=v.GetProperty(k);if(p.ValueKind!=JsonValueKind.Number||!p.TryGetDouble(out var n)||!double.IsFinite(n)) throw new InvalidDataException("invalid_component_health");return n; }
    static void Fields(JsonElement v,params string[] keys) { if(v.ValueKind!=JsonValueKind.Object||v.EnumerateObject().Count()!=keys.Length||keys.Any(k=>!v.TryGetProperty(k,out _))) Fail("invalid_component_fields"); }
    static void Unique(JsonElement v) { if(v.ValueKind==JsonValueKind.Object){var keys=new HashSet<string>();foreach(var p in v.EnumerateObject()){if(!keys.Add(p.Name))Fail("duplicate_component_field");Unique(p.Value);}}else if(v.ValueKind==JsonValueKind.Array)foreach(var e in v.EnumerateArray())Unique(e); }
    static void Fail(string code)=>throw new InvalidDataException(code);
}
