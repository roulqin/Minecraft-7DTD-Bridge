"""Phase 3.5.0 offline design checks; no game connection or object creation."""
import copy
import json
from pathlib import Path
import sys
import unittest
from validate_catalog import load_json, validate_catalog
from test_catalog import CatalogTests

ROOT = Path(__file__).resolve().parents[2]
CATALOG = load_json(ROOT / "config/entity_types.json")
SCHEMA = load_json(ROOT / "config/entity_types.schema.json")


class PlayerProxyTests(unittest.TestCase):
    def test_reserved_mapping_and_capabilities(self):
        validate_catalog(CATALOG, SCHEMA)
        players = [entry for entry in CATALOG["types"] if entry["entity_type"] == "minecraft:player"]
        self.assertEqual(len(players), 1)
        player = players[0]
        self.assertIs(player["enabled"], True)
        self.assertEqual(player["target_mapping"], {
            "game": "7dtd", "entity_type": "7dtd:player_proxy", "adapter": "player_proxy"})
        self.assertEqual({name for name, enabled in player["capabilities"].items() if enabled},
                         {"spawn", "update_position", "update_rotation", "despawn"})

    def test_cannot_enable_missing_player_adapter(self):
        data = copy.deepcopy(CATALOG)
        data["types"][1]["enabled"] = True
        data["types"][1]["target_mapping"]["adapter"] = "missing_player_proxy"
        with self.assertRaisesRegex(ValueError, "registered adapter"):
            validate_catalog(data, SCHEMA)

    def test_player_cannot_fall_back_to_marker_adapter(self):
        data = copy.deepcopy(CATALOG)
        data["types"][1]["enabled"] = True
        data["types"][1]["target_mapping"].update(adapter="marker", entity_type="7dtd:marker")
        with self.assertRaisesRegex(ValueError, "does not support"):
            validate_catalog(data, SCHEMA)

    def test_example_files_are_strict_json(self):
        folder = ROOT / "docs/examples/player_proxy"
        for name in ("spawn", "update", "despawn"):
            with self.subTest(event=name):
                state = load_json(folder / (name + ".json"))
                self.assertEqual(state["entity_type"], "minecraft:player")
                self.assertEqual(state["lifecycle"]["event"], name)
        expected = load_json(folder / "mapping_expectations.json")
        self.assertEqual(len(expected["forwarded"]), 3)


if __name__ == "__main__":
    suite = unittest.TestSuite([
        unittest.defaultTestLoader.loadTestsFromTestCase(CatalogTests),
        unittest.defaultTestLoader.loadTestsFromTestCase(PlayerProxyTests),
    ])
    result = unittest.TextTestRunner(stream=sys.stdout, verbosity=2).run(suite)
    output = ROOT / "work/phase3_5_0-test"
    output.mkdir(parents=True, exist_ok=True)
    (output / "catalog-results.json").write_text(json.dumps({
        "test_groups": result.testsRun, "failures": len(result.failures),
        "errors": len(result.errors), "passed": result.wasSuccessful()
    }, indent=2), encoding="utf-8")
    sys.exit(0 if result.wasSuccessful() else 1)
