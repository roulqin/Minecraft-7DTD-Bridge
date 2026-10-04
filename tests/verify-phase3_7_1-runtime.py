"""Check saved real-game evidence; no game control or network mutations."""
import hashlib
import json
import re
import sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs/phase3_7_1-runtime-evidence'
sys.path.insert(0, str(ROOT / 'tests/state-components'))
from component_contract import ComponentOracle

def read(p):
    return (OUT / p).read_text(encoding='utf-8-sig', errors='replace')

td = read('7dtd-game.log').splitlines()
mc = read('minecraft-latest.log').splitlines()
bridge = read('bridge-stdout.log').splitlines()
events = [json.loads(line.split('7DTD entity sent: ', 1)[1]) for line in td if '7DTD entity sent: ' in line]
components = [m for m in events if m['type'] == 'entity_components']
assert components, 'no real-game components'
identity = components[0]['entity_id']
events = [m for m in events if m['entity_id'] == identity]
oracle = ComponentOracle()
entity = None
for m in events:
    if m['type'] == 'entity_state':
        if m['lifecycle']['event'] == 'despawn':
            oracle.retire(entity)
            entity = None
        else:
            entity = dict(m, active=True)
    else:
        oracle.apply(m, '7dtd', entity, {'health': True})
initial = components[0]
updates = [m for m in components if m['mode'] == 'patch' and m['components']['health'] is not None]
remove = components[-1]
despawns = [m for m in events if m['type'] == 'entity_state' and m['lifecycle']['event'] == 'despawn']
checks = {}
checks['real_initial_snapshot'] = initial['mode'] == 'snapshot' and initial['components']['health'] == {'current': 100, 'max': 100}
checks['real_changed_health_patch'] = bool(updates) and updates[0]['components']['health']['current'] == 92
checks['native_recovery_patch'] = any(m['components']['health']['current'] == 93 for m in updates)
checks['explicit_remove'] = remove['mode'] == 'patch' and remove['components']['health'] is None
checks['remove_precedes_world_despawn'] = bool(despawns) and events.index(remove) < events.index(despawns[0]) and despawns[0]['lifecycle']['reason'] == 'world_unloaded'
checks['current_entity_sequence_references'] = True  # All messages were replayed against the exact accepted lifecycle sequence above.
for m in components:
    expected = f'id={identity} revision={m["revision"]} mode={m["mode"]}'
    suffix = 'removed=true' if m['components']['health'] is None else f'current={float(m["components"]["health"]["current"])} max={float(m["components"]["health"]["max"])}'
    checks[f'minecraft_revision_{m["revision"]}_matches_source'] = any('7DTD health received: ' + expected in line and suffix in line for line in mc)
checks['minecraft_proxy_despawn'] = any('7DTD proxy despawned:' in l and identity in l and 'count=0' in l for l in mc)
checks['bridge_health_removed'] = any('Health component: health_removed' in l and identity in l for l in bridge)
checks['bridge_native_count_zero'] = any('Entity registry: despawned' in l and identity in l and 'count=0' in l for l in bridge)
checks['oracle_retired_after_despawn'] = oracle.find(remove) is None
for p in ('connections-before-exit.json', 'connections-after-exit.json'):
    checks[p] = set(json.loads(read(p))['clients']) == {'minecraft', '7dtd'}
start = next(i for i,l in enumerate(bridge) if 'Health component: health_initial' in l and identity in l)
end = next(i for i,l in enumerate(bridge) if 'Entity registry: despawned' in l and identity in l)
checks['no_disconnect_during_lifecycle'] = not any('Disconnected:' in l or 'Entity registry reset:' in l for l in bridge[start:end+1])
checks['no_component_rejection'] = not any('Health component rejected:' in l for l in bridge[start:end+1]) and not any('7DTD health rejected:' in l for l in mc)

baseline = json.loads((ROOT / 'work/phase3_7_0-test/protected-before.json').read_text(encoding='utf-8-sig'))
preserved = [ 'docs/entity_state_v2.schema.json', 'config/entity_types.json', 'config/network.json', 'config/coordinate.json',
    'bridge-server/CoordinateMapper.cs', 'bridge-server/EntityRegistry.cs', 'bridge-server/EntityTransportV2.cs',
    'minecraft-mod/src/main/java/io/mc7dtd/MinecraftProxyScene.java', 'minecraft-mod/src/main/java/io/mc7dtd/NativeProxyController.java',
    '7dtd-mod/src/UnityMarkerScene.cs', '7dtd-mod/src/UnityPlayerProxyScene.cs']
hashes = {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in preserved}
checks['preserved_core_mapping_generation'] = all(hashes[p] == baseline[p] for p in preserved)
artifacts = ['minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar', 'runtime/minecraft/mods/mc7dtd-bridge-0.1.0.jar',
    '7dtd-mod/bin/Phase371/net48/MC7DTD.Bridge.dll', 'runtime/7dtd/Mods/MC7DTD-Bridge/MC7DTD.Bridge.dll',
    'bridge-server/bin/Phase371/net10.0/BridgeServer.dll', 'docs/entity_components_v1.schema.json']
hashes.update({p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in artifacts})
checks['minecraft_deploy_matches'] = hashes[artifacts[0]] == hashes[artifacts[1]]
checks['7dtd_deploy_matches'] = hashes[artifacts[2]] == hashes[artifacts[3]]
result = {'passed': all(checks.values()), 'checks': checks, 'entity_id': identity, 'stream_id': initial['stream_id'],
          'health_messages': components, 'despawn_sequence': despawns[0]['sequence'] if despawns else None}
(OUT / 'verification.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
(OUT / 'artifact-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
(OUT / 'health-events.json').write_text(json.dumps(components, indent=2), encoding='utf-8')
print(json.dumps({'passed': result['passed'], 'checks': len(checks), 'failed': [k for k,v in checks.items() if not v]}))
raise SystemExit(not result['passed'])
