"""Build Maya's articulated, textured game mesh from editable Blender geometry.

The body topology is derived from the CC0 MakeHuman base; the face is fitted
from the supplied persona texture using the Apache-2.0 MediaPipe topology.
No model inference or photo processing runs in Unity.
"""
from pathlib import Path
from collections import defaultdict
import json, math, random, os, shutil
import bpy
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'art/sources/maya'
HIGH_DETAIL = os.environ.get('MAYA_VARIANT', 'detailed') == 'detailed'
STEM = 'maya' if HIGH_DETAIL else 'maya-lod'
OUT = ROOT / ('game/Assets/Art/Maya' if HIGH_DETAIL else 'game/Assets/Art/Maya/LOD')
OUT.mkdir(parents=True, exist_ok=True)
(OUT/'Textures').mkdir(exist_ok=True)
if not HIGH_DETAIL:
    for name in ['MayaFace.png','MayaHair.png']:
        shutil.copy2(ROOT/'game/Assets/Art/Maya/Textures'/name,OUT/'Textures'/name)

random.seed(1507)
bpy.ops.wm.read_factory_settings(use_empty=True)
COL = bpy.context.scene.collection

def mesh_object(name, vertices, faces, material, uv=None):
    mesh = bpy.data.meshes.new(name + 'Mesh')
    mesh.from_pydata(vertices, [], faces); mesh.update()
    obj = bpy.data.objects.new(name, mesh); COL.objects.link(obj)
    if material: mesh.materials.append(material)
    for poly in mesh.polygons: poly.use_smooth = True
    if uv is not None:
        layer = mesh.uv_layers.new(name='UVMap')
        for poly in mesh.polygons:
            for li in poly.loop_indices: layer.data[li].uv = uv[mesh.loops[li].vertex_index]
    return obj

def activate(obj):
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

def apply(obj, modifier):
    activate(obj); bpy.ops.object.modifier_apply(modifier=modifier.name)

def material(name, color, roughness=.55, metallic=0, texture=None):
    m=bpy.data.materials.new(name); m.use_nodes=True
    bsdf=m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Roughness'].default_value=roughness
    bsdf.inputs['Metallic'].default_value=metallic
    if texture:
        image=bpy.data.images.load(str(texture),check_existing=True)
        node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
        m.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Base Color'])
    m.diffuse_color=(*color,1)
    return m

def fabric_maps():
    """Bake deterministic cotton weave maps as portable Blender image assets."""
    import numpy as np
    size=512
    y,x=np.mgrid[0:size,0:size]
    weave=.5+.035*np.sin(x*math.pi/3)+.022*np.cos(y*math.pi/3)*np.cos(x*math.pi/3)
    rng=np.random.default_rng(1507)
    height=weave+rng.normal(0,.018,(size,size))
    diffuse=np.clip(.42+.15*height,.1,.8)
    gx,gy=np.gradient(height)
    normal=np.stack((-gy*.35,-gx*.35,np.ones_like(height)),axis=-1)
    normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
    for name,rgb,noncolor in [('MayaFabric',np.repeat(diffuse[:,:,None],3,axis=2),False),('MayaFabricNormal',normal*.5+.5,True)]:
        image=bpy.data.images.new(name,width=size,height=size,alpha=True)
        image.colorspace_settings.name='Non-Color' if noncolor else 'sRGB'
        rgba=np.concatenate((rgb,np.ones((size,size,1))),axis=-1).astype(np.float32)
        image.pixels.foreach_set(rgba.ravel());image.filepath_raw=str(OUT/'Textures'/f'{name}.png');image.file_format='PNG';image.save()
fabric_maps()

