"""Verify saved real-game identity evidence against the extended component contract."""
import hashlib,json,re,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'docs/phase3_7_2-runtime-evidence'
sys.path.insert(0,str(ROOT/'tests/state-components'))
from component_contract import ComponentOracle
def lines(name):return (OUT/name).read_text(encoding='utf-8-sig',errors='replace').splitlines()
events=[json.loads(l.split('7DTD entity sent: ',1)[1]) for l in lines('7dtd-game.log') if '7DTD entity sent: ' in l]
components=[m for m in events if m['type']=='entity_components']
assert components,'No real-game components'
entity_id=components[0]['entity_id'];events=[m for m in events if m['entity_id']==entity_id];components=[m for m in components if m['entity_id']==entity_id]
received=[]
for l in lines('minecraft-latest.log'):
 if '7DTD identity received:' in l and entity_id in l:
  match=re.search(r'revision=(\d+) mode=(snapshot|patch) identity=(.*) removed=(true|false)$',l)
  assert match,l
  received.append({'revision':int(match[1]),'mode':match[2],'state':json.loads(match[3]),'removed':match[4]=='true'})
oracle=ComponentOracle();entity=None;applied={}
for m in events:
 if m['type']=='entity_state':
  if m['lifecycle']['event']=='despawn':oracle.retire(entity);entity=None
  else:entity=dict(m,active=True)
 else:
  oracle.apply(m,'7dtd',entity,{'health':True,'name':True,'custom_metadata':True});applied[m['revision']]=oracle.find(m)['components']
initial=components[0];checks={}
checks['real_initial_identity']=bool(initial['components'].get('name',{}).get('text')) and bool(initial['components']['name'].get('display_name'))
checks['initial_metadata_tags']=initial['components']['custom_metadata']['tag.label']=='bridge'
checks['source_native_name_preserved']=all(m['components']['name']['text']==initial['components']['name']['text'] for m in components if m['components'].get('name'))
updates=[m for m in components if m['mode']=='patch' and m['components'].get('name',{} ) and m['components']['name'].get('display_name')=='MC7DTD Identity Updated']
checks['real_partial_display_name']=bool(updates)
checks['real_partial_tag']=bool(updates) and updates[0]['components']['custom_metadata']['tag.label']=='runtime-update'
removes=[m for m in components if m['mode']=='patch' and 'name' in m['components'] and m['components']['name'] is None and m['components'].get('custom_metadata','missing') is None]
checks['explicit_identity_remove']=bool(removes)
checks['identity_remove_preserves_health']=bool(removes) and 'health' in applied[removes[0]['revision']] and 'name' not in applied[removes[0]['revision']] and 'custom_metadata' not in applied[removes[0]['revision']]
checks['source_health_changes_alongside_identity']=any(m['mode']=='patch' and m['components'].get('health') for m in components)
checks['receiver_states_match_source_oracle']=bool(received) and all(item['state']==applied[item['revision']] for item in received)
checks['receiver_identity_remove_logged']=any(item['removed'] and 'health' in item['state'] for item in received)
despawns=[m for m in events if m['type']=='entity_state' and m['lifecycle']['event']=='despawn']
checks['normal_world_exit']=bool(despawns) and despawns[-1]['lifecycle']['reason']=='world_unloaded'
checks['all_components_remove_before_despawn']=bool(despawns) and events[-2]['type']=='entity_components' and all(v is None for v in events[-2]['components'].values()) and events[-1]['lifecycle']['event']=='despawn'
checks['oracle_retired']=oracle.find(initial) is None
mc=lines('minecraft-latest.log');bridge=lines('bridge-stdout.log')
checks['proxy_despawn_count_zero']=any('7DTD proxy despawned:' in l and entity_id in l and 'count=0' in l for l in mc)
start=next(i for i,l in enumerate(bridge) if 'Components:' in l and entity_id in l)
end=next((i for i,l in enumerate(bridge) if 'Entity registry: despawned' in l and entity_id in l),len(bridge)-1)
checks['no_disconnect_in_real_lifecycle']=not any('Disconnected:' in l or 'Entity registry reset:' in l for l in bridge[start:end+1])
checks['no_component_rejection']=not any('component rejected:' in l.lower() or 'health rejected:' in l.lower() for l in bridge[start:end+1]+mc)
for name in ('connections-before-remove.json','connections-after-remove.json','connections-after-exit.json'):
 checks[name]=set(json.loads((OUT/name).read_text(encoding='utf-8-sig'))['clients'])=={'minecraft','7dtd'}
base=json.loads((ROOT/'work/phase3_7_2-test/protected-before.json').read_text(encoding='utf-8-sig'))
checks['protected_v2_mapping_generation_unchanged']=all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==digest for p,digest in base.items())
paths=['minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar','runtime/minecraft/mods/mc7dtd-bridge-0.1.0.jar','7dtd-mod/bin/Phase372/net48/MC7DTD.Bridge.dll','runtime/7dtd/Mods/MC7DTD-Bridge/MC7DTD.Bridge.dll','bridge-server/bin/Phase372/net10.0/BridgeServer.dll']
hashes={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in paths}
checks['minecraft_deploy_hash']=hashes[paths[0]]==hashes[paths[1]];checks['7dtd_deploy_hash']=hashes[paths[2]]==hashes[paths[3]]
checks['identity_config_restored']=(ROOT/'config/identity_labels.json').read_bytes()==(OUT/'identity-labels-original.json').read_bytes()
result={'passed':all(checks.values()),'checks':checks,'entity_id':entity_id,'stream_id':initial['stream_id'],'component_messages':len(components),'identity_observations':received,'despawn_sequence':despawns[-1]['sequence'] if despawns else None}
(OUT/'verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8');(OUT/'component-events.json').write_text(json.dumps(components,indent=2),encoding='utf-8');(OUT/'artifact-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
print(json.dumps({'passed':result['passed'],'checks':len(checks),'failed':[k for k,v in checks.items() if not v]}));raise SystemExit(not result['passed'])
