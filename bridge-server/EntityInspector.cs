using System.Text.Json.Nodes;

namespace MC7DTD.Bridge;

/// <summary>Read-only diagnostic mirror of accepted events. Never applies registry/component events.</summary>
public sealed class EntityInspector(EntityRegistry legacy, NativeEntityRegistry native, HealthComponents health, PresentationComponent presentation, EquipmentComponents? equipment = null)
{
    private readonly Dictionary<EntityKey, JsonObject> snapshots = new();
    private static EntityKey Key(JsonObject m) => new(Text(m,"source"),Text(m,"world_id"),Text(m,"dimension"),Text(m,"entity_id"));
    private static string Text(JsonObject m,string n) => m[n]!.GetValue<string>();
    public void ObserveAccepted(JsonObject entity)
    {
        var key=Key(entity);
        if (entity["lifecycle"]!["event"]!.GetValue<string>()=="despawn") snapshots.Remove(key);
        else snapshots[key]=entity.DeepClone().AsObject();
    }
    public void Clear() => snapshots.Clear();
    private bool Active(EntityKey key, JsonObject e) => e["version"]!.GetValue<int>()==2
        ? native.MatchesActive(key,Text(e,"entity_type"),Text(e,"stream_id"),e["sequence"]!.GetValue<long>())
        : legacy.Find(key) is {} record && record.StreamId==Text(e,"stream_id") && record.Sequence==e["sequence"]!.GetValue<long>();
    public IReadOnlyList<JsonObject> List() => snapshots.Where(pair=>Active(pair.Key,pair.Value))
        .OrderBy(pair=>pair.Key.Source,StringComparer.Ordinal).ThenBy(pair=>pair.Key.World,StringComparer.Ordinal)
        .ThenBy(pair=>pair.Key.Dimension,StringComparer.Ordinal).ThenBy(pair=>pair.Key.Id,StringComparer.Ordinal)
        .Select(pair=>Read(pair.Key,pair.Value)).ToArray();
    public IReadOnlyList<JsonObject> Find(string id,string? source=null,string? world=null,string? dimension=null) => List()
        .Where(e=>Text(e,"id")==id && (source==null || Text(e,"source")==source) && (world==null || Text(e,"world_id")==world)
            && (dimension==null || Text(e,"dimension")==dimension)).ToArray();
    private JsonObject Read(EntityKey key,JsonObject e)
    {
        var observed=health.Get(key)?.Components;
        var identity=new JsonObject();
        if(observed?["name"] is JsonObject name)
        { identity["name"]=name["text"]?.DeepClone(); if(name["display_name"]!=null)identity["display_name"]=name["display_name"]!.DeepClone(); }
        if(observed?["custom_metadata"]!=null)identity["metadata"]=observed["custom_metadata"]!.DeepClone();
        var result=new JsonObject{["id"]=key.Id,["type"]=Text(e,"entity_type"),["source"]=key.Source,
            ["world_id"]=key.World,["dimension"]=key.Dimension,["stream_id"]=Text(e,"stream_id"),["sequence"]=e["sequence"]!.DeepClone(),
            ["position"]=e["position"]?.DeepClone(),["rotation"]=e["rotation"]?.DeepClone(),["metadata"]=e["metadata"]?.DeepClone(),
            ["components"]=new JsonObject{["identity"]=identity,["health"]=observed?["health"]?.DeepClone() ?? new JsonObject(),
                ["presentation"]=presentation.Get(key) ?? new JsonObject(),
                ["authority"]=new JsonObject{["source"]=key.Source,["owner"]=e["authority"]?.DeepClone() ?? JsonValue.Create(key.Source),["origin"]=e["origin"]?.DeepClone()}}};
        if(equipment!=null)result["components"]!["equipment"]=equipment.Get(key) ?? new JsonObject();
        return result;
    }
    public static string Format(JsonObject entity) => "======== Entity Inspector ========\n"+entity.ToJsonString(new(){WriteIndented=true})+"\n==================================";
}