SKIN=material('MayaSkin',(.40,.17,.075),.6)
FACE=material('MayaFace',(1,1,1),.58,texture=OUT/'Textures/MayaFace.png')
TANK=material('MayaTank',(.014,.018,.026),.85)
PANTS=material('MayaCargo',(.045,.055,.067),.82)
BOOT=material('MayaLeather',(.058,.036,.026),.63)
SOLE=material('MayaRubber',(.012,.013,.016),.9)
THREAD=material('MayaStitch',(.16,.145,.125),.78)
GOLD=material('MayaGold',(.65,.39,.13),.24,.78)
METAL=material('MayaBuckle',(.23,.25,.27),.35,.8)
HAIR=material('MayaHair',(.02,.008,.004),.6,texture=OUT/'Textures/MayaHair.png')
# A native material bake adds an irregular alpha edge to the scalp surface.
# This avoids an opaque straight rim while retaining the source strand colour.
import numpy as np
hair_image=bpy.data.images.load(str(OUT/'Textures/MayaHair.png'),check_existing=True)
w,h=hair_image.size
pixels=np.empty(w*h*4,dtype=np.float32);hair_image.pixels.foreach_get(pixels)
pixels=pixels.reshape((h,w,4))
x=np.arange(w,dtype=np.float32)/w;v=np.arange(h,dtype=np.float32)[:,None]/h
edge=.985+.007*np.sin(x*math.pi*2*601)+.004*np.sin(x*math.pi*2*127)
pixels[:,:,3]=np.clip((edge[None,:]-v)/.005,0,1)
line_image=bpy.data.images.new('MayaHairlineTexture',width=w,height=h,alpha=True)
line_image.colorspace_settings.name='sRGB';line_image.pixels.foreach_set(pixels.ravel())
line_image.filepath_raw=str(OUT/'Textures/MayaHairline.png');line_image.file_format='PNG';line_image.save()
HAIRLINE=material('MayaHairline',(1,1,1),.82,texture=OUT/'Textures/MayaHairline.png')
line_nodes=HAIRLINE.node_tree.nodes;line_bsdf=line_nodes.get('Principled BSDF')
line_tex=next(n for n in line_nodes if n.type=='TEX_IMAGE')
HAIRLINE.node_tree.links.new(line_tex.outputs['Alpha'],line_bsdf.inputs['Alpha'])
line_bsdf.inputs['Specular IOR Level'].default_value=.18
HAIRLINE.surface_render_method='DITHERED'

HAIR_LIGHT=HAIR
HAIR.node_tree.nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value=.18
HAIR.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.82
# Portable weave maps are shared by the two garments, with distinct tints.
for m,tint in [(TANK,(.055,.07,.09,1)),(PANTS,(.20,.24,.30,1))]:
    nodes=m.node_tree.nodes;links=m.node_tree.links;bsdf=nodes.get('Principled BSDF')
    tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.get('MayaFabric')
    mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=tint
    links.new(tex.outputs['Color'],mix.inputs[1]);links.new(mix.outputs['Color'],bsdf.inputs['Base Color'])
    normal_tex=nodes.new('ShaderNodeTexImage');normal_tex.image=bpy.data.images.get('MayaFabricNormal')
    normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.14
    links.new(normal_tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],bsdf.inputs['Normal'])

# Parse the base ourselves so helper geometry never becomes a visible mesh.
vertices=[];faces=[];groups=defaultdict(list);group='';uvs=[];face_uv=[]
for line in (SOURCE/'base.obj').read_text().splitlines():
    bits=line.split()
    if not bits:continue
    if bits[0]=='v':vertices.append(Vector(tuple(map(float,bits[1:4]))))
    elif bits[0]=='vt':uvs.append(tuple(map(float,bits[1:3])))
    elif bits[0]=='g':group=' '.join(bits[1:])
    elif bits[0]=='f':
        f=[int(b.split('/')[0])-1 for b in bits[1:]];groups[group].append(f)
        if group=='body':faces.append(f);face_uv.append([int(b.split('/')[1])-1 for b in bits[1:]])
for line in (SOURCE/'female-young.target').read_text().splitlines():
    if line and not line.startswith('#'):
        i,x,y,z=line.split();vertices[int(i)]+=Vector((float(x),float(y),float(z)))
body_indices={i for f in faces for i in f}
floor=min(vertices[i].y for i in body_indices)
scale=1.72/(max(vertices[i].y for i in body_indices)-floor)
vertices=[Vector((v.x*scale,-v.z*scale,(v.y-floor)*scale)) for v in vertices]

