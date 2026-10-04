import copy
import hashlib
import json
import unittest
from pathlib import Path
from component_contract import ROOT, ComponentOracle, COMPONENTS, decode_wire, validate, validate_core, protected_paths

EXAMPLES = ROOT / 'docs/examples/entity_components'
GRANTS = dict.fromkeys(COMPONENTS, True)  # Explicit tests-only grants, never production defaults.
def message(game='7dtd', event='snapshot'):
    return json.loads((EXAMPLES / f'{game}_{event}.json').read_text(encoding='utf-8-sig'))
def entity(m):
    return dict(m, sequence=m['entity_sequence'], active=True)

class ComponentTests(unittest.TestCase):
    def setUp(self):
        self.m = message()
        self.e = entity(self.m)
        self.oracle = ComponentOracle()
    def apply(self,m=None,grants=None,e=None,role='7dtd'):
        return self.oracle.apply(m or self.m,role,self.e if e is None else e,GRANTS if grants is None else grants)
    def reject(self,m,code,**kwargs):
        with self.assertRaisesRegex(ValueError,code): self.apply(m,**kwargs)
    def test_all_nine_examples(self):
        for path in EXAMPLES.glob('*.json'):
            m=decode_wire(path.read_text(encoding='utf-8-sig'));validate(m,m['source'],entity(m),GRANTS)
        self.assertEqual(len(list(EXAMPLES.glob('*.json'))),9)
    def test_both_game_sequences(self):
        for game in ('minecraft','7dtd'):
            o=ComponentOracle();m=message(game);e=entity(m)
            for event in ('snapshot','patch','remove_name','replace'):
                self.assertEqual(o.apply(message(game,event),game,e,GRANTS),'applied')
            self.assertEqual(o.find(m)['components'],{'name':{'text':'新的显示名'}})
    def test_patch_keeps_unmentioned_component(self):
        self.apply();self.apply(message(event='patch'))
        self.assertEqual(self.oracle.find(self.m)['components']['name'],self.m['components']['name'])
    def test_patch_health_is_replacement_not_damage(self):
        self.apply();self.apply(message(event='patch'))
        self.assertEqual(self.oracle.find(self.m)['components']['health'],{'current':90,'max':100})
    def test_null_removes_whole_component(self):
        self.apply();self.apply(message(event='patch'));self.apply(message(event='remove_name'))
        self.assertNotIn('name',self.oracle.find(self.m)['components'])
    def test_snapshot_clears_omitted_components(self):
        self.apply();self.apply(message(event='replace'))
        self.assertEqual(set(self.oracle.find(self.m)['components']),{'name'})
    def test_empty_snapshot_clears_all(self):
        self.apply();m=json.loads((EXAMPLES/'clear_snapshot.json').read_text(encoding='utf-8-sig'));self.apply(m)
        self.assertEqual(self.oracle.find(self.m)['components'],{})
    def test_patch_requires_snapshot(self):
        self.reject(message(event='patch'),'snapshot_required')
    def test_wrong_patch_base_is_atomic(self):
        self.apply();before=self.oracle.find(self.m);m=message(event='patch');m['base_revision']=99
        self.reject(m,'base_revision_mismatch');self.assertEqual(self.oracle.find(self.m),before)
    def test_same_revision_is_ignored(self):
        self.apply();m=copy.deepcopy(self.m);m['components']={};self.assertEqual(self.apply(m),'ignore_stale')
        self.assertEqual(self.oracle.find(self.m)['components'],self.m['components'])
    def test_snapshot_recovers_revision_gap(self):
        self.apply();self.apply(message(event='replace'));self.assertEqual(self.oracle.find(self.m)['revision'],5)
    def test_duplicate_patch_does_not_apply_twice(self):
        self.apply();m=message(event='patch');self.apply(m);self.assertEqual(self.apply(m),'ignore_stale')
    def test_source_session(self):self.reject(self.m,'source_session_mismatch',role='minecraft')
    def test_authority(self):
        m=copy.deepcopy(self.m);m['authority']='minecraft';self.reject(m,'non_authoritative_writer')
    def test_origin_identity(self):
        m=copy.deepcopy(self.m);m['origin']['entity_id']='00000000-0000-0000-0000-000000000001';self.reject(m,'origin_identity_mismatch')
    def test_origin_namespace(self):
        m=copy.deepcopy(self.m);m['entity_type']='minecraft:player';self.reject(m,'origin_namespace_mismatch')
    def test_registry_type(self):
        m=copy.deepcopy(self.m);m['entity_type']='7dtd:zombie';self.reject(m,'entity_registry_mismatch')
    def test_inactive_and_unknown(self):
        for e in (None,dict(self.e,active=False)):
            with self.assertRaisesRegex(ValueError,'inactive_entity'):validate(self.m,'7dtd',e,GRANTS)
    def test_stream(self):
        m=copy.deepcopy(self.m);m['stream_id']='00000000-0000-0000-0000-000000000001';self.reject(m,'inactive_stream')
    def test_reference_sequence(self):
        for seq in (2,100):
            m=copy.deepcopy(self.m);m['entity_sequence']=seq;self.reject(m,'entity_sequence_mismatch')
    def test_permission_missing_denies(self):self.reject(self.m,'component_permission_denied',grants={})
    def test_permission_false_denies(self):self.reject(self.m,'component_permission_denied',grants=dict(GRANTS,health=False))
    def test_snapshot_omission_requires_permission(self):
        self.apply();self.reject(message(event='replace'),'component_permission_denied',grants={'name':True})
    def test_health_relation_atomic(self):
        self.apply();before=self.oracle.find(self.m);m=message(event='patch');m['components']['health']['current']=101
        self.reject(m,'invalid_health_range');self.assertEqual(before,self.oracle.find(self.m))
    def test_partial_health_forbidden(self):
        m=message(event='patch');del m['components']['health']['max'];self.reject(m,'schema')
    def test_numeric_boundaries(self):
        for field,value in [('revision',0),('revision',9007199254740992),('revision',True),('entity_sequence',1.5),('base_revision',-1),('version',True)]:
            m=copy.deepcopy(self.m);m[field]=value;self.reject(m,'schema|integer_required')
    def test_health_bad_values(self):
        for field,value in [('current',-1),('max',0),('current',True),('current','100')]:
            m=copy.deepcopy(self.m);m['components']['health'][field]=value;self.reject(m,'schema')
    def test_name_controls_and_length(self):
        for value in ('','a'*129,'hello\nworld','x\x7f'):
            m=copy.deepcopy(self.m);m['components']['name']['text']=value;self.reject(m,'schema|invalid_name_control')
    def test_animation_namespace(self):
        m=copy.deepcopy(self.m);m['components']['animation_state']['state']='minecraft:attack';self.reject(m,'animation_namespace_mismatch')
    def test_unknown_component_and_field(self):
        for k,v in [('combat',{}),('input_control',{})]:
            m=copy.deepcopy(self.m);m['components'][k]=v;self.reject(m,'schema')
        m=copy.deepcopy(self.m);m['extra']=True;self.reject(m,'schema')
    def test_metadata_flat_and_key_limits(self):
        for value in ({'nested':{}},{'list':[]},{'1bad':True},{'x':'x'*257},{f'key{i}':i for i in range(33)}):
            m=copy.deepcopy(self.m);m['components']['custom_metadata']=value;self.reject(m,'schema')
    def test_metadata_null_is_value_and_map_replaced(self):
        self.apply();m=message(event='patch');m['components']={'custom_metadata':{'note':None}}
        self.apply(m);self.assertEqual(self.oracle.find(self.m)['components']['custom_metadata'],{'note':None})
    def test_patch_empty_and_snapshot_null_rejected(self):
        m=message(event='patch');m['components']={};self.reject(m,'schema')
        m=copy.deepcopy(self.m);m['components']['name']=None;self.reject(m,'schema')
    def test_duplicate_and_nonfinite_wire(self):
        for raw in ('{"type":"x","type":"y"}','{"x":NaN}','{"x":1e999}'):
            with self.assertRaises(ValueError):decode_wire(raw)
    def test_utf8_byte_budget(self):
        with self.assertRaisesRegex(ValueError,'message_too_large'):decode_wire(json.dumps({'text':'汉'*3000},ensure_ascii=False))
        m=copy.deepcopy(self.m);m['components']['custom_metadata']={f'key{i}':'汉'*256 for i in range(32)};self.reject(m,'message_too_large')
    def test_finite_semantic(self):
        m=copy.deepcopy(self.m);m['components']['health']['current']=float('inf');self.reject(m,'nonfinite_number')
    def test_retire_rejects_late_patch(self):
        self.apply();self.oracle.retire(self.e);self.reject(message(event='patch'),'inactive_entity',e=dict(self.e,active=False));self.assertIsNone(self.oracle.find(self.m))
    def test_source_clear_is_scoped(self):
        self.apply();mc=message('minecraft');self.oracle.apply(mc,'minecraft',entity(mc),GRANTS);self.oracle.clear_source('7dtd')
        self.assertIsNone(self.oracle.find(self.m));self.assertIsNotNone(self.oracle.find(mc))
    def test_caller_mutation_isolated(self):
        self.apply();self.m['components']['name']['text']='changed';self.assertNotEqual(self.oracle.find(self.m)['components']['name']['text'],'changed')
    def test_v2_core_examples_still_valid(self):
        for p in (ROOT/'docs/examples/entity_state_v2').glob('*.json'):
            m=json.loads(p.read_text(encoding='utf-8-sig'));validate_core(m,m['source'],m['stream_id'])
    def test_v2_does_not_accept_extension_fields(self):
        m=json.loads((ROOT/'docs/examples/entity_state_v2/7dtd_spawn.json').read_text(encoding='utf-8-sig'));m['components']={}
        with self.assertRaises(ValueError):validate_core(m,'7dtd',m['stream_id'])
    def test_production_files_unchanged(self):
        base=json.loads((ROOT/'work/phase3_7_0-test/protected-before.json').read_text(encoding='utf-8-sig'))
        self.assertEqual(set(protected_paths()),set(base),'production files added or removed')
        for p,digest in base.items():self.assertEqual(hashlib.sha256((ROOT/p).read_bytes()).hexdigest(),digest,p)

if __name__=='__main__':
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(ComponentTests))
    (ROOT/'work/phase3_7_0-test/results.json').write_text(json.dumps({'passed':result.wasSuccessful(),'testGroups':result.testsRun,'failures':len(result.failures),'errors':len(result.errors),'runtimeModified':False},indent=2),encoding='utf-8')
    raise SystemExit(not result.wasSuccessful())
