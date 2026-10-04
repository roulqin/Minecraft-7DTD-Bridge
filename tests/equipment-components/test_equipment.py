import copy
import hashlib
import json
import unittest
from pathlib import Path
from equipment_contract import ROOT, SLOTS, COMPONENTS, VALIDATOR, EquipmentOracle, validate, decode_wire
from component_contract import VALIDATOR as LEGACY_VALIDATOR

EXAMPLES = ROOT / 'docs/examples/equipment_components'
GRANTS = {c:{'publish':True,'store':True} for c in COMPONENTS}
def load(name):
    return json.loads((EXAMPLES / (name + '.json')).read_text(encoding='utf-8'))

class EquipmentTests(unittest.TestCase):
    def setUp(self):
        self.m = load('initial')
        self.e = dict(self.m, active=True, sequence=1)
        self.o = EquipmentOracle()
    def apply(self, m=None, grants=None, entity=None, role='7dtd'):
        return self.o.apply(self.m if m is None else m, role, self.e if entity is None else entity, GRANTS if grants is None else grants)
    def reject(self, m, **kwargs):
        before = copy.deepcopy(self.o.records)
        with self.assertRaises(ValueError):
            self.apply(m, **kwargs)
        self.assertEqual(before, self.o.records)
    def patch(self, components):
        m=copy.deepcopy(self.m);m.update(mode='patch',revision=2,base_revision=1,components=components);return m
    def test_example_sequence(self):
        for name in ('initial','slot_update','slot_clear','health_update','remove','add','resnapshot','snapshot_without_equipment'):
            self.assertEqual(self.apply(load(name)), 'applied')
        self.assertNotIn('equipment',self.o.find(self.m)['components'])
        self.assertEqual(self.o.find(self.m)['components']['health']['current'],92)
    def test_single_slot_preserves_other_slots_and_components(self):
        self.apply();self.apply(load('slot_update'));state=self.o.find(self.m)['components']
        self.assertEqual(state['equipment']['slots']['head'],self.m['components']['equipment']['slots']['head'])
        for c in ('health','name','custom_metadata'): self.assertEqual(state[c],self.m['components'][c])
    def test_slot_clear_retains_component(self):
        self.apply();m=self.patch({'equipment':{'slots':{'head':None}}});self.apply(m)
        self.assertIsNone(self.o.find(self.m)['components']['equipment']['slots']['head'])
        self.assertEqual(set(self.o.find(self.m)['components']['equipment']['slots']),SLOTS)
    def test_remove_retains_health_identity_and_revision(self):
        self.apply();self.apply(self.patch({'equipment':None}));s=self.o.find(self.m)
        self.assertEqual(s['revision'],2);self.assertNotIn('equipment',s['components'])
        for c in ('health','name','custom_metadata'): self.assertEqual(s['components'][c],self.m['components'][c])
    def test_all_empty_is_known_empty(self):
        m=copy.deepcopy(self.m);m['components']['equipment']['slots']=dict.fromkeys(SLOTS);self.apply(m)
        self.assertEqual(self.o.find(m)['components']['equipment']['slots'],dict.fromkeys(SLOTS))
    def test_unknown_component_distinct_from_empty(self):
        m=copy.deepcopy(self.m);m['components'].pop('equipment');self.apply(m)
        self.assertNotIn('equipment',self.o.find(m)['components'])
    def test_add_after_remove_requires_all_slots(self):
        self.apply();self.apply(self.patch({'equipment':None}));m=load('slot_update');m.update(revision=3,base_revision=2);self.reject(m)
        m['components']['equipment']['slots']=dict.fromkeys(SLOTS);self.assertEqual(self.apply(m),'applied')
    def test_empty_snapshot_clears_shared_set(self):
        self.apply();m=copy.deepcopy(self.m);m.update(revision=2,components={});self.apply(m);self.assertEqual(self.o.find(m)['components'],{})
    def test_snapshot_without_equipment_removes_it(self):
        self.apply();self.apply(load('snapshot_without_equipment'));self.assertNotIn('equipment',self.o.find(self.m)['components'])
    def test_snapshot_omitted_delete_permission_checked_even_if_stale(self):
        self.apply();m=copy.deepcopy(self.m);m['components'].pop('equipment');grants=copy.deepcopy(GRANTS);grants['equipment']['store']=False;self.reject(m,grants=grants)
    def test_health_only_patch_preserves_equipment(self):
        self.apply();self.apply(self.patch({'health':{'current':90,'max':100}}));self.assertEqual(self.o.find(self.m)['components']['equipment'],self.m['components']['equipment'])
    def test_name_and_metadata_keep_replacement_semantics(self):
        self.apply();self.apply(self.patch({'name':{'text':'new'},'custom_metadata':{'tag.source':'7dtd'}}));s=self.o.find(self.m)['components'];self.assertEqual(s['name'],{'text':'new'});self.assertEqual(s['custom_metadata'],{'tag.source':'7dtd'})
    def test_stale_valid_patch_ignored(self):
        self.apply();m=load('slot_update');self.apply(m);self.assertEqual(self.apply(m),'ignore_stale')
    def test_bad_stale_content_rejected(self):
        self.apply();m=copy.deepcopy(self.m);m['components']['equipment']['slots']['head']={};self.reject(m)
    def test_patch_without_baseline(self): self.reject(load('slot_update'))
    def test_wrong_shared_base_atomic(self):
        self.apply();m=load('slot_update');m['base_revision']=9;self.reject(m)
    def test_equipment_and_bad_health_atomic(self):
        self.apply();self.reject(self.patch({'equipment':{'slots':{'head':None}},'health':{'current':101,'max':100}}))
    def test_missing_publish_or_store_permission(self):
        for permission in ('publish','store'):
            grants=copy.deepcopy(GRANTS);grants['equipment'][permission]=False;self.reject(self.m,grants=grants)
        grants=copy.deepcopy(GRANTS);grants.pop('equipment');self.reject(self.m,grants=grants)
    def test_remove_needs_permission(self):
        self.apply();grants=copy.deepcopy(GRANTS);grants['equipment']['publish']=False;self.reject(self.patch({'equipment':None}),grants=grants)
    def test_role_authority_origin_type_stream_sequence(self):
        self.reject(self.m,role='minecraft')
        for field,value in [('authority','minecraft'),('entity_type','7dtd:zombie'),('stream_id','00000000-0000-0000-0000-000000000001'),('entity_sequence',2)]:
            m=copy.deepcopy(self.m);m[field]=value;self.reject(m)
        m=copy.deepcopy(self.m);m['origin']['world_id']='other';self.reject(m)
    def test_unknown_or_retired_entity(self):
        with self.assertRaises(ValueError):validate(self.m,'7dtd',None,GRANTS)
        e=copy.deepcopy(self.e);e['active']=False;self.reject(self.m,entity=e)
    def test_despawn_cleans_and_reconnect_requires_snapshot(self):
        self.apply();self.o.retire(self.e);self.assertIsNone(self.o.find(self.m));self.reject(load('slot_update'));self.apply();self.o.clear_source('7dtd');self.assertIsNone(self.o.find(self.m));self.apply()
    def test_same_uuid_different_world_isolated(self):
        self.apply();m=copy.deepcopy(self.m);m['world_id']=m['origin']['world_id']='other';e=dict(m,active=True,sequence=1);self.apply(m,entity=e);self.assertEqual(len(self.o.records),2);self.o.retire(e);self.assertIsNotNone(self.o.find(self.m))
    def test_find_returns_deep_copy(self):
        self.apply();s=self.o.find(self.m);s['components']['equipment']['slots']['head']=None;self.assertIsNotNone(self.o.find(self.m)['components']['equipment']['slots']['head'])
    def test_slot_and_item_bounds(self):
        for slots in ({},{'helmet':None},{'head':{}},{'head':{'item_id':'minecraft:stone'}},{'head':{'item_id':'7dtd:a\n'}},{'head':{'item_id':'7dtd:a','quality':1}},{'head':{'item_id':'7dtd:'+'a'*123}}):
            self.reject(self.patch({'equipment':{'slots':slots}}))
    def test_snapshot_requires_complete_slots_and_nonnull_component(self):
        for value in (None,{}, {'slots':{'head':None}}):
            m=copy.deepcopy(self.m);m['components']['equipment']=value;self.reject(m)
    def test_boolean_revision_and_empty_patch(self):
        m=copy.deepcopy(self.m);m['revision']=True;self.reject(m);self.reject(self.patch({}))
    def test_duplicate_keys_nonfinite_and_raw_byte_budget(self):
        for raw in ('{"slots":{},"slots":{}}','{"x":NaN}','{"x":1e999}', '{"x":"'+'a'*8192+'"}', '['*9+'0'+']'*9):
            with self.assertRaises(ValueError):decode_wire(raw)
    def test_old_runtime_rejects_equipment_but_legacy_messages_valid_in_draft(self):
        self.assertTrue(list(LEGACY_VALIDATOR.iter_errors(self.m)))
        for path in (ROOT/'docs/examples/identity_components').glob('*.json'):
            m=json.loads(path.read_text(encoding='utf-8-sig'));self.assertEqual(list(VALIDATOR.iter_errors(m)),[])
        policy=load('permissions.draft');self.assertEqual(policy['types'][0]['components']['equipment'],{'publish':True,'store':True})
    def test_production_files_unchanged(self):
        baseline=json.loads((ROOT/'work/phase3_8_0-test/protected-before.json').read_text(encoding='utf-8-sig'))
        for path,digest in baseline.items():self.assertEqual(hashlib.sha256(Path(path).read_bytes()).hexdigest().upper(),digest,path)

if __name__=='__main__':
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(EquipmentTests))
    (ROOT/'work/phase3_8_0-test/results.json').write_text(json.dumps({'tests':result.testsRun,'failures':len(result.failures),'errors':len(result.errors),'passed':result.wasSuccessful()},indent=2)+'\n',encoding='utf-8')
    raise SystemExit(not result.wasSuccessful())