def joint(name):
    ids={i for f in groups['joint-'+name] for i in f}
    return sum((vertices[i] for i in ids),Vector())/len(ids)

# Lower body is entirely clothed. The head above the neck is replaced by the
# fitted face, skull and long hair rather than hiding the old head underneath.
body_faces=[];body_uv=[];mat_indices=[]
for face,coords in zip(faces,face_uv):
    center=sum((vertices[i] for i in face),Vector())/len(face)
    if max(vertices[i].z for i in face)<.17:continue
    z=center.z;x=abs(center.x)
    mat=0
    if z<.96 and x<.27:mat=2
    elif x < .19 and z < (1.39 if center.y>.005 else 1.315+2.6*x*x):mat=1
    body_faces.append(face);body_uv.append(coords);mat_indices.append(mat)
used=sorted({i for f in body_faces for i in f});index={old:i for i,old in enumerate(used)}
body_vertices=[vertices[i].copy() for i in used]
# Smooth the shared skin/garment cut before splitting meshes. Both sides use
# the same boundary positions, avoiding zigzag collars and waist gaps.
edge_materials=defaultdict(set)
for f,m in zip(body_faces,mat_indices):
    for a,b in zip(f,f[1:]+f[:1]):edge_materials[tuple(sorted((a,b)))].add(m)
neighbors=defaultdict(set)
for (a,b),mats in edge_materials.items():
    if len(mats)>1:neighbors[a].add(b);neighbors[b].add(a)
for _ in range(8):
    old=[v.copy() for v in body_vertices]
    for original,adjacent in neighbors.items():
        if len(adjacent)==2:
            average=sum((old[index[n]] for n in adjacent),Vector())/2
            body_vertices[index[original]]=old[index[original]].lerp(average,.6)
for original,v in zip(used,body_vertices):
    if original in neighbors and .91<v.z<1.02:v.z=.965
    if v.z<.22 and abs(v.x)<.27:v.z=.22

# Subtle cloth volume and folds remove the anatomical-tight look of a skin mesh.
for vi,v in zip(used,body_vertices):
    if .16<v.z<.96:
        side=1 if v.x>0 else -1
        center_x=.12*side
        v.x=center_x+(v.x-center_x)*1.07
        v.y*=1.07
        fold=math.sin(v.z*160+abs(v.x)*38)*.0035*math.exp(-((v.z-.48)/.1)**2)
        v.y += fold
    elif v.z<.16:
        footx=.185*(1 if v.x>0 else -1)
        v.x=footx+(v.x-footx)*1.15
        v.y=(v.y+.03)*1.08-.03
body=mesh_object('Maya_OutfitAndSkin',body_vertices,[[index[i] for i in f] for f in body_faces],None)
for m in [SKIN,TANK,PANTS,BOOT]:body.data.materials.append(m)
layer=body.data.uv_layers.new(name='UVMap')
for poly,coords,mat in zip(body.data.polygons,body_uv,mat_indices):
    poly.material_index=mat
    for li,uvindex in zip(poly.loop_indices,coords):layer.data[li].uv=uvs[uvindex]
# Split the garment borders before simplification so the neckline cannot turn
# into large triangles of alternating skin/fabric material.
activate(body)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.separate(type='MATERIAL');bpy.ops.object.mode_set(mode='OBJECT')
parts=[o for o in bpy.context.scene.objects if o.type=='MESH']
for part in parts:
    part.data.calc_loop_triangles()
    count=len(part.data.loop_triangles)
    name=part.data.materials[0].name
    part.name='Maya_'+name.removeprefix('Maya')
    target=({'MayaSkin':5400,'MayaTank':850,'MayaCargo':1150} if HIGH_DETAIL else {'MayaSkin':1750,'MayaTank':300,'MayaCargo':470}).get(name,100)
    mod=part.modifiers.new('Game topology','DECIMATE');mod.ratio=min(1,target/max(1,count))
    apply(part,mod)

