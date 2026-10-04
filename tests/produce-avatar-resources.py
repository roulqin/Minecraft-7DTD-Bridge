"""Deterministic source resources; native Prefab/Controller are built by Unity Editor."""
import json, math, struct
from pathlib import Path

ROOT=Path(__file__).resolve().parent.parent
A=ROOT/'assets/avatar'
UNIT=1/16
parts=[
 ('Head','head',[0,28,0],[8,8,8],[0,0],[32,0],.5,'rig_head'),
 ('Body','body',[0,18,0],[8,12,4],[16,16],[16,32],.25,'rig_spine'),
 ('Arm_L','arm_left',[-5.5,18,0],[3,12,4],[32,48],[48,48],.25,'rig_arm_left_upper'),
 ('Arm_R','arm_right',[5.5,18,0],[3,12,4],[40,16],[40,32],.25,'rig_arm_right_upper'),
 ('Leg_L','leg_left',[-2,6,0],[4,12,4],[16,48],[0,48],.25,'rig_leg_left_upper'),
 ('Leg_R','leg_right',[2,6,0],[4,12,4],[0,16],[0,32],.25,'rig_leg_right_upper'),
]
bones=[]
def bone(name,parent,p,human):bones.append(dict(name=name,parent=parent,position=p,human=human))
bone('rig_hips','InternalRig',[0,.75,0],'Hips')
bone('rig_spine','rig_hips',[0,.25,0],'Spine')
bone('rig_chest','rig_spine',[0,.25,0],'Chest')
bone('rig_neck','rig_chest',[0,.15,0],'Neck')
bone('rig_head','rig_neck',[0,.1,0],'Head')
for side,s in [('left',-1),('right',1)]:
 title=side.title()
 bone(f'rig_shoulder_{side}','rig_chest',[s*.15,.125,0],title+'Shoulder')
 bone(f'rig_arm_{side}_upper',f'rig_shoulder_{side}',[s*.19375,0,0],title+'UpperArm')
 bone(f'rig_arm_{side}_lower',f'rig_arm_{side}_upper',[s*.375,0,0],title+'LowerArm')
 bone(f'rig_hand_{side}',f'rig_arm_{side}_lower',[s*.25,0,0],title+'Hand')
 bone(f'rig_leg_{side}_upper','rig_hips',[s*.125,0,0],title+'UpperLeg')
 bone(f'rig_leg_{side}_lower',f'rig_leg_{side}_upper',[0,-.375,0],title+'LowerLeg')
 bone(f'rig_foot_{side}',f'rig_leg_{side}_lower',[0,-.355,.08],title+'Foot')
public=[dict(name='root',parent='AvatarRoot',source=None)]
public += [dict(name=p[1],parent='root',source=p[7]) for p in parts]
def write(path,data):
 path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
write(A/'skeletons/minecraft_avatar_v1.json',dict(id='minecraft_avatar_v1',public_nodes=public,internal_bones=bones,coordinate_system='Unity +Y up, +Z forward; left is -X',unit_per_pixel=UNIT))

