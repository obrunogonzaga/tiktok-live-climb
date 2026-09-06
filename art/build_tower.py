"""Build the reference-study masonry tower and reusable ledge in Blender 4.5."""
import bpy, math, random
from pathlib import Path
from mathutils import Vector
random.seed(1402)
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'game/Assets/Art/Environment';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
materials=[]
for i in range(6):
 m=bpy.data.materials.new(f'Stone{i}');m.diffuse_color=(.34+i*.02,.36+i*.02,.42+i*.02,1);materials.append(m)
mortar=bpy.data.materials.new('Mortar');mortar.diffuse_color=(.045,.055,.075,1)
ivy=bpy.data.materials.new('Ivy');ivy.diffuse_color=(.055,.13,.025,1)

def brick(name,pos,scale,bevel=.04,mat=None,rough=True):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if rough:
  for v in o.data.vertices:v.co+=Vector((random.uniform(-.018,.018),random.uniform(-.014,.014),random.uniform(-.014,.014)))
 mod=o.modifiers.new('Worn stone edges','BEVEL');mod.width=bevel;mod.segments=2
 bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 o.data.materials.append(mat or random.choice(materials))
 uv=o.data.uv_layers.new(name='UVMap')
 for poly in o.data.polygons:
  n=poly.normal;axis=max(range(3),key=lambda i:abs(n[i]));axes=[i for i in range(3) if i!=axis]
  for li in poly.loop_indices:
   p=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co
   uv.data[li].uv=(p[axes[0]]/2.1,p[axes[1]]/2.1)
 return o

# Dark structural shell, visible only in the mortar joints and deep arch reveal.
brick('Structural masonry core',(0,0,7),(4.18,4.18,14),.06,mortar,False)
for row in range(35):
 z=row*.4+.2
 width=4.4 - .018*z
 count=7;step=width/count
 for side in range(4):
  for col in range(count):
   a=-width/2+(col+.5)*step+(0.07 if row%2 else -.07)
   # A recessed blind arch on each face, repeated on the modular tower.
   if abs(a-.7)<.59 and 4.8<z<6.6: continue
   if abs(a-.7)<.43 and 6.6<=z<7: continue
   if side%2==0:
    pos=(a,(-1 if side==0 else 1)*(width/2),z);scale=(step-.025,.32+random.random()*.10,.365)
   else:
    pos=((-1 if side==1 else 1)*(width/2),a,z);scale=(.32+random.random()*.1,step-.025,.365)
   brick(f'Masonry_{side}_{row:02}_{col}',pos,scale)
# Corner piers and projecting string courses give the tower an architectural silhouette.
for h in [-.2,3.2,7.6,11.6,13.85]:
 width=4.65-.018*max(0,h)
 for side in range(4):
  for c in range(6):
   a=-width/2+(c+.5)*width/6
   pos=(a,(-1 if side==0 else 1)*width/2,h) if side%2==0 else ((-1 if side==1 else 1)*width/2,a,h)
   size=(width/6-.018,.5,.28) if side%2==0 else (.5,width/6-.018,.28)
   brick('Cornice',pos,size,.045)
for x in [-2.18,2.18]:
 for y in [-2.18,2.18]:
  for row in range(27):brick('Corner pier',(x,y,row*.51+.25),(.46,.46,.48),.055)
  brick('Crown post',(x,y,14.25),(.68,.68,1.25),.065)
  brick('Crown cap',(x,y,14.93),(.84,.84,.22),.05)
# Front arch: real wedge-shaped voussoirs and deep vertical jambs.
for side in [-1,1]:
 for row in range(5):brick('Arch jamb',(.7+side*.70,-2.35,4.8+row*.35),(.26,.36,.32),.025)
for i in range(11):
 angle=(i+.5)*math.pi/11
 o=brick('Arch voussoir',(.7+.7*math.cos(angle),-2.35,6.5+.7*math.sin(angle)),(.29,.38,.35),.028)
 o.rotation_euler[1]=angle-math.pi/2
# Sparse trailing ivy: actual leaf silhouettes, not a green tint over the wall.
for vine in range(5):
 x=random.uniform(-2,2);top=random.uniform(7,13)
 for j in range(random.randint(12,23)):
  z=top-j*.17;x+=random.uniform(-.11,.11)
  bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=(x,-2.37,z))
  o=bpy.context.object;o.name='Ivy leaf';o.scale=(random.uniform(.04,.085),.018,random.uniform(.08,.13));o.rotation_euler=(0,random.uniform(-.6,.6),random.uniform(-.5,.5));o.data.materials.append(ivy)
# Join by material to keep the runtime hierarchy and draw calls bounded.
bpy.ops.object.select_all(action='DESELECT')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for m in materials+[mortar,ivy]:
 group=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==m]
 for o in group:o.select_set(True)
 if group:
  bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();bpy.context.object.name='Tower_'+m.name
 bpy.ops.object.select_all(action='DESELECT')
# Export tower geometry, then a reusable stone ledge with corbel.
for o in bpy.context.scene.objects:o.select_set(o.type=='MESH')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/tower-kit.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'Tower.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
bpy.ops.object.select_all(action='DESELECT')
ledge=[]
for i in range(3):ledge.append(brick('Ledge stone',((i-1)*.46,0,-.14),(.455,.86,.28),.055))
ledge.append(brick('Ledge corbel',(0,.2,-.39),(.64,.48,.32),.065))
bpy.ops.object.select_all(action='DESELECT')
for o in ledge:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ledge.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/tower-kit.blend'))
print('TOWER_ASSETS_READY',OUT)
