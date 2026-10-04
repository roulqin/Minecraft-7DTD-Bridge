"""Phase 3.8.0 offline design oracle. Never imported by a game or Bridge."""
import copy
import json
import sys
from pathlib import Path
from jsonschema import Draft202012Validator
from referencing import Registry, Resource

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tests/state-components'))
from component_contract import validate as validate_legacy, decode_wire, key

SLOTS = {'head', 'body', 'hands', 'feet', 'held_item'}
COMPONENTS = {'health', 'name', 'custom_metadata', 'equipment'}
registry = Registry()
for name in ('entity_state_v2.schema.json', 'equipment_component_v1.schema.json'):
    resource = json.loads((ROOT / 'docs' / name).read_text(encoding='utf-8-sig'))
    registry = registry.with_resource('https://mc7dtd.invalid/schemas/' + name, Resource.from_contents(resource))
schema = json.loads((ROOT / 'docs/entity_components_equipment_v1.draft.schema.json').read_text(encoding='utf-8'))
Draft202012Validator.check_schema(schema)
VALIDATOR = Draft202012Validator(schema, registry=registry)

def allowed(grants, component):
    grant = grants.get(component, {})
    return grant.get('publish') is True and grant.get('store') is True

def validate(message, role, entity, grants):
    decode_wire(json.dumps(message, ensure_ascii=False, allow_nan=False, separators=(',', ':')))
    errors = list(VALIDATOR.iter_errors(message))
    if errors:
        raise ValueError('schema: ' + errors[0].message)
    if role != '7dtd' or message['source'] != '7dtd' or message['entity_type'] != '7dtd:player':
        raise ValueError('unsupported_equipment_route')
    if any(not allowed(grants, c) for c in message['components']):
        raise ValueError('component_permission_denied')
    # Reuse legacy structural/identity/health/Identity checks without changing legacy files.
    projected = copy.deepcopy(message)
    projected['components'].pop('equipment', None)
    if projected['mode'] == 'patch' and not projected['components']:
        projected['components']['custom_metadata'] = {}  # validation-only nonempty placeholder
    validate_legacy(projected, role, entity, dict.fromkeys(COMPONENTS, True))

class EquipmentOracle:
    """Trusted lifecycle context supplied by caller; no alternate Registry/Authority."""
    def __init__(self):
        self.records = {}

    def apply(self, message, role, entity, grants):
        validate(message, role, entity, grants)
        identity = (key(message), message['stream_id'])
        old = self.records.get(identity)
        if message['mode'] == 'snapshot':
            affected = set(message['components']) | (set(old['components']) if old else set())
            if any(not allowed(grants, c) for c in affected):
                raise ValueError('component_permission_denied')
        value = message['components'].get('equipment')
        if message['mode'] == 'patch' and value is not None:
            absent = old is None or 'equipment' not in old['components']
            if absent and set(value['slots']) != SLOTS:
                raise ValueError('complete_equipment_required')
        if old and message['revision'] <= old['revision']:
            return 'ignore_stale'
        if message['mode'] == 'patch':
            if old is None or message['base_revision'] != old['revision']:
                raise ValueError('base_revision_mismatch')
            result = copy.deepcopy(old['components'])
            for component, value in message['components'].items():
                if value is None:
                    result.pop(component, None)
                elif component == 'equipment':
                    result.setdefault('equipment', {'slots': {}})['slots'].update(copy.deepcopy(value['slots']))
                else:
                    result[component] = copy.deepcopy(value)
        else:
            result = copy.deepcopy(message['components'])
        self.records[identity] = {'revision': message['revision'], 'components': result}
        return 'applied'

    def find(self, message):
        return copy.deepcopy(self.records.get((key(message), message['stream_id'])))

    def retire(self, entity):
        self.records.pop((key(entity), entity['stream_id']), None)

    def clear_source(self, source):
        self.records = {k:v for k,v in self.records.items() if k[0][0] != source}
