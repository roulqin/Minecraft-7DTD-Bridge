from pathlib import Path
import json,hashlib
from jsonschema import Draft202012Validator
r=Path(__file__).resolve().parents[2]
def load(p): return json.loads((r/p).read_text(encoding='utf-8-sig'))
passed=0
def check(ok,label):
    global passed
    assert ok,label
    passed+=1;print('PASS',label)
schema=Draft202012Validator(load('config/debug.schema.json'));schema.validate(load('config/debug.json'))
check(True,'debug config schema')
for value in [{'debug_name_tag':False,'debug_logging':False},{'debug_name_tag':True,'debug_logging':False},{'debug_name_tag':False,'debug_logging':True}]:schema.validate(value)
check(True,'independent name tag and logging flags')
for invalid in [{},{'debug_name_tag':'true','debug_logging':True},{'debug_name_tag':True,'debug_logging':1},{'debug_name_tag':True,'debug_logging':True,'extra':True}]:check(bool(list(schema.iter_errors(invalid))),'invalid debug config rejected')
snapshot=load('work/phase3_7_4-test/inspector-snapshot.json')
validator=Draft202012Validator(load('docs/entity_debug_inspector.schema.json'));validator.validate(snapshot)
check(True,'Inspector response diagnostic schema')
for component in ['identity','health','presentation']:
    missing=json.loads(json.dumps(snapshot));missing['components'][component]={};validator.validate(missing)
    check(True,'optional missing '+component)
for p,expected in load('work/phase3_7_4-test/protected-hashes.json').items():check(hashlib.sha256((r/p).read_bytes()).hexdigest()==expected,'unchanged '+p)
# Check that the existing WebSocket implementation differs only by read-only debug observer hooks.
before=(r/'work/phase3_7_4-test/Program-before.cs.txt').read_text(encoding='utf-8-sig')
after=(r/'bridge-server/Program.cs').read_text(encoding='utf-8-sig')
before=before[before.index('app.UseWebSockets'):]
after=after[after.index('app.UseWebSockets'):]
after='\n'.join(line for line in after.splitlines() if 'inspector.Clear();' not in line and 'inspector.ObserveAccepted(entity);' not in line)+'\n'
check(before.strip()==after.strip(),'WebSocket implementation unchanged except debug observers')
mc=(r/'minecraft-mod/src/main/java/io/mc7dtd/MinecraftProxyScene.java').read_text()
check('Blocks.CYAN_CONCRETE.getDefaultState()' in mc and 'new Vector3f(0.6f, 1.8f, 0.3f)' in mc,'original proxy visual shape retained')
for name in ['mc7dtd_entity_inspect','mc7dtd_debug_entities']:check(name in (r/'minecraft-mod/src/main/java/io/mc7dtd/EntityDebugCommands.java').read_text(),'command registered '+name)
print('TOTAL',passed,'passed')