meshes=[]
for name,anchor,center,size,base,overlay,inflate,bone_name in parts:
 for layer,origin,pad in [('base',base,0),('layer2',overlay,inflate)]:
  w,h,d=size;a,b,c=[(n/2+pad)*UNIT for n in size];x,y=origin
  faces=[('front',[(-a,-b,c),(a,-b,c),(a,b,c),(-a,b,c)],(x+d,y+d),w,h),('back',[(a,-b,-c),(-a,-b,-c),(-a,b,-c),(a,b,-c)],(x+2*d+w,y+d),w,h),('right',[(-a,-b,-c),(-a,-b,c),(-a,b,c),(-a,b,-c)],(x,y+d),d,h),('left',[(a,-b,c),(a,-b,-c),(a,b,-c),(a,b,c)],(x+d+w,y+d),d,h),('top',[(-a,b,c),(a,b,c),(a,b,-c),(-a,b,-c)],(x+d,y),w,d),('bottom',[(-a,-b,-c),(a,-b,-c),(a,-b,c),(-a,-b,c)],(x+d+w,y),w,d)]
  verts=[];uv=[];tri=[];rects=[]
  for face,points,(u,v),fw,fh in faces:
   start=len(verts)//3
   for px,py,pz in points:
    px+=center[0]*UNIT;py+=center[1]*UNIT;pz+=center[2]*UNIT
    if name.startswith('Arm'):
     s=-1 if name=='Arm_L' else 1; pivot=s*.34375;rx=px-pivot;ry=py-1.375;px=pivot-s*ry;py=1.375+s*rx
    verts.extend([round(px,7),round(py,7),round(pz,7)])
   # Bottom face is reflected vertically relative to side/top atlas wrapping.
   corners=[(u,v+fh),(u+fw,v+fh),(u+fw,v),(u,v)] if face!='bottom' else [(u,v),(u+fw,v),(u+fw,v+fh),(u,v+fh)]
   uv.extend(n for pu,pv in corners for n in [pu/64,1-pv/64]);tri.extend([start,start+1,start+2,start,start+2,start+3]);rects.append(dict(face=face,pixels=[u,v,fw,fh]))
  m=dict(name=name+'_'+layer,part=name,anchor=anchor,bone=bone_name,layer=layer,dimensions_pixels=size,inflate_pixels=pad,vertices=verts,uv=uv,triangles=tri,face_rects=rects)
  meshes.append(m);write(A/f'models/minecraft_avatar_v1/{m["name"]}.mesh.json',m)
  obj=['# Authored Minecraft Alex Slim mesh; no external model asset','o '+m['name']]
  obj += ['v '+' '.join(map(str,verts[i:i+3])) for i in range(0,len(verts),3)]
  obj += [f'vt {uv[i]} {uv[i+1]}' for i in range(0,len(uv),2)]
  obj += ['f '+' '.join(f'{j+1}/{j+1}' for j in tri[i:i+3]) for i in range(0,len(tri),3)]
  (A/f'models/minecraft_avatar_v1/{m["name"]}.obj').write_text('\n'.join(obj)+'\n',encoding='utf-8')

paths={}
for b in bones:paths[b['name']]=paths.get(b['parent'],b['parent'])+'/'+b['name']
animations=[]
for state,duration,swing in [('Idle',2,1),('Walk',1,25),('Run',.6,45)]:
 curves=[]
 for side,s in [('left',1),('right',-1)]:
  for limb,opposite in [('arm',-1),('leg',1)]:
   path=paths[f'rig_{limb}_{side}_upper']
   curves.append(dict(path=path,property='localEulerAnglesRaw.x',keys=[[round(duration*i/32,6),round(math.sin(2*math.pi*i/32)*swing*s*opposite,6)] for i in range(33)]))
   if limb=='arm':curves.append(dict(path=path,property='localEulerAnglesRaw.z',keys=[[0,90*s],[duration,90*s]]))
 curves.append(dict(path=paths['rig_spine'],property='localEulerAnglesRaw.z',keys=[[round(duration*i/32,6),round(math.sin(2*math.pi*i/32),6)] for i in range(33)]))
 animation=dict(name=state,duration=duration,loop=True,curves=curves);animations.append(animation);write(A/f'animations/{state}.clip.json',animation)
for state,duration in [('JumpStart',.18),('JumpLoop',.5),('Fall',.5),('Land',.18)]:
 curves=[]
 for side,s in [('left',1),('right',-1)]:
  for limb in ['arm','leg']:
   # Stationary airborne pose stays straight. Moving airborne states blend Walk/Run below.
   angle=0
   curves.append(dict(path=paths[f'rig_{limb}_{side}_upper'],property='localEulerAnglesRaw.x',keys=[[0,angle],[duration,0 if state=='Land' else angle]]))
   if limb=='arm':curves.append(dict(path=paths[f'rig_{limb}_{side}_upper'],property='localEulerAnglesRaw.z',keys=[[0,90*s],[duration,90*s]]))
 animation=dict(name=state,duration=duration,loop=state in ['JumpLoop','Fall'],curves=curves);animations.append(animation)
import copy
for base,swing in [('Walk',8),('Run',4)]:
 held=copy.deepcopy(next(a for a in animations if a['name']==base));held['name']='Held'+base
 for curve in held['curves']:
  if 'rig_arm_' in curve['path'] and curve['property']=='localEulerAnglesRaw.x':
   original=25 if base=='Walk' else 45
   curve['keys']=[[t,round(angle*swing/original,6)] for t,angle in curve['keys']]
 animations.append(held)
