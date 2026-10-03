using System.Text.Json.Nodes;

namespace MC7DTD.Bridge;

public readonly record struct EntityKey(string Source, string World, string Dimension, string Id);
public sealed record EntityRecord(EntityKey Key, string Type, string Status, string StreamId,
    long Sequence, DateTimeOffset LastUpdate, JsonObject State);
public sealed record EntityTransition(bool Forward, string Code);

/// <summary>In-memory source records only. No game object ownership or creation.</summary>
public sealed class EntityRegistry(int capacity = 4096)
{
    private readonly object gate = new();
    private readonly Dictionary<EntityKey, EntityRecord> records = new();
    public int Count { get { lock (gate) return records.Count; } }
    public EntityRecord? Find(EntityKey key)
    {
        lock (gate) return records.TryGetValue(key, out var record)
            ? record with { State = (JsonObject)record.State.DeepClone() } : null;
    }
    // Input must first pass EntityTransport validation and coordinate mapping.
    public EntityTransition Apply(JsonObject state)
    {
        var key = new EntityKey(Value("source"), Value("world_id"), Value("dimension"), Value("entity_id"));
        string Value(string name) => state[name]!.GetValue<string>();
        var type = Value("entity_type"); var stream = Value("stream_id");
        var action = state["lifecycle"]!["event"]!.GetValue<string>();
        lock (gate)
        {
            records.TryGetValue(key, out var current);
            if (current != null && current.Type != type) return new(false, "entity_type_mismatch");
            if (current != null && current.StreamId != stream) return new(false, "entity_stream_mismatch");
            switch (action)
            {
                case "spawn":
                    if (current != null) return new(false, "duplicate_spawn");
                    if (records.Count >= capacity) return new(false, "entity_registry_full");
                    break;
                case "update":
                    if (current == null) return new(false, "entity_not_found");
                    break;
                case "despawn":
                    if (current == null) return new(false, "unknown_despawn");
                    records.Remove(key);
                    return new(true, "despawned");
                default: throw new InvalidOperationException("Validated canonical lifecycle event required");
            }
            records[key] = new EntityRecord(key, type, "active", stream,
                state["sequence"]!.GetValue<long>(), DateTimeOffset.UtcNow, (JsonObject)state.DeepClone());
            return new(true, action == "spawn" ? "spawned" : "updated");
        }
    }
    public int Clear()
    {
        lock (gate) { var removed = records.Count; records.Clear(); return removed; }
    }
}
