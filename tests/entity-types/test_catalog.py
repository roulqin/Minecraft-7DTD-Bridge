"""Validate shipped files and rejection boundaries without touching live games."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
import sys
from validate_catalog import load_json, validate_catalog

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = load_json(ROOT / "config/entity_types.schema.json")
CATALOG = load_json(ROOT / "config/entity_types.json")


class CatalogTests(unittest.TestCase):
    def test_shipped_marker(self):
        self.assertEqual(validate_catalog(CATALOG, SCHEMA), CATALOG)
        marker = CATALOG["types"][0]
        self.assertEqual(marker["entity_type"], "minecraft:marker")
        self.assertEqual(marker["target_mapping"]["entity_type"], "7dtd:marker")
        self.assertEqual({k for k, v in marker["capabilities"].items() if v},
                         {"spawn", "update_position", "despawn"})

    def test_valid_variants(self):
        for variant in ("disabled", "no_position", "reserved"):
            with self.subTest(variant=variant):
                data = copy.deepcopy(CATALOG)
                entry = data["types"][0]
                if variant == "no_position":
                    entry["capabilities"]["update_position"] = False
                else:
                    entry["enabled"] = False
                    if variant == "reserved":
                        entry["entity_type"] = "minecraft:future_marker"
                        entry["target_mapping"]["adapter"] = "future_marker"
                        entry["target_mapping"]["entity_type"] = "7dtd:future_marker"
                validate_catalog(data, SCHEMA)

    def test_invalid_variants(self):
        def cases():
            yield "version", lambda d: d.update(version=2)
            yield "boolean_version", lambda d: d.update(version=True)
            yield "float_version", lambda d: d.update(version=1.0)
            yield "root_extra", lambda d: d.update(extra=1)
            yield "empty", lambda d: d.update(types=[])
            yield "too_many", lambda d: d.update(types=d["types"] * 129)
            yield "duplicate", lambda d: d["types"].append(copy.deepcopy(d["types"][0]))
            yield "disabled_duplicate", lambda d: d["types"].append(dict(copy.deepcopy(d["types"][0]), enabled=False))
            for field in ("entity_type", "enabled", "target_mapping", "capabilities"):
                yield "missing_" + field, lambda d, f=field: d["types"][0].pop(f)
            yield "entry_extra", lambda d: d["types"][0].update(extra=True)
            for value in ("Marker", "minecraft:Marker", "minecraft:", "other:marker", "minecraft:marker\n", "minecraft:" + "a" * 128):
                yield "type_" + value, lambda d, v=value: d["types"][0].update(entity_type=v)
            yield "enabled_string", lambda d: d["types"][0].update(enabled="true")
            for key, value in (("game", "unknown"), ("game", "minecraft"), ("entity_type", "minecraft:marker"), ("entity_type", "7dtd:unknown"), ("adapter", "unknown"), ("adapter", "marker\n"), ("adapter", "../marker"), ("extra", True)):
                yield "mapping_" + key + str(value), lambda d, k=key, v=value: d["types"][0]["target_mapping"].update({k: v})
            for field in ("game", "entity_type", "adapter"):
                yield "mapping_missing_" + field, lambda d, f=field: d["types"][0]["target_mapping"].pop(f)
            for field in CATALOG["types"][0]["capabilities"]:
                yield "capability_missing_" + field, lambda d, f=field: d["types"][0]["capabilities"].pop(f)
            for field in ("update_rotation", "health", "collision", "ai", "combat", "persistence"):
                yield "unsupported_" + field, lambda d, f=field: d["types"][0]["capabilities"].update({f: True})
            for field in ("spawn", "despawn"):
                yield "cleanup_" + field, lambda d, f=field: d["types"][0]["capabilities"].update({f: False})
            yield "capability_extra", lambda d: d["types"][0]["capabilities"].update(teleport=True)
            yield "capability_number", lambda d: d["types"][0]["capabilities"].update(spawn=1)
        for label, mutate in cases():
            with self.subTest(case=label):
                data = copy.deepcopy(CATALOG)
                mutate(data)
                with self.assertRaises(ValueError):
                    validate_catalog(data, SCHEMA)

    def test_strict_json(self):
        for text in ('{"version":1,"version":2}', '{"value":NaN}', '{"value":Infinity}', '{bad}'):
            with self.subTest(text=text):
                with tempfile.TemporaryDirectory(dir=ROOT / "work/phase3_4-test") as folder:
                    path = Path(folder) / "invalid.json"
                    path.write_text(text, encoding="utf-8")
                    with self.assertRaises(ValueError):
                        load_json(path)

    def test_validation_does_not_rewrite_catalog(self):
        data = copy.deepcopy(CATALOG)
        before = json.dumps(data, sort_keys=True)
        validate_catalog(data, SCHEMA)
        self.assertEqual(json.dumps(data, sort_keys=True), before)


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(CatalogTests)
    result = unittest.TextTestRunner(stream=sys.stdout, verbosity=2).run(suite)
    (ROOT / "work/phase3_4-test/catalog-results.json").write_text(
        json.dumps({"test_groups": result.testsRun, "failures": len(result.failures),
                    "errors": len(result.errors), "passed": result.wasSuccessful()}, indent=2),
        encoding="utf-8")
    sys.exit(0 if result.wasSuccessful() else 1)