# Project the persona texture onto the continuous anatomical head. Source
# landmark rows align eyes/nose/mouth without replacing the head with a mask.
data=json.loads((SOURCE/'face-landmarks.json').read_text());landmarks=data['landmarks']
eye_l=joint('l-eye');eye_r=joint('r-eye')
eye_u=(landmarks[468][0]+landmarks[473][0])/2
u_scale=abs(landmarks[468][0]-landmarks[473][0])/abs(eye_l.x-eye_r.x)
eye_z=(eye_l.z+eye_r.z)/2
# The oral rig marker lies inside the mouth, not on the outer lip seam.
mouth_z=1.540
nose=min((vertices[i] for i in body_indices if abs(vertices[i].x)<.012 and 1.56<vertices[i].z<1.61),key=lambda v:v.y)
rows=sorted([(1.47,.91),(1.502,landmarks[152][1]),(mouth_z,landmarks[13][1]),(nose.z,landmarks[1][1]),(eye_z,(landmarks[468][1]+landmarks[473][1])/2),(1.657,landmarks[10][1]),(1.70,.12),(1.74,.06)])
def photo_uv(v):
    row=rows[0][1]
    for (za,ya),(zb,yb) in zip(rows,rows[1:]):
        if za<=v.z<=zb:
            row=ya+(yb-ya)*(v.z-za)/(zb-za);break
        if v.z>zb:row=yb
    return (eye_u+v.x*u_scale,1-row)

oval_indices=[10,338,297,332,284,251,389,356,454,323,361,288,397,365,379,378,400,377,152,148,176,149,150,136,172,58,132,93,234,127,162,21,54,103,67,109]
contour=[landmarks[i][:2] for i in oval_indices]
def facial_edge(u,y):
    crossings=[]
    for a,b in zip(contour,contour[1:]+contour[:1]):
        if min(a[1],b[1])<=y<max(a[1],b[1]):
            crossings.append(a[0]+(b[0]-a[0])*(y-a[1])/(b[1]-a[1]))
    if len(crossings)<2:return 0
    return max(0,min(1,min(u-min(crossings),max(crossings)-u)/.025))

skin=next(o for o in parts if o.name=='Maya_Skin')
projection=skin.data.uv_layers.new(name='PhotoProjection')
skin.data.color_attributes.new(name='FaceBlend',type='FLOAT_COLOR',domain='CORNER')
# Adding CustomData layers can invalidate previous RNA references. Resolve
# both layers by name after allocation so projection never overwrites UVMap.
projection=skin.data.uv_layers['PhotoProjection']
blend=skin.data.color_attributes['FaceBlend']
for poly in skin.data.polygons:
    for li in poly.loop_indices:
        v=skin.data.vertices[skin.data.loops[li].vertex_index].co
        projection.data[li].uv=photo_uv(v)
        front=max(0,min(1,(-v.y-.015)/.075))
        cheek=max(0,min(1,(.071-abs(v.x))/.018))
        neck=max(0,min(1,(v.z-1.49)/.055))
        top=max(0,min(1,(1.725-v.z)/.025))
        u,w=photo_uv(v)
        photo_edge=facial_edge(u,1-w)
        strength=front*cheek*neck*top*photo_edge
        blend.data[li].color=(strength,strength,strength,1)
