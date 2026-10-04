"""Offline Phase 3.4 design validator. Never imported by Bridge or game Mods."""
import json
from jsonschema import Draft202012Validator

# Design-time description of the existing Phase 3.3 adapter, not a factory.
ADAPTERS = {
    ("minecraft", "block_display_proxy"): {
        "source_type": "7dtd:player",
        "target_type": "minecraft:player_proxy",
        "capabilities": {"spawn", "update_position", "update_rotation", "despawn"},
    },
    ("7dtd", "player_proxy"): {
        "source_type": "minecraft:player",
        "target_type": "7dtd:player_proxy",
        "capabilities": {"spawn", "update_position", "update_rotation", "despawn"},
    },
    ("7dtd", "marker"): {
        "source_type": "minecraft:marker",
        "target_type": "7dtd:marker",
        "capabilities": {"spawn", "update_position", "despawn"},
    }
}


def load_json(path):
    def unique_object(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"Duplicate JSON key: {key}")
            result[key] = value
        return result

    def reject_constant(value):
        raise ValueError(f"Invalid JSON constant: {value}")

    return json.loads(path.read_text(encoding="utf-8-sig"),
                      object_pairs_hook=unique_object, parse_constant=reject_constant)


def validate_catalog(catalog, schema):
    Draft202012Validator.check_schema(schema)
    errors = sorted(Draft202012Validator(schema).iter_errors(catalog),
                    key=lambda error: str(error.path))
    if errors:
        raise ValueError(errors[0].message)
    # bool compares equal to 1 in Python; configuration version must be an integer.
    if type(catalog["version"]) is not int:
        raise ValueError("Configuration version must be an integer")
    seen = set()
    for entry in catalog["types"]:
        source = entry["entity_type"].split(":", 1)[0]
        target = entry["target_mapping"]
        if source not in ("minecraft", "7dtd") or source == target["game"]:
            raise ValueError("Mapping must cross supported game namespaces")
        if target["entity_type"].split(":", 1)[0] != target["game"]:
            raise ValueError("Target namespace does not match target game")
        key = (entry["entity_type"], target["game"])
        if key in seen:
            raise ValueError("Duplicate entity type / target game mapping")
        seen.add(key)
        if not entry["enabled"]:
            continue  # Reserved definitions cannot create objects or grant capabilities.
        adapter = ADAPTERS.get((target["game"], target["adapter"]))
        if adapter is None:
            raise ValueError("Enabled mapping requires a registered adapter")
        if (entry["entity_type"] != adapter["source_type"] or
                target["entity_type"] != adapter["target_type"]):
            raise ValueError("Adapter does not support this type mapping")
        requested = {name for name, enabled in entry["capabilities"].items() if enabled}
        if not requested <= adapter["capabilities"]:
            raise ValueError("Adapter does not implement requested capabilities")
        if not {"spawn", "despawn"} <= requested:
            raise ValueError("Enabled adapter requires spawn and despawn for cleanup")
    return catalog
