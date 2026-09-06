"""Export a seamless 7.8 m body segment and a lower-detail mesh from the approved tower."""
import bpy,bmesh
from mathutils import Matrix,Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/tower-kit.blend'))
for obj in list(bpy.context.scene.objects):
 if not obj.name.startswith('Tower_'): bpy.data.objects.remove(obj,do_unlink=True)
for obj in list(bpy.context.scene.objects):
 if obj.type!='MESH':continue
 bm=bmesh.new();bm.from_mesh(obj.data);bm.transform(obj.matrix_world);obj.matrix_world=Matrix.Identity(4)
 for height,inner,outer in [(0,True,False),(7.8,False,True)]:
  result=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=Vector((0,0,height)),plane_no=Vector((0,0,1)),clear_inner=inner,clear_outer=outer)
  cut=[e for e in result['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_boundary]
  if cut:bmesh.ops.holes_fill(bm,edges=cut,sides=0)
 bm.normal_update();bm.to_mesh(obj.data);bm.free();obj.data.update()
bpy.ops.object.select_all(action='SELECT')
out=ROOT/'game/Assets/Art/Environment'
bpy.ops.export_scene.fbx(filepath=str(out/'ContinuousBody.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/continuous-body.blend'))
for obj in bpy.context.scene.objects:
 if obj.type!='MESH':continue
 bpy.context.view_layer.objects.active=obj
 mod=obj.modifiers.new('Distance detail','DECIMATE');mod.ratio=.35
 bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.export_scene.fbx(filepath=str(out/'ContinuousBodyLOD.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
print('CONTINUOUS_BODY_READY height=7.8m, source and LOD exported')