# Bake the blend to the original, continuous MakeHuman UV map. The game uses
# an ordinary URP texture and needs no projection or landmark code at runtime.
nodes=SKIN.node_tree.nodes;links=SKIN.node_tree.links;nodes.clear()
output=nodes.new('ShaderNodeOutputMaterial');emission=nodes.new('ShaderNodeEmission')
tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(OUT/'Textures/MayaFace.png'),check_existing=True)
uv=nodes.new('ShaderNodeUVMap');uv.uv_map='PhotoProjection';links.new(uv.outputs['UV'],tex.inputs['Vector'])
attribute=nodes.new('ShaderNodeVertexColor');attribute.layer_name='FaceBlend'
mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[1].default_value=(.40,.17,.075,1)
links.new(attribute.outputs['Color'],mix.inputs[0]);links.new(tex.outputs['Color'],mix.inputs[2])
links.new(mix.outputs['Color'],emission.inputs['Color']);links.new(emission.outputs['Emission'],output.inputs['Surface'])
atlas=bpy.data.images.new('MayaSkinAtlas',width=2048,height=2048,alpha=False)
atlas.generated_color=(.40,.17,.075,1)
target=nodes.new('ShaderNodeTexImage');target.image=atlas;nodes.active=target
skin.data.uv_layers.active=skin.data.uv_layers.get('UVMap')
bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=1
activate(skin);bpy.context.scene.render.bake.use_clear=False;bpy.context.scene.render.bake.margin=12
bpy.ops.object.bake(type='EMIT')
atlas.filepath_raw=str(OUT/'Textures/MayaSkinAtlas.png');atlas.file_format='PNG';atlas.save()
nodes.clear();output=nodes.new('ShaderNodeOutputMaterial');bsdf=nodes.new('ShaderNodeBsdfPrincipled');bsdf.inputs['Roughness'].default_value=.58
tex=nodes.new('ShaderNodeTexImage');tex.image=atlas
links.new(tex.outputs['Color'],bsdf.inputs['Base Color']);links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
# Eye surfaces fit the existing sockets and share the same photographic
# projection, retaining the persona's iris colour without a flat face card.
for side,center in [('L',eye_l),('R',eye_r)]:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16 if HIGH_DETAIL else 10,ring_count=8 if HIGH_DETAIL else 5,radius=1,location=center+Vector((0,-.006,0)))
    eye=bpy.context.object;eye.name='Maya_Eye_'+side
    eye.scale=(.013,.0115,.0125);activate(eye);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    eye.data.materials.append(FACE)
    for poly in eye.data.polygons:
        poly.use_smooth=True
        for li in poly.loop_indices:eye.data.uv_layers.active.data[li].uv=photo_uv(eye.data.vertices[eye.data.loops[li].vertex_index].co)

def loft(name,sections,segments,mat):
    vs=[];fs=[];uv=[]
    for j,(x,y,z,rx,ry) in enumerate(sections):
        for i in range(segments):
            a=2*math.pi*i/segments
            vs.append((x+rx*math.cos(a),y+ry*math.sin(a),z));uv.append((i/segments,j/(len(sections)-1)))
    for j in range(len(sections)-1):
        for i in range(segments):
            a=j*segments+i;b=j*segments+(i+1)%segments
            fs.append((a,b,b+segments,a+segments))
    fs.append(tuple(reversed(range(segments))));fs.append(tuple((len(sections)-1)*segments+i for i in range(segments)))
    return mesh_object(name,vs,fs,mat,uv)



# Long side-swept hair is a closed cap plus layered swept ribbons. Strands
# follow the head/shoulder silhouette with varying widths and uneven tips.
hair_parts=[]
cap_vertices=[];cap_uv=[];cap_faces=[]
segments=32 if HIGH_DETAIL else 20;rings=7 if HIGH_DETAIL else 5
for j in range(rings+1):
    t=max(.025,j/rings)
    for i in range(segments):
        theta=2*math.pi*i/segments
        phi=t*(1.72+.35*math.sin(theta)-.18*math.cos(theta))
        x=.099*math.cos(theta)*math.sin(phi)
        y=-.035+.122*math.sin(theta)*math.sin(phi)
        z=1.632+.116*math.cos(phi)
        cap_vertices.append((x,y,z));cap_uv.append((i/segments,t))
for j in range(rings):
    for i in range(segments):
        a=j*segments+i;b=j*segments+(i+1)%segments
        cap_faces.append((a,a+segments,b+segments,b))
cap=mesh_object('Maya_HairCrown',cap_vertices,cap_faces,HAIRLINE,cap_uv);hair_parts.append(cap)

