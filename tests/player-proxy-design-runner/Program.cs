using System.Text.Json;
using System.Text.Json.Nodes;
using MC7DTD.Bridge;

// Offline fixtures exercise existing production validation/mapping/registry only.
var root = Path.GetFullPath(args.Single());
var folder = Path.Combine(root, "docs", "examples", "player_proxy");
var output = Path.Combine(root, "work", "phase3_5_0-test");
Directory.CreateDirectory(output);
var passed = new List<string>();
var failed = new List<string>();
void Check(bool condition, string name)
{
    (condition ? passed : failed).Add(name);
    Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
}
JsonObject Read(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(folder, name)))!.AsObject();
JsonObject Prepare(JsonObject state, CoordinateMapper mapper)
{
    using var document = JsonDocument.Parse(state.ToJsonString());
    return EntityTransport.Prepare(document.RootElement, "minecraft", mapper);
}
void Reject(JsonObject state, string code)
{
    try { Prepare(state, new CoordinateMapper(1, 0, 0, 0)); Check(false, "reject " + code); }
    catch (InvalidDataException ex) { Check(ex.Message == code, "reject " + code); }
}
try
{
    var fixture = Read("mapping_expectations.json");
    var mapping = fixture["coordinate"]!;
    var mapper = new CoordinateMapper(mapping["scale"]!.GetValue<double>(),
        mapping["offsetX"]!.GetValue<double>(), mapping["offsetY"]!.GetValue<double>(), mapping["offsetZ"]!.GetValue<double>());
    var registry = new EntityRegistry();
    EntityKey? identity = null;
    string? stream = null;
    var sequence = 0L;
    foreach (var (action, index) in new[] { ("spawn", 0), ("update", 1), ("despawn", 2) })
    {
        var state = Read(action + ".json");
        var before = state.ToJsonString();
        var key = new EntityKey(state["source"]!.GetValue<string>(), state["world_id"]!.GetValue<string>(),
            state["dimension"]!.GetValue<string>(), state["entity_id"]!.GetValue<string>());
        identity ??= key;
        stream ??= state["stream_id"]!.GetValue<string>();
        Check(key == identity && state["stream_id"]!.GetValue<string>() == stream, action + " stable identity and stream");
        var next = state["sequence"]!.GetValue<long>();
        Check(next > sequence, action + " example sequence increases (no ordering feature added)");
        sequence = next;
        var actual = Prepare(state, mapper);
        Check(JsonNode.DeepEquals(actual, fixture["forwarded"]![index]), action + " non-default mapped output matches fixture");
        Check(Prepare(state, new CoordinateMapper(1, 0, 0, 0))["position"]?.ToJsonString() ==
            (state["position"] is null ? null : WithTargetSpace(state["position"]!).ToJsonString()), action + " default mapping");
        Check(actual["entity_type"]!.GetValue<string>() == "minecraft:player" &&
            JsonNode.DeepEquals(actual["rotation"], state["rotation"]) && JsonNode.DeepEquals(actual["metadata"], state["metadata"]),
            action + " source type rotation metadata retained");
        Check(state.ToJsonString() == before, action + " input fixture unchanged");
        Check(registry.Apply(actual).Forward && registry.Count == (action == "despawn" ? 0 : 1), action + " existing registry transition");
        if (action != "despawn")
            Check(registry.Find(key)!.Type == "minecraft:player", action + " registry retains source player type");
    }
    var invalid = Read("spawn.json"); invalid["entity_type"] = "7dtd:player_proxy";
    Reject(invalid, "invalid_entity_type");
    invalid = Read("spawn.json"); invalid["position"]!["space"] = "7dtd";
    Reject(invalid, "invalid_entity_space");
    invalid = Read("spawn.json"); invalid["capabilities"] = new JsonObject();
    Reject(invalid, "invalid_entity_fields");
    invalid = Read("despawn.json"); invalid["metadata"] = new JsonObject { ["display_name"] = "ProxyDemo" };
    Reject(invalid, "invalid_entity_metadata");
    var reset = new EntityRegistry(); reset.Apply(Prepare(Read("spawn.json"), mapper));
    Check(reset.Clear() == 1 && reset.Count == 0, "existing reset clears player records without game objects");
    Check(reset.Apply(Prepare(Read("update.json"), mapper)).Code == "entity_not_found", "update after reset cannot restore player record");
}
catch (Exception ex) { failed.Add(ex.ToString()); Console.WriteLine(ex); }
File.WriteAllText(Path.Combine(output, "example-results.json"),
    JsonSerializer.Serialize(new { passed, failed }, new JsonSerializerOptions { WriteIndented = true }));
return failed.Count == 0 ? 0 : 1;

static JsonNode WithTargetSpace(JsonNode position)
{
    var copy = position.DeepClone(); copy["space"] = "7dtd"; return copy;
}
