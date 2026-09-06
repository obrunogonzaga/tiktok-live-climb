"""Build the compact Climb Bot source asset and Unity-ready FBX.

Run with Blender 4.5+:
  /Users/bruno/Applications/Blender.app/Contents/MacOS/Blender \
    --background --python art/build_bot.py

The model faces -Y, uses Z up, and has a root at the sole plane.  Its mesh
hierarchy deliberately exposes limb pivots so Unity can use simple transform
animation without an armature.
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT_ROOT = Path(__file__).resolve().parents[1]
BLEND_PATH = PROJECT_ROOT / "art" / "bot.blend"
FBX_PATH = PROJECT_ROOT / "game" / "Assets" / "Art" / "Bot" / "Bot.fbx"


def hex_color(value: str) -> tuple[float, float, float, float]:
    """Return a linear RGBA color from a six-character sRGB hex value."""
    value = value.lstrip("#")
    channels = [int(value[index : index + 2], 16) / 255 for index in range(0, 6, 2)]
    return (*channels, 1.0)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def add_to_asset_collection(obj: bpy.types.Object) -> None:
    for collection in tuple(obj.users_collection):
        collection.objects.unlink(obj)
    ASSET_COLLECTION.objects.link(obj)


def smooth_mesh(obj: bpy.types.Object) -> None:
    if obj.type != "MESH":
        return
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def apply_modifier(obj: bpy.types.Object, modifier: bpy.types.Modifier) -> None:
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def apply_transform(obj: bpy.types.Object) -> None:
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def set_origin(obj: bpy.types.Object, origin: Vector) -> None:
    """Move an object's origin without moving its visible geometry."""
    activate(obj)
    bpy.context.scene.cursor.location = origin
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")


def parent_keep_world(child: bpy.types.Object, parent: bpy.types.Object) -> None:
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


