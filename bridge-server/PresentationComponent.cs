using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MC7DTD.Bridge;

/// <summary>Independent presentation state; existing ownership and lifecycle validators remain authoritative.</summary>
public sealed class PresentationComponent
{
    private readonly Dictionary<string, JsonObject> defaults = new();
    private readonly Dictionary<EntityKey, JsonObject> states = new();
    public int Count => states.Count;
    public static JsonObject Fallback() => new() { ["renderer"] = "unknown", ["model"] = "default", ["scale"] = 1.0 };
    public PresentationComponent(string catalogPath)
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
        foreach (var type in catalog.RootElement.GetProperty("types").EnumerateArray())
            if (type.TryGetProperty("presentation_default", out var value))
            {
                Validate(value);
                defaults.Add(type.GetProperty("entity_type").GetString()!, Merge(Fallback(), JsonNode.Parse(value.GetRawText())!.AsObject()));
            }
    }
    public static void Validate(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("invalid_presentation");
        var seen = new HashSet<string>();
        foreach (var field in value.EnumerateObject())
        {
            if (!seen.Add(field.Name)) throw new InvalidDataException("duplicate_presentation_field");
            if (field.Name == "scale")
            {
                if (field.Value.ValueKind != JsonValueKind.Number || !field.Value.TryGetDouble(out var number) ||
                    !double.IsFinite(number) || number <= 0 || number > 16) throw new InvalidDataException("invalid_presentation_scale");
            }
            else if (field.Name is "renderer" or "model" or "variant")
            {
                if (field.Value.ValueKind != JsonValueKind.String || !Regex.IsMatch(field.Value.GetString()!, @"\A[a-z0-9_.:-]{1,64}\z"))
                    throw new InvalidDataException("invalid_presentation_identifier");
            }
            else throw new InvalidDataException("unknown_presentation_field");
        }
    }
    public static JsonObject Prepare(JsonElement input, string role, CoordinateMapper mapper)
    {
        // Validate the extension before the original transport checks authority/source/origin.
        JsonNode? extension = null;
        if (input.TryGetProperty("components", out var components))
        {
            if (components.ValueKind != JsonValueKind.Object) throw new InvalidDataException("invalid_presentation_components");
            var fields = components.EnumerateObject().ToArray();
            if (fields.Length != 1 || fields[0].Name != "presentation") throw new InvalidDataException("invalid_presentation_components");
            if (!input.TryGetProperty("lifecycle", out var life) || life.ValueKind != JsonValueKind.Object || !life.TryGetProperty("event", out var e) || e.ValueKind != JsonValueKind.String) throw new InvalidDataException("invalid_lifecycle");
            var action = e.GetString();
            if (action is "despawn" or "remove") throw new InvalidDataException("presentation_on_despawn");
            if (fields[0].Value.ValueKind == JsonValueKind.Null)
            { if (action != "update") throw new InvalidDataException("presentation_remove_requires_update"); }
            else Validate(fields[0].Value);
            extension = JsonNode.Parse(components.GetRawText());
        }
        if (input.EnumerateObject().Count(p => p.Name == "components") > 1) throw new InvalidDataException("duplicate_presentation_components");
        // Do not mask duplicates in the existing protocol by parsing and reserializing the core.
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in input.EnumerateObject()) if (property.Name != "components") property.WriteTo(writer);
            writer.WriteEndObject();
        }
        using var core = JsonDocument.Parse(buffer.ToArray());
        var v2 = input.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n == 2;
        var result = v2 ? EntityTransportV2.Prepare(core.RootElement, role, mapper) : EntityTransport.Prepare(core.RootElement, role, mapper);
        if (extension != null) result["components"] = extension;
        return result;
    }
    private static EntityKey Key(JsonObject entity) => new(entity["source"]!.GetValue<string>(), entity["world_id"]!.GetValue<string>(),
        entity["dimension"]!.GetValue<string>(), entity["entity_id"]!.GetValue<string>());
    private static JsonObject Merge(JsonObject baseline, JsonObject delta)
    { var value = baseline.DeepClone().AsObject(); foreach (var field in delta) value[field.Key] = field.Value!.DeepClone(); return value; }
    // Resolve is pure: rejected lifecycle events cannot mutate presentation state.
    public JsonObject Resolve(JsonObject entity)
    {
        var result = entity.DeepClone().AsObject();
        var action = entity["lifecycle"]!["event"]!.GetValue<string>();
        if (action == "despawn") return result;
        var key = Key(entity);
        var present = entity["components"] is JsonObject extension && extension.ContainsKey("presentation");
        JsonObject? value;
        if (action == "spawn") value = defaults.TryGetValue(entity["entity_type"]!.GetValue<string>(), out var configured) ? configured : Fallback();
        else value = states.TryGetValue(key, out var stored) ? stored : null;
        if (present) value = entity["components"]!["presentation"] is JsonObject delta ? Merge(value ?? Fallback(), delta) : null;
        if (value != null || present) result["components"] = new JsonObject { ["presentation"] = value?.DeepClone() };
        return result;
    }
    // Called only after EntityRegistry / NativeEntityRegistry accepts the same resolved event.
    public void Commit(JsonObject entity)
    {
        var key = Key(entity);
        if (entity["lifecycle"]!["event"]!.GetValue<string>() == "despawn" || entity["components"]?["presentation"] == null) states.Remove(key);
        else states[key] = entity["components"]!["presentation"]!.DeepClone().AsObject();
    }
    public JsonObject? Get(EntityKey key) => states.TryGetValue(key, out var state) ? state.DeepClone().AsObject() : null;
    public void Clear() => states.Clear();
}
