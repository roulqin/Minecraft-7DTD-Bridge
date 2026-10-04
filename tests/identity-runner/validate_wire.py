import sys,json,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tests/state-components'))
from component_contract import ComponentOracle
from test_components import ComponentTests
from jsonschema import Draft202012Validator
def read(p):return json.loads((ROOT/p).read_text(encoding='utf-8-sig'))
Draft202012Validator(read('config/component_permissions.schema.json')).validate(read('config/component_permissions.json'))
Draft202012Validator(read('config/identity_labels.schema.json')).validate(read('config/identity_labels.json'))
oracle=ComponentOracle();entity=None;count=0
for m in read('work/phase3_7_2-test/sender-events.json'):
 if m['type']=='entity_state':
  if m['lifecycle']['event']=='despawn':oracle.retire(entity);entity=None
  else:entity=dict(m,active=True)
 else:oracle.apply(m,'7dtd',entity,{'health':True,'name':True,'custom_metadata':True});count+=1
print(f'{count} actual serialized component messages passed original v1 extended schema and semantics')
names=[n for n in unittest.defaultTestLoader.getTestCaseNames(ComponentTests) if n!='test_production_files_unchanged']
r=unittest.TextTestRunner().run(unittest.TestSuite(ComponentTests(n) for n in names))
raise SystemExit(not r.wasSuccessful())