def hair_lock(name,points,widths,mat):
    vs=[];uv=[];fs=[];sides=8 if HIGH_DETAIL else 5
    if HIGH_DETAIL:
        original=[Vector(p) for p in points];expanded=[];expanded_widths=[]
        for i in range(len(original)-1):
            p0=original[max(0,i-1)];p1=original[i];p2=original[i+1];p3=original[min(len(original)-1,i+2)]
            for j in range(2):
                t=j/2
                expanded.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
                expanded_widths.append(widths[i]*(1-t)+widths[i+1]*t)
        expanded.append(original[-1]);expanded_widths.append(widths[-1]);points=expanded;widths=expanded_widths
    for j,p in enumerate(points):
        p=Vector(p)
        tangent=(Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])).normalized()
        cross=Vector((1,0,0));depth=tangent.cross(cross).normalized()
        for k in range(sides):
            angle=k*math.pi*2/sides
            vs.append(p+cross*(math.cos(angle)*widths[j])+depth*(math.sin(angle)*widths[j]*.42))
            uv.append((k/sides,j/(len(points)-1)))
    for j in range(len(points)-1):
        for k in range(sides):
            a=j*sides+k;b=j*sides+(k+1)%sides
            fs.append((a,b,b+sides,a+sides))
    fs.append(tuple(reversed(range(sides))));fs.append(tuple((len(points)-1)*sides+i for i in range(sides)))
    return mesh_object(name,vs,fs,mat,uv)

for side in [-1,1]:
    for k in range(5 if HIGH_DETAIL else 3):
        pts=[];widths=[]
        end=1.14 if side<0 else 1.24
        for j in range(7):
            t=j/6
            x=side*(.063+.07*min(1,t*3.5)) + side*(k-(2 if HIGH_DETAIL else 1))*(.013 if HIGH_DETAIL else .018)
            x+=side*.012*math.sin(t*math.pi*3+k)*t
            y=-.025-.175*min(1,t*1.6)-k*.008
            z=1.724*(1-t)+end*t
            pts.append((x,y,z));widths.append((.019 if HIGH_DETAIL else .026)*(1-.3*t))
        pts[0]=(side*(.036+k*.004),-.047,1.731)
        widths[0]=.012;widths[-1]=.0008
        hair_parts.append(hair_lock(f'Maya_HairLock_{side}_{k}',pts,widths,HAIR))
for k in ([-2,-1,0,1,2] if HIGH_DETAIL else [-1,0,1]):
    pts=[(k*(.038 if HIGH_DETAIL else .048)+.01*math.sin(j*.9+k),.115+.015*math.sin(j*.7),1.71-j*.075) for j in range(7)]
    pts[0]=(k*.022,.010,1.721)
    hair_parts.append(hair_lock('Maya_HairBack_'+str(k),pts,[.018]+[.025 if HIGH_DETAIL else .033]*5+[.003],HAIR))

# Garment seams, laces, pockets, belt hardware and her small gold necklace.
def tube(name,points,radius,mat,sides=4):
    vs=[];faces=[]
    for j,p in enumerate(points):
        p=Vector(p)
        tangent=(Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])).normalized()
        u=tangent.cross(Vector((0,1,0))).normalized()
        if u.length<.1:u=Vector((1,0,0))
        v=tangent.cross(u).normalized()
        for k in range(sides):
            a=k*2*math.pi/sides;vs.append(p+radius*(u*math.cos(a)+v*math.sin(a)))
    for j in range(len(points)-1):
        for k in range(sides):faces.append((j*sides+k,j*sides+(k+1)%sides,(j+1)*sides+(k+1)%sides,(j+1)*sides+k))
    return mesh_object(name,vs,faces,mat)

