using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MC7DTD.Bridge;

/// <summary>Validates and maps wire snapshots only; never creates game entities.</summary>
public static class EntityTransportV2
{
    public static JsonObject Prepare(JsonElement root, string role, CoordinateMapper mapper)
    {
        UniqueKeys(root);
        Fields(root, "type", "version", "source", "stream_id", "entity_id", "entity_type",
            "world_id", "dimension", "sequence", "lifecycle", "position", "rotation", "metadata", "authority", "origin");
        if (role != "7dtd" || Text(root, "source") != role) Fail("invalid_entity_source");
        if (Text(root, "type") != "entity_state" || root.GetProperty("version").ValueKind != JsonValueKind.Number || !root.GetProperty("version").TryGetInt32(out var version) || version != 2)
            Fail("invalid_entity_version");
        if (Text(root, "authority") != role) Fail("invalid_entity_authority");
        var origin = root.GetProperty("origin");
        Fields(origin, "game", "world_id", "dimension", "entity_id");
        if (Text(origin, "game") != role || new[] { "world_id", "dimension", "entity_id" }.Any(key => Text(origin, key) != Text(root, key)))
            Fail("invalid_entity_origin");
        foreach (var key in new[] { "stream_id", "entity_id" })
        {
            var id = Text(root, key);
            if (!Guid.TryParseExact(id, "D", out _) || id != id.ToLowerInvariant()) Fail("invalid_entity_id");
        }
        var entityType = Text(root, "entity_type");
        if (!Name(entityType) || !entityType.StartsWith(role + ":", StringComparison.Ordinal)) Fail("invalid_entity_type");
        var world = Text(root, "world_id");
        if (world.Length is < 1 or > 128 || !Regex.IsMatch(world, @"\A[a-zA-Z0-9_.-]+\z")) Fail("invalid_entity_world");
        if (!Name(Text(root, "dimension")) || !Text(root, "dimension").StartsWith(role + ":", StringComparison.Ordinal)) Fail("invalid_entity_dimension");
        if (root.GetProperty("sequence").ValueKind != JsonValueKind.Number || !root.GetProperty("sequence").TryGetInt64(out var sequence) || sequence is < 1 or > 9007199254740991)
            Fail("invalid_entity_sequence");
        var lifecycle = root.GetProperty("lifecycle");
        var action = Text(lifecycle, "event");
        if (action is not ("spawn" or "update" or "despawn")) Fail("invalid_entity_lifecycle");
        if (action == "despawn")
        {
            Fields(lifecycle, "event", "reason");
            if (Text(lifecycle, "reason") is not ("died" or "despawned" or "out_of_scope" or "world_unloaded" or "dimension_changed"))
                Fail("invalid_entity_reason");
            if (root.GetProperty("position").ValueKind != JsonValueKind.Null || root.GetProperty("rotation").ValueKind != JsonValueKind.Null)
                Fail("invalid_entity_state");
        }
        else Fields(lifecycle, "event");
        var metadata = root.GetProperty("metadata");
        if (metadata.ValueKind != JsonValueKind.Object || metadata.EnumerateObject().Count() > 32) Fail("invalid_entity_metadata");
        foreach (var item in metadata.EnumerateObject())
        {
            if (item.Name.Length is < 1 or > 64 || !Regex.IsMatch(item.Name, @"\A[a-zA-Z][a-zA-Z0-9_.-]*\z")) Fail("invalid_entity_metadata");
            switch (item.Value.ValueKind)
            {
                case JsonValueKind.String: if (item.Value.GetString()!.Length > 256) Fail("invalid_entity_metadata"); break;
                case JsonValueKind.Number: Number(metadata, item.Name); break;
                case JsonValueKind.True: case JsonValueKind.False: case JsonValueKind.Null: break;
                default: Fail("invalid_entity_metadata"); break;
            }
        }
        if (metadata.TryGetProperty("health", out _ ) && Number(metadata, "health") < 0) Fail("invalid_entity_metadata");
        if (metadata.TryGetProperty("max_health", out _) && Number(metadata, "max_health") <= 0) Fail("invalid_entity_metadata");
        if (metadata.TryGetProperty("health", out _) && metadata.TryGetProperty("max_health", out _) &&
            Number(metadata, "health") > Number(metadata, "max_health")) Fail("invalid_entity_metadata");
        foreach (var key in new[] { "display_name", "native_type" })
            if (metadata.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.String) Fail("invalid_entity_metadata");
        if (metadata.TryGetProperty("is_alive", out var alive) && alive.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            Fail("invalid_entity_metadata");
        var result = JsonNode.Parse(root.GetRawText())!.AsObject();
        result["lifecycle"]!["event"] = action;
        if (action == "despawn")
        {
            if (metadata.EnumerateObject().Any()) Fail("invalid_entity_metadata");
            return Bounded(result);
        }
        var position = root.GetProperty("position");
        Fields(position, "x", "y", "z", "space");
        if (Text(position, "space") != role) Fail("invalid_entity_space");
        if (mapper.Scale <= 0) Fail("invalid_entity_scale");
        var x = (Number(position, "x") - mapper.OffsetX) / mapper.Scale;
        var y = (Number(position, "y") - mapper.OffsetY) / mapper.Scale;
        var z = (Number(position, "z") - mapper.OffsetZ) / mapper.Scale;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) Fail("coordinate_mapping_overflow");
        var rotation = root.GetProperty("rotation");
        Fields(rotation, "yaw", "pitch", "roll");
        var yaw = Number(rotation, "yaw"); var pitch = Number(rotation, "pitch"); var roll = Number(rotation, "roll");
        if (yaw is < 0 or >= 360 || pitch is < -90 or > 90 || roll is < -180 or >= 180) Fail("invalid_entity_rotation");
        result["position"] = new JsonObject { ["x"] = x, ["y"] = y, ["z"] = z, ["space"] = "minecraft" };
        return Bounded(result);
    }

    private static JsonObject Bounded(JsonObject value)
    {
        if (JsonSerializer.SerializeToUtf8Bytes(value).Length > 8192) Fail("entity_message_too_large");
        return value;
    }

    private static bool Name(string value) => value.Length is > 0 and <= 128 && Regex.IsMatch(value, @"\A[a-z0-9_.-]+:[a-z0-9_./-]+\z");
    private static string Text(JsonElement value, string key)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var item) || item.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("invalid_entity_state");
        return item.GetString()!;
    }
    private static double Number(JsonElement value, string key)
    {
        if (!value.TryGetProperty(key, out var item) || item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new InvalidDataException("invalid_entity_number");
        return number;
    }
    private static void Fields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != names.Length || names.Any(name => !value.TryGetProperty(name, out _)))
            Fail("invalid_entity_fields");
    }
    private static void UniqueKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in value.EnumerateObject()) { if (!names.Add(item.Name)) Fail("duplicate_entity_field"); UniqueKeys(item.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) UniqueKeys(item);
    }
    private static void Fail(string code) => throw new InvalidDataException(code);
}
