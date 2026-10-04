import copy
import hashlib
import json
import sys
import tempfile
import unittest
from contract import ROOT, OwnershipOracle, decode_wire, key, load, map_snapshot, validate

EXAMPLES = ROOT / "docs/examples/entity_state_v2"
MAPPING = {"scale": 2, "offsetX": 100, "offsetY": -10, "offsetZ": 25}


def example(role="minecraft", event="spawn"):
    return load(EXAMPLES / (role + "_" + event + ".json"))


def check(message, role=None):
    return validate(message, role or message["source"], message["stream_id"])


def oracle():
    value = OwnershipOracle()
    for role in ("minecraft", "7dtd"):
        value.begin_session(role, example(role)["stream_id"])
    return value


class ProtocolTests(unittest.TestCase):
    def test_all_six_examples(self):
        for role in ("minecraft", "7dtd"):
            state = oracle()
            for event in ("spawn", "update", "despawn"):
                with self.subTest(role=role, event=event):
                    message = example(role, event)
                    self.assertEqual(decode_wire((EXAMPLES / (role + "_" + event + ".json")).read_text(encoding="utf-8")), message)
                    self.assertEqual(check(message), message)
                    self.assertEqual(state.apply(message, role), "accept_" + event)
            self.assertEqual(state.active_count, 0)

    def test_source_must_match_connection(self):
        with self.assertRaisesRegex(ValueError, "source_session"):
            check(example(), "7dtd")

    def test_authority_cannot_be_claimed_by_other_game(self):
        for field in ("source", "authority"):
            with self.subTest(field=field):
                message = example()
                message[field] = "7dtd"
                with self.assertRaises(ValueError):
                    check(message)

    def test_origin_is_fixed_source_identity(self):
        for field, value in (("game", "7dtd"), ("entity_id", example("7dtd")["entity_id"]),
                             ("world_id", "other-world"), ("dimension", "minecraft:the_nether")):
            with self.subTest(field=field):
                message = example()
                message["origin"][field] = value
                with self.assertRaises(ValueError):
                    check(message)

    def test_namespace_follows_origin(self):
        for field in ("entity_type", "dimension"):
            message = example()
            message[field] = "7dtd:main"
            if field == "dimension":
                message["origin"][field] = message[field]
            with self.assertRaisesRegex(ValueError, "namespace"):
                check(message)

    def test_strict_fields_version_and_numbers(self):
        for mutate in (
            lambda m: m.update(version=1), lambda m: m.update(version=2.0),
            lambda m: m.update(sequence=True), lambda m: m.update(sequence=0),
            lambda m: m.update(extra=True), lambda m: m.pop("authority"),
            lambda m: m.pop("origin"), lambda m: m["origin"].update(extra=1),
            lambda m: m["position"].update(x=float("inf")),
            lambda m: m["rotation"].update(yaw=360),
            lambda m: m["metadata"].update(nested={}),
            lambda m: m["metadata"].update(health=30, max_health=20),
        ):
            with self.subTest(mutate=mutate):
                message = example()
                mutate(message)
                with self.assertRaises(ValueError):
                    check(message)

    def test_aliases_not_in_v2(self):
        for event in ("announce", "remove"):
            message = example()
            message["lifecycle"]["event"] = event
            with self.assertRaises(ValueError):
                check(message)

    def test_despawn_has_no_state(self):
        for field, value in (("position", example()["position"]), ("rotation", example()["rotation"]),
                             ("metadata", {"display_name": "demo"})):
            message = example(event="despawn")
            message[field] = value
            with self.assertRaises(ValueError):
                check(message)

    def test_forward_and_inverse_mapping(self):
        mc, td = example(), example("7dtd")
        mapped = map_snapshot(mc, "minecraft", mc["stream_id"], MAPPING)
        self.assertEqual(mapped["position"], {"x": 120, "y": 118, "z": 65, "space": "7dtd"})
        inverse = map_snapshot(td, "7dtd", td["stream_id"], MAPPING)
        self.assertEqual(inverse["position"], {"x": 10, "y": 64, "z": 20, "space": "minecraft"})
        for source, target in ((mc, mapped), (td, inverse)):
            for field in ("source", "authority", "origin", "entity_type", "rotation", "metadata"):
                self.assertEqual(source[field], target[field])

    def test_mapping_does_not_mutate_input_or_map_despawn(self):
        message = example()
        before = copy.deepcopy(message)
        map_snapshot(message, "minecraft", message["stream_id"], MAPPING)
        self.assertEqual(before, message)
        message = example(event="despawn")
        self.assertEqual(map_snapshot(message, "minecraft", message["stream_id"], MAPPING), message)

    def test_reject_double_mapping_and_echo(self):
        message = example()
        mapped = map_snapshot(message, "minecraft", message["stream_id"], MAPPING)
        with self.assertRaisesRegex(ValueError, "coordinate_space"):
            map_snapshot(mapped, "minecraft", message["stream_id"], MAPPING)
        with self.assertRaisesRegex(ValueError, "source_session"):
            check(mapped, "7dtd")

    def test_mapping_rejects_invalid_parameters_and_overflow(self):
        message = example()
        for scale in (0, -1, float("nan"), float("inf"), True, 1e308):
            with self.subTest(scale=scale):
                with self.assertRaises(ValueError):
                    map_snapshot(message, "minecraft", message["stream_id"], dict(MAPPING, scale=scale))

    def test_duplicate_spawn_does_not_overwrite(self):
        state = oracle()
        original = example()
        state.apply(original, "minecraft")
        duplicate = copy.deepcopy(original)
        duplicate["position"]["x"] = 999
        duplicate["sequence"] = 100
        self.assertEqual(state.apply(duplicate, "minecraft"), "ignore_duplicate_spawn")
        self.assertEqual(state.records[key(original)]["state"], original)
        self.assertEqual(state.apply(example(event="update"), "minecraft"), "accept_update")

    def test_stale_update_never_overwrites_new_state(self):
        state = oracle()
        state.apply(example(), "minecraft")
        update = example(event="update")
        state.apply(update, "minecraft")
        stale = copy.deepcopy(update)
        stale["sequence"] = 1
        stale["position"]["x"] = 999
        self.assertEqual(state.apply(stale, "minecraft"), "ignore_stale_or_duplicate")
        self.assertEqual(state.records[key(update)]["state"], update)

    def test_unknown_update_and_despawn_tombstone(self):
        state = oracle()
        self.assertEqual(state.apply(example(event="update"), "minecraft"), "reject_unknown_or_retired_update")
        self.assertEqual(state.apply(example(event="despawn"), "minecraft"), "accept_despawn")
        self.assertEqual(state.active_count, 0)
        new = example()
        new["sequence"] = 4
        self.assertEqual(state.apply(new, "minecraft"), "reject_retired_identity")

    def test_type_change_is_rejected(self):
        state = oracle()
        state.apply(example(), "minecraft")
        message = example(event="update")
        message["entity_type"] = "minecraft:marker"
        self.assertEqual(state.apply(message, "minecraft"), "reject_type_change")

    def test_same_uuid_from_two_origins_is_independent(self):
        state = oracle()
        mc, td = example(), example("7dtd")
        td["entity_id"] = td["origin"]["entity_id"] = mc["entity_id"]
        state.apply(mc, "minecraft")
        state.apply(td, "7dtd")
        self.assertEqual(state.active_count, 2)

    def test_session_reset_invalidates_old_stream_only_for_owner(self):
        state = oracle()
        state.apply(example(), "minecraft")
        state.apply(example("7dtd"), "7dtd")
        new_stream = "60546c8c-08b5-466a-ae8c-97a0e22c8cfa"
        state.begin_session("minecraft", new_stream)
        self.assertEqual(state.active_count, 1)
        with self.assertRaisesRegex(ValueError, "inactive_stream"):
            state.apply(example(event="update"), "minecraft")
        fresh = example()
        fresh["stream_id"] = new_stream
        self.assertEqual(state.apply(fresh, "minecraft"), "accept_spawn")

    def test_non_owner_rejection_does_not_change_records(self):
        state = oracle()
        state.apply(example(), "minecraft")
        before = copy.deepcopy(state.records)
        with self.assertRaises(ValueError):
            state.apply(example(event="despawn"), "7dtd")
        self.assertEqual(state.records, before)

    def test_duplicate_keys_and_message_size(self):
        folder = ROOT / "work/phase3_6_0-test"
        with tempfile.TemporaryDirectory(dir=folder) as temp:
            path = __import__("pathlib").Path(temp) / "duplicate.json"
            path.write_text('{"source":"minecraft","source":"7dtd"}', encoding="utf-8")
            with self.assertRaises(ValueError):
                load(path)
        message = example()
        message["metadata"] = {"k" + str(i): "中" * 256 for i in range(32)}
        with self.assertRaisesRegex(ValueError, "message_too_large"):
            check(message)
        with self.assertRaisesRegex(ValueError, "message_too_large"):
            decode_wire(" " * 8192 + "{}")

    def test_production_files_unchanged(self):
        baseline = load(ROOT / "work/phase3_6_0-test/protected-before.json")
        for path, digest in baseline.items():
            with self.subTest(path=path):
                self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), digest)


if __name__ == "__main__":
    if "--snapshot" in sys.argv:
        paths = []
        for folder in ("minecraft-mod/src", "7dtd-mod/src"):
            paths.extend(path for path in (ROOT / folder).rglob("*") if path.is_file())
        paths.extend((ROOT / "bridge-server").glob("*.cs"))
        paths.extend((ROOT / "bridge-server").glob("*.csproj"))
        paths.extend((ROOT / "config").glob("*.json"))
        fingerprint = {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
        (ROOT / "work/phase3_6_0-test/protected-before.json").write_text(json.dumps(fingerprint, indent=2), encoding="utf-8")
        sys.exit(0)
    result = unittest.TextTestRunner(stream=sys.stdout, verbosity=2).run(
        unittest.defaultTestLoader.loadTestsFromTestCase(ProtocolTests))
    (ROOT / "work/phase3_6_0-test/results.json").write_text(json.dumps({
        "passed": result.wasSuccessful(), "test_groups": result.testsRun,
        "failures": len(result.failures), "errors": len(result.errors),
        "gameRuntimeTested": False, "scope": "offline entity_state v2 design"
    }, indent=2), encoding="utf-8")
    sys.exit(0 if result.wasSuccessful() else 1)
