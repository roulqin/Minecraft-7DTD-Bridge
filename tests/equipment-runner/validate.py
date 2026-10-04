import hashlib,json,sys
from pathlib import Path
from jsonschema import Draft202012Validator
from referencing import Registry,Resource
r=Path(__file__).resolve().parents[2]
def load(path):return json.loads((r/path).read_text(encoding='utf-8-sig'))
passed=0
def check(ok,label):
    global passed
    assert ok,label
    passed+=1
    print('PASS',label)
registry=Registry()
for name in ('entity_state_v2.schema.json','equipment_component_v1.schema.json'):
    data=load('docs/'+name);Draft202012Validator.check_schema(data)
    registry=registry.with_resource('https://mc7dtd.invalid/schemas/'+name,Resource.from_contents(data))
schema=load('docs/entity_components_v1.schema.json')
Draft202012Validator.check_schema(schema)
validator=Draft202012Validator(schema,registry=registry)
entity=None;count=0
for m in load('work/phase3_8_1-test/sender-events.json'):
    if m['type']=='entity_state':
        entity=m
    else:
        validator.validate(m)
        check(m['entity_sequence']==entity['sequence'],'serialized component references current entity sequence')
        count+=1
check(count>=5,'actual publisher equipment messages serialized')
for name in ('component_permissions','equipment_permissions','debug'):
    Draft202012Validator(load('config/'+name+'.schema.json')).validate(load('config/'+name+'.json'))
    check(True,name+' config schema')
Draft202012Validator(load('docs/entity_debug_inspector.schema.json')).validate(load('work/phase3_8_1-test/inspector-snapshot.json'))
check(True,'Bridge equipment Inspector schema')
for path,digest in load('work/phase3_8_1-test/protected-before.json').items():
    check(hashlib.sha256((r/path).read_bytes()).hexdigest().upper()==digest,'unchanged '+path)
# Existing design-phase regression has frozen-source assertions; run only the applicable
# preexisting component contract cases, not their obsolete production freeze assertion.
sys.path.insert(0,str(r/'tests/state-components'))
from test_components import ComponentTests
import unittest
names=[name for name in unittest.defaultTestLoader.getTestCaseNames(ComponentTests) if name!='test_production_files_unchanged']
result=unittest.TextTestRunner().run(unittest.TestSuite(ComponentTests(name) for name in names))
check(result.wasSuccessful(),'existing '+str(result.testsRun)+' component contract tests')
(r/'work/phase3_8_1-test/schema-results.json').write_text(json.dumps({'checks':passed,'legacy_contract_tests':result.testsRun,'passed':result.wasSuccessful()},indent=2)+'\n',encoding='utf-8')
print('TOTAL',passed,'checks and',result.testsRun,'legacy contract tests passed')
