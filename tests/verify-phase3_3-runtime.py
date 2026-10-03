"""Compare preserved real-game logs. This does not connect to or control games."""
import json
import math
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
evidence = root / 'docs/phase3_3-runtime-evidence'
mc = (evidence / 'minecraft-latest.log').read_text(encoding='utf-8', errors='replace')
td = (evidence / '7dtd-game-first-round.log').read_text(encoding='utf-8', errors='replace')
bridge = (evidence / 'bridge-test.log').read_text(encoding='utf-8', errors='replace')
mapping = json.loads((evidence / 'coordinate-test.json').read_text(encoding='utf-8-sig'))
sent = [json.loads(line.split('Entity test sent: ', 1)[1])
        for line in mc.splitlines() if 'Entity test sent: ' in line]
sent = [s for s in sent if s['entity_type'] == 'minecraft:marker']
assert [s['lifecycle']['event'] for s in sent[:3]] == ['spawn', 'update', 'despawn']
identity = sent[0]['entity_id']
assert all(s['entity_id'] == identity and s['stream_id'] == sent[0]['stream_id'] for s in sent[:3])
positions = []
for message in sent[:2]:
    event = message['lifecycle']['event']
    rows = [line for line in td.splitlines() if f'Entity state: event={event} id={identity} ' in line]
    assert len(rows) == 1
    xyz = {}
    for axis in ('x', 'y', 'z'):
        actual = float(re.search(rf'\b{axis}=([^ ]+)', rows[0]).group(1))
        expected = message['position'][axis] * mapping['scale'] + mapping['offset' + axis.upper()]
        assert math.isclose(actual, expected, rel_tol=0, abs_tol=1e-10)
        xyz[axis] = actual
    positions.append(xyz)
assert positions[0] != positions[1]
created = [line for line in td.splitlines() if 'Marker Unity created:' in line and identity in line]
assert len(created) == 1 and 'active=True renderer=True colliderEnabled=False' in created[0]
instance = re.search(r'instance=(-?\d+)', created[0]).group(1)
assert f'Marker Unity deleted: instance={instance} inactive=true destroyScheduled=true' in td
for action, count in [('spawned', 1), ('updated', 1), ('despawned', 0)]:
    assert f'Marker {action}: id={identity} count={count}' in td
    assert re.search(rf'Marker {action}: id={identity} count={count} .* thread=1\b', td)
    assert f'Entity registry: {action}, id={identity}, count={count}' in bridge
assert sent[2]['position'] is None and sent[2]['rotation'] is None
assert sent[2]['metadata'] == {}
assert (evidence / 'marker-spawn.jpg').is_file()
assert (evidence / 'marker-update.jpg').is_file()
assert (evidence / 'marker-despawn.jpg').is_file()
result = {'passed': True, 'gameRuntimeTested': True, 'entityId': identity,
          'unityInstanceId': instance, 'positions': positions, 'mapping': mapping,
          'registryCounts': [1, 1, 0], 'receiverCounts': [1, 1, 0],
          'singleObjectCreated': True, 'mainThread': 1,
          'despawnInactiveAndDestroyScheduled': True,
          'screenshots': ['marker-spawn.jpg', 'marker-update.jpg', 'marker-despawn.jpg']}
(evidence / 'runtime-comparison.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False))
