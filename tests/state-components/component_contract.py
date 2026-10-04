"""Phase 3.7.0 offline specification; never imported by a game or Bridge."""
import copy
import json
import hashlib
import math
import sys
from pathlib import Path
from jsonschema import Draft202012Validator
from referencing import Registry, Resource

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tests/bidirectional-protocol'))
from contract import decode_wire, finite_tree, key, validate as validate_core

SCHEMA = json.loads((ROOT / 'docs/entity_components_v1.schema.json').read_text(encoding='utf-8-sig'))
CORE = json.loads((ROOT / 'docs/entity_state_v2.schema.json').read_text(encoding='utf-8-sig'))
Draft202012Validator.check_schema(SCHEMA)
RESOURCES = Registry().with_resource('https://mc7dtd.invalid/schemas/entity_state_v2.schema.json', Resource.from_contents(CORE))
VALIDATOR = Draft202012Validator(SCHEMA, registry=RESOURCES)
COMPONENTS = {'health', 'name', 'animation_state', 'custom_metadata'}


def validate(message, sender_role, entity, grants):
    errors = list(VALIDATOR.iter_errors(message))
    if errors:
        raise ValueError('schema: ' + errors[0].message)
    finite_tree(message)
    for field in ('version', 'entity_state_version', 'entity_sequence', 'revision', 'base_revision'):
        if type(message[field]) is not int:
            raise ValueError('integer_required')
    if len(json.dumps(message, ensure_ascii=False, allow_nan=False, separators=(',', ':')).encode()) > 8192:
        raise ValueError('message_too_large')
    origin = message['origin']
    if sender_role not in ('minecraft', '7dtd') or message['source'] != sender_role:
        raise ValueError('source_session_mismatch')
    if message['source'] != message['authority'] or message['authority'] != origin['game']:
        raise ValueError('non_authoritative_writer')
    if any(message[k] != origin[k] for k in ('entity_id', 'world_id', 'dimension')):
        raise ValueError('origin_identity_mismatch')
    if not all(message[k].startswith(origin['game'] + ':') for k in ('entity_type', 'dimension')):
        raise ValueError('origin_namespace_mismatch')
    if entity is None or not entity['active']:
        raise ValueError('inactive_entity')
    if key(message) != key(entity) or message['entity_type'] != entity['entity_type'] or message['authority'] != entity['authority']:
        raise ValueError('entity_registry_mismatch')
    if message['stream_id'] != entity['stream_id']:
        raise ValueError('inactive_stream')
    if message['entity_sequence'] != entity['sequence']:
        raise ValueError('entity_sequence_mismatch')
    if not set(message['components']) <= set(grants) or any(grants[k] is not True for k in message['components']):
        raise ValueError('component_permission_denied')
    components = message['components']
    health = components.get('health')
    if health and health['current'] > health['max']:
        raise ValueError('invalid_health_range')
    name = components.get('name')
    if name and any(ord(c) < 32 or 127 <= ord(c) <= 159 for c in name['text']):
        raise ValueError('invalid_name_control')
    if name and 'display_name' in name and any(ord(c) < 32 or 127 <= ord(c) <= 159 for c in name['display_name']):
        raise ValueError('invalid_name_control')
    for k, v in (components.get('custom_metadata') or {}).items():
        if k.startswith('tag.') and (len(k) == 4 or not isinstance(v, str) or not v or any(ord(c) < 32 or 127 <= ord(c) <= 159 for c in v)):
            raise ValueError('invalid_identity_tag')
    animation = components.get('animation_state')
    if animation and not animation['state'].startswith(sender_role + ':'):
        raise ValueError('animation_namespace_mismatch')
    return message


class ComponentOracle:
    """Reference-only atomic reducer; caller supplies trusted lifecycle/permission context."""
    def __init__(self):
        self.records = {}

    def apply(self, message, role, entity, grants):
        validate(message, role, entity, grants)
        identity = (key(message), message['stream_id'])
        previous = self.records.get(identity)
        if previous and message['revision'] <= previous['revision']:
            return 'ignore_stale'
        if message['mode'] == 'patch':
            if previous is None:
                raise ValueError('snapshot_required')
            if message['base_revision'] != previous['revision']:
                raise ValueError('base_revision_mismatch')
            result = copy.deepcopy(previous['components'])
            for component, state in message['components'].items():
                if state is None:
                    result.pop(component, None)
                else:
                    result[component] = copy.deepcopy(state)
        else:
            # Omitting an existing component is also a write/removal that needs permission.
            changed = set(message['components']) | (set(previous['components']) if previous else set())
            if any(grants.get(component) is not True for component in changed):
                raise ValueError('component_permission_denied')
            result = copy.deepcopy(message['components'])
        self.records[identity] = {'revision': message['revision'], 'components': result}
        return 'applied'

    def find(self, message):
        return copy.deepcopy(self.records.get((key(message), message['stream_id'])))

    def retire(self, entity):
        self.records.pop((key(entity), entity['stream_id']), None)

    def clear_source(self, source):
        self.records = {k: v for k, v in self.records.items() if k[0][0] != source}


def protected_paths():
    paths = []
    for folder in ('minecraft-mod/src', '7dtd-mod/src', 'bridge-server', 'config'):
        paths.extend(p for p in (ROOT / folder).rglob('*') if p.is_file() and p.suffix in ('.java', '.cs', '.json', '.csproj') and 'bin' not in p.parts and 'obj' not in p.parts)
    paths.append(ROOT / 'docs/entity_state_v2.schema.json')
    return {p.relative_to(ROOT).as_posix(): p for p in paths}


def capture_baseline():
    output = ROOT / 'work/phase3_7_0-test'
    output.mkdir(parents=True, exist_ok=True)
    baseline = output / 'protected-before.json'
    if not baseline.exists():
        baseline.write_text(json.dumps({name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in protected_paths().items()}, indent=2), encoding='utf-8')