def add_material(
    name: str,
    color: str,
    *,
    metallic: float,
    roughness: float,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = hex_color(color)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness

    if emission_strength:
        emission_input = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
        if emission_input:
            emission_input.default_value = hex_color(color)
        strength_input = bsdf.inputs.get("Emission Strength")
        if strength_input:
            strength_input.default_value = emission_strength

    return material


def assign_material(obj: bpy.types.Object, material: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)


def rounded_box(
    name: str,
    location: Vector,
    dimensions: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    bevel: float = 0.025,
    bevel_segments: int = 2,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.dimensions = dimensions
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    modifier = obj.modifiers.new("Soft shell edges", "BEVEL")
    modifier.width = bevel
    modifier.segments = bevel_segments
    apply_modifier(obj, modifier)
    smooth_mesh(obj)
    assign_material(obj, material)
    add_to_asset_collection(obj)
    return obj


def sphere(
    name: str,
    location: Vector,
    scale: tuple[float, float, float],
    material: bpy.types.Material,
    *,
    segments: int = 10,
    rings: int = 5,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    apply_transform(obj)
    smooth_mesh(obj)
    assign_material(obj, material)
    add_to_asset_collection(obj)
    return obj


def beveled_cylinder(
    name: str,
    location: Vector,
    radius: float,
    depth: float,
    material: bpy.types.Material,
    *,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 8,
    bevel: float = 0.01,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        end_fill_type="NGON",
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.active_object
    obj.name = name
    apply_transform(obj)
    if bevel:
        modifier = obj.modifiers.new("Rounded rims", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        apply_modifier(obj, modifier)
    smooth_mesh(obj)
    assign_material(obj, material)
    add_to_asset_collection(obj)
    return obj


def cylinder_between(
    name: str,
    start: Vector,
    end: Vector,
    radius: float,
    material: bpy.types.Material,
    *,
    bevel: float = 0.015,
) -> bpy.types.Object:
    direction = end - start
    center = (start + end) / 2
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8,
        radius=radius,
        depth=direction.length,
        end_fill_type="NGON",
        location=center,
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(direction.normalized())
    apply_transform(obj)
    modifier = obj.modifiers.new("Segment edge radius", "BEVEL")
    modifier.width = bevel
    modifier.segments = 1
    apply_modifier(obj, modifier)
    smooth_mesh(obj)
    assign_material(obj, material)
    add_to_asset_collection(obj)
    set_origin(obj, start)
    return obj


def curved_visor(
    name: str,
    center: Vector,
    material: bpy.types.Material,
) -> bpy.types.Object:
    """Create a smooth curved faceplate that wraps visibly around the helmet."""
    columns = 10
    rows = 3
    extent = math.radians(126)
    radius_x = 0.361
    radius_y = 0.346
    half_height = 0.094
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []

    for row in range(rows):
        vertical = -1.0 + 2.0 * row / (rows - 1)
        for column in range(columns + 1):
            angle = -extent + 2.0 * extent * column / columns
            edge_falloff = 1.0 - 0.18 * (abs(angle) / extent) ** 1.7
            vertices.append(
                (
                    center.x + radius_x * math.sin(angle),
                    center.y - radius_y * math.cos(angle) - 0.012,
                    center.z + vertical * half_height * edge_falloff,
                )
            )

    for row in range(rows - 1):
        for column in range(columns):
            index = row * (columns + 1) + column
            faces.append((index, index + 1, index + columns + 2, index + columns + 1))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    ASSET_COLLECTION.objects.link(obj)
    assign_material(obj, material)

    solidify = obj.modifiers.new("Faceplate thickness", "SOLIDIFY")
    solidify.thickness = 0.012
    solidify.offset = 0.0
    apply_modifier(obj, solidify)
    bevel = obj.modifiers.new("Faceplate soft edge", "BEVEL")
    bevel.width = 0.012
    bevel.segments = 1
    apply_modifier(obj, bevel)
    smooth_mesh(obj)
    return obj


def add_ring(
    name: str,
    center: Vector,
    radius: float,
    thickness: float,
    material: bpy.types.Material,
    *,
    axis: str = "Z",
) -> bpy.types.Object:
    rotation = {
        "X": (0.0, math.pi / 2, 0.0),
        "Y": (math.pi / 2, 0.0, 0.0),
        "Z": (0.0, 0.0, 0.0),
    }[axis]
    return beveled_cylinder(
        name,
        center,
        radius,
        thickness,
        material,
        rotation=rotation,
        bevel=0.0,
    )


def add_arm(
    side: str,
    shoulder: Vector,
    elbow: Vector,
    wrist: Vector,
    hand_center: Vector,
    torso: bpy.types.Object,
) -> None:
    shoulder_joint = sphere(
        f"Arm_{side}_ShoulderJoint", shoulder, (0.085, 0.085, 0.085), BOT_JOINT
    )
    parent_keep_world(shoulder_joint, torso)

    upper = cylinder_between(
        f"Arm_{side}_UpperShell", shoulder, elbow, 0.072, BOT_SHELL, bevel=0.018
    )
    parent_keep_world(upper, shoulder_joint)
    upper_band_point = shoulder.lerp(elbow, 0.22)
    upper_band = cylinder_between(
        f"Arm_{side}_UpperOrangeBand",
        upper_band_point - (elbow - shoulder).normalized() * 0.018,
        upper_band_point + (elbow - shoulder).normalized() * 0.018,
        0.076,
        BOT_ORANGE,
        bevel=0.006,
    )
    parent_keep_world(upper_band, upper)

    elbow_joint = sphere(f"Arm_{side}_ElbowJoint", elbow, (0.073, 0.073, 0.073), BOT_JOINT)
    parent_keep_world(elbow_joint, upper)
    forearm = cylinder_between(
        f"Arm_{side}_ForearmShell", elbow, wrist, 0.067, BOT_SHELL, bevel=0.018
    )
    parent_keep_world(forearm, elbow_joint)
    wrist_joint = sphere(f"Arm_{side}_WristJoint", wrist, (0.053, 0.053, 0.053), BOT_JOINT)
    parent_keep_world(wrist_joint, forearm)

    hand = rounded_box(
        f"Arm_{side}_MittenHand",
        hand_center,
        (0.115, 0.105, 0.115),
        BOT_SHELL,
        bevel=0.035,
        bevel_segments=1,
    )
    parent_keep_world(hand, wrist_joint)


def add_leg(
    side: str,
    hip: Vector,
    knee: Vector,
    ankle: Vector,
    foot_center: Vector,
    pelvis: bpy.types.Object,
) -> None:
    hip_joint = sphere(f"Leg_{side}_HipJoint", hip, (0.082, 0.082, 0.082), BOT_JOINT)
    parent_keep_world(hip_joint, pelvis)
    thigh = cylinder_between(f"Leg_{side}_ThighShell", hip, knee, 0.078, BOT_SHELL, bevel=0.02)
    parent_keep_world(thigh, hip_joint)
    knee_joint = sphere(f"Leg_{side}_KneeJoint", knee, (0.078, 0.078, 0.078), BOT_JOINT)
    parent_keep_world(knee_joint, thigh)
    shin = cylinder_between(f"Leg_{side}_ShinShell", knee, ankle, 0.071, BOT_SHELL, bevel=0.018)
    parent_keep_world(shin, knee_joint)
    ankle_joint = sphere(f"Leg_{side}_AnkleJoint", ankle, (0.057, 0.057, 0.057), BOT_JOINT)
    parent_keep_world(ankle_joint, shin)

    ankle_band = add_ring(
        f"Leg_{side}_AnkleOrangeBand", ankle + Vector((0.0, 0.0, -0.015)), 0.074, 0.035, BOT_ORANGE
    )
    parent_keep_world(ankle_band, ankle_joint)
    foot = rounded_box(
        f"Leg_{side}_FootShell",
        foot_center,
        (0.175, 0.255, 0.122),
        BOT_SHELL,
        bevel=0.035,
        bevel_segments=1,
    )
    parent_keep_world(foot, ankle_joint)
    sole = rounded_box(
        f"Leg_{side}_FootSole",
        foot_center + Vector((0.0, 0.0, -0.061)),
        (0.148, 0.225, 0.018),
        BOT_JOINT,
        bevel=0.007,
        bevel_segments=1,
    )
    parent_keep_world(sole, foot)


def build_bot() -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    root = bpy.data.objects.new("BotRoot", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    root.location = (0.0, 0.0, 0.0)
    root["forward_axis"] = "-Y"
    root["up_axis"] = "Z"
    root["unit_height"] = 1.4
    ASSET_COLLECTION.objects.link(root)

    # Core body. The torso is a mesh root below BotRoot so Unity gets a clear
    # renderable hierarchy while the empty keeps the controller origin at feet.
    torso = rounded_box(
        "Torso_Core", Vector((0.0, 0.015, 0.78)), (0.47, 0.325, 0.405), BOT_SHELL, bevel=0.06, bevel_segments=3
    )
    parent_keep_world(torso, root)
    chest_inset = rounded_box(
        "Torso_ChestInset",
        Vector((0.0, -0.158, 0.81)),
        (0.27, 0.018, 0.17),
        BOT_METAL,
        bevel=0.008,
        bevel_segments=1,
    )
    parent_keep_world(chest_inset, torso)
    chest_mark = rounded_box(
        "Torso_OrangeBadge",
        Vector((0.0, -0.171, 0.81)),
        (0.105, 0.012, 0.072),
        BOT_ORANGE,
        bevel=0.012,
        bevel_segments=1,
    )
    parent_keep_world(chest_mark, torso)

    pelvis = rounded_box(
        "Pelvis_Core", Vector((0.0, 0.035, 0.56)), (0.36, 0.28, 0.17), BOT_SHELL, bevel=0.045
    )
    parent_keep_world(pelvis, torso)
    waist = beveled_cylinder("Waist_Joint", Vector((0.0, 0.035, 0.635)), 0.135, 0.085, BOT_JOINT, bevel=0.012)
    parent_keep_world(waist, torso)
    parent_keep_world(pelvis, waist)

    # Compact vertical backpack: a service pack rather than a rocket pack.
    pack = beveled_cylinder("Backpack_Cylinder", Vector((0.0, 0.225, 0.79)), 0.145, 0.33, BOT_METAL, bevel=0.026)
    parent_keep_world(pack, torso)
    pack_band_top = add_ring("Backpack_OrangeBandTop", Vector((0.0, 0.225, 0.89)), 0.151, 0.034, BOT_ORANGE)
    pack_band_bottom = add_ring("Backpack_OrangeBandBottom", Vector((0.0, 0.225, 0.69)), 0.151, 0.034, BOT_ORANGE)
    parent_keep_world(pack_band_top, pack)
    parent_keep_world(pack_band_bottom, pack)
    pack_cap = beveled_cylinder("Backpack_Cap", Vector((0.0, 0.225, 0.955)), 0.09, 0.02, BOT_JOINT, bevel=0.005)
    parent_keep_world(pack_cap, pack)

    neck = sphere("Neck_Joint", Vector((0.0, 0.0, 0.975)), (0.12, 0.105, 0.07), BOT_JOINT)
    parent_keep_world(neck, torso)
    # Deliberately oversized near-spherical helmet: its shell stays visible
    # above and below the visor at the small in-game screen size.
    head_center = Vector((0.0, -0.015, 1.085))
    head = sphere("Head_Helmet", head_center, (0.365, 0.345, 0.285), BOT_SHELL, segments=18, rings=8)
    parent_keep_world(head, neck)
    visor = curved_visor("Head_WrapVisor", head_center, BOT_VISOR)
    parent_keep_world(visor, head)
    for side, x in (("L", -0.130), ("R", 0.130)):
        emitter = sphere(
            f"Head_{side}_OrangeEmitter",
            Vector((x, -0.389, 1.085)),
            (0.052, 0.020, 0.070),
            BOT_ORANGE,
            segments=10,
            rings=5,
        )
        parent_keep_world(emitter, visor)

    for side, x, sign in (("L", -0.377, -1), ("R", 0.377, 1)):
        outer = add_ring(
            f"Head_{side}_SideDiscOuter", Vector((x, -0.005, 1.085)), 0.092, 0.034, BOT_JOINT, axis="X"
        )
        inner = add_ring(
            f"Head_{side}_SideDiscOrange", Vector((x + sign * 0.020, -0.005, 1.085)), 0.060, 0.016, BOT_ORANGE, axis="X"
        )
        parent_keep_world(outer, head)
        parent_keep_world(inner, outer)

    antenna_stem = beveled_cylinder("Head_AntennaStem", Vector((0.0, -0.01, 1.360)), 0.016, 0.055, BOT_JOINT, bevel=0.006)
    antenna_tip = sphere("Head_AntennaOrangeTip", Vector((0.0, -0.01, 1.376)), (0.024, 0.024, 0.024), BOT_ORANGE, segments=10, rings=5)
    parent_keep_world(antenna_stem, head)
    parent_keep_world(antenna_tip, antenna_stem)

    # Arms have named, independently rotatable mesh pivots. The right arm is
    # raised and forward to make the default look like a hold-reaching pose.
    add_arm(
        "R",
        Vector((0.275, -0.015, 0.89)),
        Vector((0.365, -0.115, 0.865)),
        Vector((0.415, -0.245, 1.025)),
        Vector((0.435, -0.275, 1.10)),
        torso,
    )
    add_arm(
        "L",
        Vector((-0.275, -0.005, 0.88)),
        Vector((-0.355, 0.015, 0.655)),
        Vector((-0.305, -0.090, 0.495)),
        Vector((-0.295, -0.145, 0.455)),
        torso,
    )

    # Short stable legs keep the sole plane exactly at Z=0 for a controller.
    add_leg(
        "R",
        Vector((0.145, -0.005, 0.535)),
        Vector((0.19, -0.09, 0.325)),
        Vector((0.155, -0.145, 0.16)),
        Vector((0.155, -0.215, 0.070)),
        pelvis,
    )
    add_leg(
        "L",
        Vector((-0.145, 0.01, 0.535)),
        Vector((-0.19, 0.06, 0.32)),
        Vector((-0.165, -0.005, 0.16)),
        Vector((-0.165, -0.075, 0.070)),
        pelvis,
    )

    # A small head yaw adds 3/4 personality while retaining -Y as the rig's
    # authored forward direction. All face parts are already parented to it.
    head.rotation_euler[2] = math.radians(-12)
    bpy.context.view_layer.update()

    meshes = [obj for obj in ASSET_COLLECTION.objects if obj.type == "MESH"]
    for obj in meshes:
        obj["asset_role"] = "bot_geometry"
    return root, meshes


def mesh_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector, int]:
    lower = Vector((float("inf"), float("inf"), float("inf")))
    upper = Vector((float("-inf"), float("-inf"), float("-inf")))
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        for vertex in obj.data.vertices:
            point = obj.matrix_world @ vertex.co
            lower.x = min(lower.x, point.x)
            lower.y = min(lower.y, point.y)
            lower.z = min(lower.z, point.z)
            upper.x = max(upper.x, point.x)
            upper.y = max(upper.y, point.y)
            upper.z = max(upper.z, point.z)
    return lower, upper, triangles


def export_asset(root: bpy.types.Object, meshes: list[bpy.types.Object]) -> None:
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    FBX_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), check_existing=False)

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        check_existing=False,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        use_armature_deform_only=False,
        path_mode="AUTO",
        embed_textures=False,
    )


clear_scene()
ASSET_COLLECTION = bpy.data.collections.new("Bot_Asset")
bpy.context.scene.collection.children.link(ASSET_COLLECTION)

BOT_SHELL = add_material("BotShell", "#E8ECEC", metallic=0.08, roughness=0.28)
BOT_JOINT = add_material("BotJoint", "#10131B", metallic=0.45, roughness=0.42)
BOT_VISOR = add_material("BotVisor", "#070A10", metallic=0.18, roughness=0.17)
BOT_ORANGE = add_material("BotOrange", "#FD8704", metallic=0.08, roughness=0.24, emission_strength=1.8)
BOT_METAL = add_material("BotMetal", "#657083", metallic=0.82, roughness=0.28)

BOT_ROOT, BOT_MESHES = build_bot()
LOWER, UPPER, TRIANGLES = mesh_bounds(BOT_MESHES)
if TRIANGLES >= 4000:
    for mesh in sorted(BOT_MESHES, key=lambda item: item.name):
        mesh.data.calc_loop_triangles()
        print(f"triangle_detail={mesh.name}:{len(mesh.data.loop_triangles)}")
    raise RuntimeError(f"Bot triangle target exceeded: {TRIANGLES}")
export_asset(BOT_ROOT, BOT_MESHES)

print("BOT_ASSET_REPORT")
print(f"blend={BLEND_PATH}")
print(f"fbx={FBX_PATH}")
print(f"meshes={len(BOT_MESHES)}")
print(f"triangles={TRIANGLES}")
print(f"bounds_min=({LOWER.x:.4f}, {LOWER.y:.4f}, {LOWER.z:.4f})")
print(f"bounds_max=({UPPER.x:.4f}, {UPPER.y:.4f}, {UPPER.z:.4f})")
print(f"size=({UPPER.x - LOWER.x:.4f}, {UPPER.y - LOWER.y:.4f}, {UPPER.z - LOWER.z:.4f})")
