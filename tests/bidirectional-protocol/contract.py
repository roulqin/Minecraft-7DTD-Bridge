"""Offline executable specification for the v2 draft, never imported by runtime."""
import copy
import json
import math
from pathlib import Path
from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = json.loads((ROOT / "docs/entity_state_v2.schema.json").read_text(encoding="utf-8"))
Draft202012Validator.check_schema(SCHEMA)
VALIDATOR = Draft202012Validator(SCHEMA)
ROLES = {"minecraft", "7dtd"}


def decode_json(text):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate_json_key")
            result[key] = value
        return result

    def invalid_constant(value):
        raise ValueError("nonfinite_json_number")

    return json.loads(text,
                      object_pairs_hook=unique, parse_constant=invalid_constant)


def load(path):
    return decode_json(Path(path).read_text(encoding="utf-8-sig"))


def decode_wire(text):
    if len(text.encode("utf-8")) > 8192:
        raise ValueError("message_too_large")
    result = decode_json(text)
    finite_tree(result)
    return result


def finite_tree(value, depth=1):
    if depth > 8:
        raise ValueError("message_depth")
    if isinstance(value, float) and not math.isfinite(value):
        raise ValueError("nonfinite_number")
    if isinstance(value, dict):
        for child in value.values():
            finite_tree(child, depth + 1)
    elif isinstance(value, list):
        for child in value:
            finite_tree(child, depth + 1)


def validate(message, sender_role, expected_stream, forwarded=False):
    errors = list(VALIDATOR.iter_errors(message))
    if errors:
        raise ValueError("schema: " + errors[0].message)
    if type(message["version"]) is not int or type(message["sequence"]) is not int:
        raise ValueError("integer_required")
    finite_tree(message)
    if len(json.dumps(message, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")) > 8192:
        raise ValueError("message_too_large")
    source, authority, origin = message["source"], message["authority"], message["origin"]
    if sender_role not in ROLES or source != sender_role:
        raise ValueError("source_session_mismatch")
    if source != authority or authority != origin["game"]:
        raise ValueError("non_authoritative_writer")
    if message["stream_id"] != expected_stream:
        raise ValueError("inactive_stream")
    if any(message[key] != origin[key] for key in ("world_id", "dimension", "entity_id")):
        raise ValueError("origin_identity_mismatch")
    if not message["entity_type"].startswith(origin["game"] + ":") or not message["dimension"].startswith(origin["game"] + ":"):
        raise ValueError("origin_namespace_mismatch")
    if message["position"] is not None:
        expected_space = ("7dtd" if source == "minecraft" else "minecraft") if forwarded else source
        if message["position"]["space"] != expected_space:
            raise ValueError("coordinate_space_mismatch")
    metadata = message["metadata"]
    if "health" in metadata and "max_health" in metadata and metadata["health"] > metadata["max_health"]:
        raise ValueError("invalid_health_range")
    return message


def map_snapshot(message, sender_role, expected_stream, coordinate):
    validate(message, sender_role, expected_stream)
    if set(coordinate) != {"scale", "offsetX", "offsetY", "offsetZ"} or any(
            type(value) not in (float, int) or not math.isfinite(value) for value in coordinate.values()):
        raise ValueError("invalid_coordinate_config")
    scale = coordinate["scale"]
    if scale <= 0:
        raise ValueError("nonpositive_entity_scale")
    result = copy.deepcopy(message)
    position = result["position"]
    if position is not None:
        for axis in "xyz":
            offset = coordinate["offset" + axis.upper()]
            position[axis] = (position[axis] * scale + offset) if sender_role == "minecraft" else (position[axis] - offset) / scale
            if not math.isfinite(position[axis]):
                raise ValueError("mapping_overflow")
        position["space"] = "7dtd" if sender_role == "minecraft" else "minecraft"
    validate(result, sender_role, expected_stream, forwarded=True)
    return result


def key(message):
    origin = message["origin"]
    return tuple(origin[field] for field in ("game", "world_id", "dimension", "entity_id"))


class OwnershipOracle:
    """Tests-only conflict table. Not a new EntityRegistry or Bridge service."""
    def __init__(self):
        self.streams = {}
        self.records = {}

    def begin_session(self, role, stream):
        if role not in ROLES:
            raise ValueError("unknown_role")
        self.records = {k: v for k, v in self.records.items() if k[0] != role}
        self.streams[role] = stream

    def apply(self, message, role):
        validate(message, role, self.streams.get(role))
        identity = key(message)
        previous = self.records.get(identity)
        event, sequence = message["lifecycle"]["event"], message["sequence"]
        if previous:
            if previous["entity_type"] != message["entity_type"]:
                return "reject_type_change"
            if sequence <= previous["sequence"]:
                return "ignore_stale_or_duplicate"
        if event == "spawn":
            if previous and previous["active"]:
                return "ignore_duplicate_spawn"  # No state overwrite, no high-water change.
            if previous:
                return "reject_retired_identity"  # New observation requires a fresh ID.
        elif event == "update":
            if not previous or not previous["active"]:
                return "reject_unknown_or_retired_update"
        # Unknown despawn records a tombstone but never creates a game object.
        self.records[identity] = {"sequence": sequence, "active": event != "despawn",
                                  "entity_type": message["entity_type"], "state": copy.deepcopy(message)}
        return "accept_" + event

    @property
    def active_count(self):
        return sum(record["active"] for record in self.records.values())
