"""Validate actual C# publisher serialization against the existing offline component contract."""
import json
import sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tests/state-components'))
from component_contract import ComponentOracle
from jsonschema import Draft202012Validator

read = lambda p: json.loads((ROOT / p).read_text(encoding='utf-8-sig'))
Draft202012Validator(read('config/component_permissions.schema.json')).validate(read('config/component_permissions.json'))
oracle = ComponentOracle()
entity = None
count = 0
for m in read('work/phase3_7_1-test/sender-events.json'):
    if m['type'] == 'entity_state':
        if m['lifecycle']['event'] == 'despawn':
            if entity:
                oracle.retire(entity)
            entity = None
        else:
            entity = dict(m, active=True)
    else:
        oracle.apply(m, '7dtd', entity, {'health': True})
        count += 1
print(f'Publisher wire schema/semantics: {count} component messages passed; permission schema passed')