# Uniform complete channels include elbows/hands/knees/feet; head look remains a separate layer.
animated=[b for b in bones if b['name']!='rig_head']
for animation in animations:
 existing={(c['path'],c['property']) for c in animation['curves']}
 for b in animated:
  for axis in 'xyz':
   key=(paths[b['name']],f'localEulerAnglesRaw.{axis}')
   if key not in existing:animation['curves'].append(dict(path=key[0],property=key[1],keys=[[0,0],[animation['duration'],0]]))
 write(A/f'animations/{animation["name"]}.clip.json',animation)
controller=dict(name='MinecraftAvatarAnimator',backend='generic_transform_controller',parameters=[dict(name='speed',type='float',default=0),dict(name='isGrounded',type='bool',default=True),dict(name='verticalVelocity',type='float',default=0),dict(name='actionState',type='int',default=0),dict(name='jumpStart',type='trigger')],default='Ground',states=['Ground','JumpStart','JumpLoop','Fall','Land'],ground_blend_tree={'parameter':'speed','thresholds':{'Idle':0,'Walk':2,'Run':5}},flow=['Ground','JumpStart','JumpLoop','Fall','Land','Ground'],transition_duration=.12,root_motion=False,write_defaults=False,layer_count=1,avatar_mask=None)
controller['air_blend_trees']={state:{'parameter':'speed','thresholds':{state:0,'Walk':2,'Run':5}} for state in ['JumpStart','JumpLoop','Fall','Land']}
controller['land_playback_rate']=4
write(A/'animations/MinecraftAvatarAnimator.source.json',controller)
write(A/'prefabs/MinecraftAvatarPrefab.source.json',dict(name='MinecraftAvatarPrefab',root='AvatarRoot',skeleton='minecraft_avatar_v1',render_nodes=[p[0] for p in parts],mesh_layers=['base','layer2'],controller='MinecraftAvatarAnimator',humanoid_avatar='MinecraftAvatarHumanoid',animator_avatar='MinecraftAvatarGeneric'))
write(A/'materials/minecraft_skin_materials.json',dict(shader='MC7DTD/AvatarSkin',base=dict(alpha_cutoff=.5,queue=2450),layer2=dict(alpha_cutoff=.5,queue=2451),blend='off',depth_write=True,filter='point',mipmaps=False,wrap='clamp',skin_size=[64,64]))

def floats(values):return ','.join(f'{v:.8g}f' for v in values)
cs=['// Generated by tests/produce-avatar-resources.py; resource authoring only.','using UnityEngine;','public static class AvatarProductionSpec {','public static readonly BoneSpec[] Bones = new BoneSpec[] {']
for b in bones:cs.append(f'new BoneSpec("{b["name"]}","{b["parent"]}",new Vector3({floats(b["position"])}),"{b["human"]}"),')
cs+=['};','public static readonly MeshSpec[] Meshes = new MeshSpec[] {']
for m in meshes:cs.append(f'new MeshSpec("{m["name"]}","{m["part"]}","{m["bone"]}","{m["layer"]}",new float[]{{{floats(m["vertices"])}}},new float[]{{{floats(m["uv"])}}},new int[]{{{",".join(map(str,m["triangles"]))}}}),')
cs+=['};','public static readonly ClipSpec[] Clips = new ClipSpec[] {']
for anim in animations:
 cs.append(f'new ClipSpec("{anim["name"]}",new CurveSpec[]{{')
 for c in anim['curves']:cs.append(f'new CurveSpec("{c["path"]}","{c["property"]}",new float[]{{{floats([n for k in c["keys"] for n in k])}}}),')
 cs.append('}),')
cs+=['};','}']
(A/'production_unity/Assets/Avatar/Editor/AvatarProductionSpec.generated.cs').write_text('\n'.join(cs)+'\n',encoding='utf-8')
write(A/'models/minecraft_avatar_v1/manifest.json',dict(mesh='MinecraftAvatarHumanoidMesh',variant='alex_slim',parts=[p[0] for p in parts],layers=2,vertices=288,triangles=144,height_units=2,unit_per_pixel=UNIT,resource_kind='source_mesh_catalog'))
print(f'Authored 12 mesh sources, 19 internal bones, 7 public nodes, {len(animations)} animation clip sources.')
