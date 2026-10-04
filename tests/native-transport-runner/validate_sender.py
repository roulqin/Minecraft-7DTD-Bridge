"""Validate production C# sender payloads against the existing v2 contract."""
import json
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root / "tests/bidirectional-protocol"))
from contract import load, validate

out = root / "work/phase3_6_1-test"
passed = []
for event in ("spawn", "update", "despawn"):
    message = load(out / f"sender_{event}.json")
    validate(message, "7dtd", message["stream_id"])
    assert message["lifecycle"]["event"] == event
    passed.append(f"production sender {event}")
for line in (out / "minecraft.log").read_text(encoding="utf-8-sig").splitlines():
    if line.startswith("7DTD entity received: "):
        message = json.loads(line[len("7DTD entity received: "):])
        validate(message, "7dtd", message["stream_id"], forwarded=True)
        passed.append(f"forwarded {message['lifecycle']['event']}")
assert len(passed) >= 8
(out / "schema-results.json").write_text(json.dumps({"passed": passed}, indent=2), encoding="utf-8")
print(f"PASS {len(passed)} production payload contract checks")
