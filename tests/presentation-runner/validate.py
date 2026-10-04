from pathlib import Path
import json, hashlib, copy
from jsonschema import Draft202012Validator
from referencing import Registry, Resource
r=Path(__file__).resolve().parents[2]
def load(p): return json.loads((r/p).read_text(encoding='utf-8-sig'))
count=0
def check(ok,label):
    global count
    assert ok,label
    count+=1
    print('PASS',label)
catalog=load('config/entity_types.json')
Draft202012Validator(load('config/entity_types.schema.json')).validate(catalog)
check(True,'entity type defaults schema')
component=Draft202012Validator(load('components/presentation/presentation.schema.json'))
for t in catalog['types']:component.validate(t['presentation_default'])
for value in [{'scale':1.5},{'model':'survivor'},{}]:component.validate(value)
check(True,'sparse component patches')
for value in [{'scale':0},{'scale':True},{'scale':17},{'scale':'1'},{'model':'../skin'},{'model':'a\n'},{'variant':None},{'authority':'minecraft'}]:
    check(bool(list(component.iter_errors(value))),'reject invalid patch '+str(value))
schema=Draft202012Validator(load('docs/entity_state_v2.schema.json'))
entity=load('docs/examples/entity_state_v2/7dtd_spawn.json')
schema.validate(entity)
check(True,'old v2 without presentation')
for delta in [{'model':'survivor'}, {'renderer':'humanoid','model':'default','variant':'survivor','scale':1.0}]:
    m=copy.deepcopy(entity);m['components']={'presentation':delta};schema.validate(m)
check(True,'extended v2 spawn')
m=copy.deepcopy(entity);m['lifecycle']={'event':'update'};m['components']={'presentation':{'scale':1.5}};schema.validate(m)
m['components']['presentation']=None;schema.validate(m)
check(True,'partial update and remove schema')
for action in ['spawn','despawn']:
    m=copy.deepcopy(entity);m['lifecycle']={'event':action};m['components']={'presentation':None}
    if action=='despawn':m['position']=m['rotation']=None;m['lifecycle']['reason']='world_unloaded'
    check(bool(list(schema.iter_errors(m))),'reject invalid remove on '+action)
for p,expected in load('work/phase3_7_3-test/protected-hashes.json').items():
    check(hashlib.sha256((r/p).read_bytes()).hexdigest()==expected,'unchanged '+p)
# Original health/identity schema still rejects presentation as an unrelated sidecar field.
old=load('docs/examples/entity_components/7dtd_snapshot.json');old['components']['presentation']={'scale':1.5}
resources=Registry().with_resource('https://mc7dtd.invalid/schemas/entity_state_v2.schema.json',Resource.from_contents(load('docs/entity_state_v2.schema.json')))
check(bool(list(Draft202012Validator(load('docs/entity_components_v1.schema.json'),registry=resources).iter_errors(old))),'existing component protocol unchanged')
for p in (r/'docs/examples/presentation').glob('*.json'):
    schema.validate(json.loads(p.read_text(encoding='utf-8')))
check(True,'documentation examples')
print('TOTAL',count,'passed')