accessories=[]
necklace=[(.086*math.sin(a),-.075-.07*math.cos(a),1.383-.04*math.cos(a)) for a in [i*2*math.pi/12 for i in range(13)]]
accessories.append(tube('Maya_Necklace',necklace,.0014,GOLD,3))
pendant=loft('Maya_Pendant',[(0,-.147,1.334,.004,.0018),(0,-.149,1.346,.007,.002),(0,-.147,1.355,.003,.0018)],6,GOLD);accessories.append(pendant)
for side in [-1,1]:
    # A short knee seam and two pocket flaps catch light without extra glow.
    x=.185*side
    pocket=mesh_object('Maya_CargoPocket_'+str(side),[(x,-.027,.85),(x+side*.012,.042,.85),(x+side*.006,.042,.73),(x,-.027,.73)],[(0,1,2,3)],PANTS)
    mod=pocket.modifiers.new('Pocket depth','SOLIDIFY');mod.thickness=.004;apply(pocket,mod);accessories.append(pocket)
    accessories.append(tube('Maya_PocketStitch_'+str(side),[(x,-.029,.84),(x,-.03,.745)],.0016,THREAD,3))
    # A full boot upper covers the toes; source bare-foot polygons are omitted.
    footx=.185*side
    upper=loft('Maya_BootUpper_'+str(side),[(footx,-.095,.025,.053,.11),(footx,-.097,.067,.052,.108),(footx,-.045,.112,.043,.069),(footx,-.025,.17,.041,.041),(footx,-.025,.22,.04,.04)],10,BOOT);accessories.append(upper)
    sole=loft('Maya_BootSole_'+str(side),[(footx,-.095,.01,.051,.111),(footx,-.095,.028,.052,.113)],10,SOLE);accessories.append(sole)
    for row in range(3):
        z=.068+row*.023;y=-.132+row*.024
        accessories.append(tube('Maya_Lace_'+str(side)+'_'+str(row),[(footx-.025,y,z),(footx+.025,y-.006,z+.004)],.0018,THREAD,3))
# A functional-looking fabric belt with a small machined clasp.
belt=loft('Maya_Belt',[(0,-.002,.954,.147,.105),(0,-.002,.977,.147,.105)],16,SOLE);accessories.append(belt)
clasp=mesh_object('Maya_BeltClasp',[(-.012,-.089,.964),(.012,-.089,.964),(.012,-.089,.983),(-.012,-.089,.983)],[(0,1,2,3)],METAL);accessories.append(clasp)

# One generic deform rig; no Humanoid/retarget dependency or runtime IK solver.
arm_data=bpy.data.armatures.new('MayaSkeleton');rig=bpy.data.objects.new('MayaRig',arm_data);COL.objects.link(rig)
activate(rig);bpy.ops.object.mode_set(mode='EDIT')
bones={}
def bone(name,head,tail,parent=None):
    b=arm_data.edit_bones.new(name);b.head=head;b.tail=tail
    if (b.tail-b.head).length<.01:b.tail=b.head+Vector((0,0,.02))
    if parent:b.parent=arm_data.edit_bones[parent]
    bones[name]=(Vector(head),Vector(tail));return b
bone('Root',(0,0,0),(0,0,.2))
bone('Hips',joint('pelvis'),joint('spine-3'),'Root')
bone('Spine',joint('spine-3'),joint('spine-2'),'Hips')
bone('Chest',joint('spine-2'),joint('neck'),'Spine')
bone('Neck',joint('neck'),(0,-.025,1.53),'Chest')
bone('Head',(0,-.025,1.53),(0,-.025,1.73),'Neck')
for s,label in [('l','L'),('r','R')]:
    shoulder=joint(s+'-shoulder');elbow=joint(s+'-elbow');wrist=joint(s+'-hand')
    hip=joint(s+'-upper-leg');knee=joint(s+'-knee');ankle=joint(s+'-ankle');foot=joint(s+'-foot-1')
    bone('UpperArm.'+label,shoulder,elbow,'Chest')
    bone('Forearm.'+label,elbow,wrist,'UpperArm.'+label)
    bone('Hand.'+label,wrist,wrist+(wrist-elbow).normalized()*.105,'Forearm.'+label)
    bone('Thigh.'+label,hip,knee,'Hips')
    bone('Shin.'+label,knee,ankle,'Thigh.'+label)
    bone('Foot.'+label,ankle,foot,'Shin.'+label)
bpy.ops.object.mode_set(mode='OBJECT')

def distance(p,a,b):
    d=b-a;t=max(0,min(1,(p-a).dot(d)/d.length_squared))
    return (p-(a+d*t)).length

