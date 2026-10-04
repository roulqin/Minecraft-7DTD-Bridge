import json,math
from pathlib import Path
bones=[]
def bone(name,parent,p): bones.append(dict(name=name,parent=parent,position=p))
bone('Hips','Root',[0,.9,0]);bone('Spine','Hips',[0,.2,0]);bone('Chest','Spine',[0,.2,0]);bone('Neck','Chest',[0,.2,0]);bone('Head','Neck',[0,.15,0])
for side,sgn in [('Left',-1),('Right',1)]:
 bone(side+'Shoulder','Chest',[sgn*.15,.08,0]);bone(side+'UpperArm',side+'Shoulder',[sgn*.15,0,0]);bone(side+'LowerArm',side+'UpperArm',[sgn*.3,0,0]);bone(side+'Hand',side+'LowerArm',[sgn*.25,0,0]);bone(side+'UpperLeg','Hips',[sgn*.1,0,0]);bone(side+'LowerLeg',side+'UpperLeg',[0,-.42,0]);bone(side+'Foot',side+'LowerLeg',[0,-.42,.04])
vertices=[];uv=[];triangles=[];weights=[]
def cube(pos,size,atlas,w,h,d,bone_name,arm_sign=0):
 a,b,c=[s/2 for s in size];x,y=atlas
 faces=[([(-a,-b,c),(a,-b,c),(a,b,c),(-a,b,c)],(x+d,y+d),w,h),([(a,-b,-c),(-a,-b,-c),(-a,b,-c),(a,b,-c)],(x+2*d+w,y+d),w,h),([(-a,-b,-c),(-a,-b,c),(-a,b,c),(-a,b,-c)],(x,y+d),d,h),([(a,-b,c),(a,-b,-c),(a,b,-c),(a,b,c)],(x+d+w,y+d),d,h),([(-a,b,c),(a,b,c),(a,b,-c),(-a,b,-c)],(x+d,y),w,d),([(-a,-b,-c),(a,-b,-c),(a,-b,c),(-a,-b,c)],(x+d+w,y),w,d)]
 bi=next(i for i,b in enumerate(bones) if b['name']==bone_name)
 for points,(u,v),pw,ph in faces:
  start=len(weights)
  for px,py,pz in points:
   px+=pos[0];py+=pos[1];pz+=pos[2]
   if arm_sign:
    rx=px-arm_sign*.3;ry=py-1.38;px=arm_sign*.3-arm_sign*ry;py=1.38+arm_sign*rx
   vertices.extend([round(px,6),round(py,6),round(pz,6)]);weights.append(bi)
  uv.extend([u/64,1-(v+ph)/64,(u+pw)/64,1-(v+ph)/64,(u+pw)/64,1-v/64,u/64,1-v/64]);triangles.extend([start,start+1,start+2,start,start+2,start+3])
cube([0,1.62,0],[.4,.4,.4],[0,0],8,8,8,'Head');cube([0,1.12,0],[.4,.6,.2],[16,16],8,12,4,'Spine')
cube([.3,1.08,0],[.15,.6,.2],[40,16],3,12,4,'RightUpperArm',1);cube([-.3,1.08,0],[.15,.6,.2],[32,48],3,12,4,'LeftUpperArm',-1)
cube([.1,.52,0],[.2,.6,.2],[0,16],4,12,4,'RightUpperLeg');cube([-.1,.52,0],[.2,.6,.2],[16,48],4,12,4,'LeftUpperLeg')
p=Path('assets/avatar/prototype/alex_slim_mesh.json');p.write_text(json.dumps(dict(vertices=vertices,uv=uv,triangles=triangles,weights=weights,bones=bones)),encoding='utf-8')
print('Prototype resource:',len(weights),'vertices;',len(triangles)//3,'triangles;',len(bones),'bones')
