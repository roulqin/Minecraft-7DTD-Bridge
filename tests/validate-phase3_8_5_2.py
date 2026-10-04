"""Independent source contracts and real-engine evidence checks."""
import json, math, struct
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parent.parent
A=ROOT/'assets/avatar';E=ROOT/'work/phase3852-test'
passed=[]
def check(ok,label):
 if not ok:raise AssertionError(label)
 passed.append(label+' PASS')
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
config=read(A/'avatar_config.json')
check(config['skeleton']=='minecraft_avatar_v1' and config['variant']=='alex_slim','Production Config')
skin=Image.open(A/config['skin']).convert('RGBA');check(skin.size==(64,64),'Skin 64x64')
skeleton=read(A/'skeletons/minecraft_avatar_v1.json')
check({n['name'] for n in skeleton['public_nodes']}=={'root','body','head','arm_left','arm_right','leg_left','leg_right'},'Seven Public Nodes')
names={'InternalRig'}
for b in skeleton['internal_bones']:
 check(b['parent'] in names and b['name'] not in names,'Bone Parent '+b['name']);names.add(b['name'])
expected={'Head':([8,8,8],[0,0],[32,0],.5),'Body':([8,12,4],[16,16],[16,32],.25),'Arm_L':([3,12,4],[32,48],[48,48],.25),'Arm_R':([3,12,4],[40,16],[40,32],.25),'Leg_L':([4,12,4],[16,48],[0,48],.25),'Leg_R':([4,12,4],[0,16],[0,32],.25)}
for part,(dimensions,base,layer,pad) in expected.items():
 for suffix,origin,inflate in [('base',base,0),('layer2',layer,pad)]:
  m=read(A/f'models/minecraft_avatar_v1/{part}_{suffix}.mesh.json');w,h,d=dimensions;x,y=origin
  rects={'front':[x+d,y+d,w,h],'back':[x+2*d+w,y+d,w,h],'right':[x,y+d,d,h],'left':[x+d+w,y+d,d,h],'top':[x+d,y,w,d],'bottom':[x+d+w,y,w,d]}
  check(m['dimensions_pixels']==dimensions and m['inflate_pixels']==inflate and {r['face']:r['pixels'] for r in m['face_rects']}==rects,part+' '+suffix+' UV Rectangles')
  check(len(m['vertices'])==72 and len(m['uv'])==48 and len(m['triangles'])==36 and all(0<=v<=1 for v in m['uv']) and all(0<=i<24 for i in m['triangles']),part+' '+suffix+' Mesh Data')
  if suffix=='layer2':
   pixels=[skin.getpixel((px,py))[3] for r in rects.values() for py in range(r[1],r[1]+r[3]) for px in range(r[0],r[0]+r[2])]
   check(any(pixels),part+' Overlay Skin Pixels')
controller=read(A/'animations/MinecraftAvatarAnimator.source.json')
def f32(value):return struct.unpack('<f',struct.pack('<f',value))[0]
def state(speed):
 speed=f32(speed)
 return 'Idle' if speed<=f32(.1) else 'Walk' if speed<=1 else 'Run'
for start in ['Idle','Walk','Run']:
 for speed in [0,.1,.10000002,.5,1,1.0000002,2,-1]:
  speed=struct.unpack('<f',struct.pack('<f',speed))[0]
  edges=[t for t in controller['transitions'] if t['source']==start and all(speed>f32(v) if mode=='Greater' else speed<f32(v) for mode,v in t['conditions'])]
  target=state(speed);check((not edges if target==start else len(edges)==1 and edges[0]['target']==target),f'Transition {start} speed={speed}')
for label in ['Avatar Mesh Load','Base Layer','Layer2','Bone Hierarchy','Prefab Load','Idle Mesh Animation','Walk Mesh Animation','Run Mesh Animation','Layer2 Transparent Pixels','Bundled Prefab Load']:
 check(label+' PASS' in (E/'unity-resource-results.txt').read_text(),label+' Engine Evidence')
check('RESOURCE ERROR' not in (E/'7dtd-resource-results.txt').read_text() and (E/'7dtd-resource-results.txt').read_text().count('PASS')==10,'7DTD Bundle Compatibility')
check('TOTAL 38 passed' in (ROOT/'work/phase3841-test/RendererHarness.log').read_text(),'Equipment Renderer Compatibility')
manifest=read(A/'resource_manifest.json')
import hashlib
check(hashlib.sha256((A/manifest['bundle']).read_bytes()).hexdigest().upper()==manifest['bundle_sha256'],'Resource Bundle Hash')
game=read(E/'game-resource-validation.json')
check(game['status']=='verified_7dtd' and game['bundle_sha256']==manifest['bundle_sha256'],'Game Validation Bundle Hash')
(E/'source-contract-results.txt').write_text('\n'.join(passed)+f'\nTOTAL {len(passed)} passed\n',encoding='utf-8')
print(f'TOTAL {len(passed)} resource contract checks PASS')