def bind(obj,rigid=None):
    obj.parent=rig
    if obj.type!='MESH':return
    groups={name:obj.vertex_groups.new(name=name) for name in bones if name!='Root'}
    for v in obj.data.vertices:
        p=v.co
        if rigid:
            groups[rigid].add([v.index],1,'REPLACE');continue
        side='L' if p.x>0 else 'R'
        if p.z>1.535:
            groups['Head'].add([v.index],1,'REPLACE');continue
        if p.z>1.47:
            blend=max(0,min(1,(p.z-1.47)/.065))
            groups['Head'].add([v.index],blend,'REPLACE')
            groups['Neck'].add([v.index],1-blend,'REPLACE');continue
        if p.z>1.44:names=['Neck','Chest']
        elif abs(p.x)>.205 and p.z>.97:names=['UpperArm.'+side,'Forearm.'+side,'Hand.'+side]
        elif p.z<.91:names=['Hips','Thigh.'+side,'Shin.'+side,'Foot.'+side]
        else:names=['Hips','Spine','Chest','UpperArm.'+side]
        candidates=sorted([(distance(p,*bones[name]),name) for name in names])[:2]
        weights=[1/(d+.015)**5 for d,n in candidates];total=sum(weights)
        for (_,name),w in zip(candidates,weights):groups[name].add([v.index],w/total,'REPLACE')
    mod=obj.modifiers.new('Maya deformation','ARMATURE');mod.object=rig

for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH':continue
    if obj.name.startswith(('Maya_Eye','Maya_Hair')):bind(obj,'Head')
    elif obj.name.startswith(('Maya_Necklace','Maya_Pendant')):bind(obj,'Chest')
    elif obj.name.startswith(('Maya_Belt','Maya_CargoPocket','Maya_PocketStitch')):bind(obj,'Hips')
    elif obj.name.startswith(('Maya_BootSole','Maya_BootUpper','Maya_Lace')):bind(obj,'Foot.L' if '_1' in obj.name else 'Foot.R')
    else:bind(obj)

# Rig/source poses remain editable. Export includes skinning and a neutral
# rest pose; Unity applies the small climb/jump cycle to these named bones.
rig.location.z=-.01
bpy.context.view_layer.update()
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.samples=16
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
report={'source':'Continuous MakeHuman CC0 anatomy with Maya photographic skin bake and authored hair/outfit','heightMetres':0,'triangles':0,'objects':[],'bones':len(bones)}
lo=Vector((float('inf'),)*3);hi=Vector((-float('inf'),)*3)
for obj in meshes:
    obj.data.calc_loop_triangles();tris=len(obj.data.loop_triangles)
    report['triangles']+=tris;report['objects'].append({'name':obj.name,'triangles':tris})
    for v in obj.data.vertices:
        p=obj.matrix_world@v.co
        for a in range(3):lo[a]=min(lo[a],p[a]);hi[a]=max(hi[a],p[a])
report['heightMetres']=hi.z-lo.z;report['boundsMin']=list(lo);report['boundsMax']=list(hi)
(ROOT/'art'/f'{STEM}-report.json').write_text(json.dumps(report,indent=2)+'\n')
# Keep the authoring file even when a budget check needs another iteration.
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art'/f'{STEM}.blend'),check_existing=False)
if report['triangles'] >= (12000 if HIGH_DETAIL else 4000):
    raise RuntimeError(f"Maya exceeds runtime triangle budget: {report['triangles']}")
# The editable source retains separate garments/locks. Export batches shared
# materials, keeping the same weights/geometry while reducing runtime draws.
for mat in list(bpy.data.materials):
    group=[o for o in bpy.context.scene.objects if o.type=='MESH' and len(o.data.materials)==1 and o.data.materials[0]==mat]
    if len(group)>1:
        activate(group[0])
        for obj in group:obj.select_set(True)
        bpy.ops.object.join();bpy.context.object.name=mat.name+'Surface'
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
activate(rig)
for obj in meshes:obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/('Maya.fbx' if HIGH_DETAIL else 'MayaLOD.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
    bake_space_transform=False,add_leaf_bones=False,use_armature_deform_only=False,bake_anim=False,path_mode='AUTO')
print('MAYA_ASSET_REPORT',json.dumps({k:v for k,v in report.items() if k!='objects'}))
print('MAYA_EXPORTED_RENDERERS',len(meshes))
