using System.Text.Json.Nodes;

namespace MC7DTD.Bridge;

/// <summary>v2 ownership/session state, independent of the legacy v1 registry.</summary>
public sealed class NativeEntityRegistry(int capacity = 4096)
{
    private sealed record State(string Type, long Sequence, bool Active);
    private readonly Dictionary<EntityKey, State> records = new();
    private string? stream;
    public int Count => records.Values.Count(value => value.Active);
    public bool MatchesActive(EntityKey key, string type, string incomingStream, long sequence) =>
        stream == incomingStream && records.TryGetValue(key, out var state) && state.Active && state.Type == type && state.Sequence == sequence;
    public EntityTransition Apply(JsonObject message)
    {
        string Text(string name) => message[name]!.GetValue<string>();
        var key = new EntityKey(Text("source"), Text("world_id"), Text("dimension"), Text("entity_id"));
        var incomingStream = Text("stream_id"); var type = Text("entity_type");
        var action = message["lifecycle"]!["event"]!.GetValue<string>();
        var sequence = message["sequence"]!.GetValue<long>();
        if (stream != null && stream != incomingStream) return new(false, "entity_stream_mismatch");
        records.TryGetValue(key, out var current);
        if (current != null && current.Type != type) return new(false, "entity_type_mismatch");
        if (current != null && sequence <= current.Sequence) return new(false, "stale_entity_sequence");
        if (action == "spawn" && current != null)
            return new(false, current.Active ? "duplicate_spawn" : "retired_entity");
        if (stream == null && action != "spawn") return new(false, "entity_stream_unbound");
        if (action == "update" && (current == null || !current.Active)) return new(false, "entity_not_found");
        if (current == null && records.Count >= capacity) return new(false, "entity_registry_full");
        stream ??= incomingStream;
        records[key] = new State(type, sequence, action != "despawn");
        return new(action != "despawn" || current?.Active == true,
            action == "spawn" ? "spawned" : action == "update" ? "updated" : current?.Active == true ? "despawned" : "unknown_despawn");
    }
    public void Clear() { records.Clear(); stream = null; }
}
