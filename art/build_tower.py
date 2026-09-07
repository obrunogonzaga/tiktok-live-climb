"""Build the approved tower assembly and its reusable Blender kit.

Usage:
  Blender --background --python art/build_tower.py
  Blender --background --python art/build_tower.py -- --kit-only

The kit-only form exports only ``Assets/Art/Environment/Kit/*.fbx``.  It is
safe to run while another process validates the approved Tower/Ledge assets.
"""

from __future__ import annotations

import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "game" / "Assets" / "Art" / "Environment"
KIT_OUT = OUT / "Kit"
KIT_ONLY = "--kit-only" in sys.argv

OUT.mkdir(parents=True, exist_ok=True)
KIT_OUT.mkdir(parents=True, exist_ok=True)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def new_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for owner in tuple(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_transform(obj: bpy.types.Object) -> None:
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def parent_keep_world(child: bpy.types.Object, parent: bpy.types.Object) -> None:
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    result: list[bpy.types.Object] = []
    pending = list(root.children)
    while pending:
        child = pending.pop()
        result.append(child)
        pending.extend(child.children)
    return result


def create_materials() -> tuple[list[bpy.types.Material], bpy.types.Material, bpy.types.Material]:
    stones: list[bpy.types.Material] = []
    for index in range(6):
        material = bpy.data.materials.new(f"Stone{index}")
        material.diffuse_color = (.34 + index * .02, .36 + index * .02, .42 + index * .02, 1)
        stones.append(material)
    mortar = bpy.data.materials.new("Mortar")
    mortar.diffuse_color = (.045, .055, .075, 1)
    ivy = bpy.data.materials.new("Ivy")
    ivy.diffuse_color = (.055, .13, .025, 1)
    return stones, mortar, ivy


STONES, MORTAR, IVY = create_materials()


def brick(
    name: str,
    position: tuple[float, float, float],
    scale: tuple[float, float, float],
    bevel: float = .04,
    material: bpy.types.Material | None = None,
    rough: bool = True,
    rng: random.Random | object = random,
    collection: bpy.types.Collection | None = None,
) -> bpy.types.Object:
    """Create the worn, UV-projected stone unit used by both assembly and kit."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rough:
        for vertex in obj.data.vertices:
            vertex.co += Vector((rng.uniform(-.018, .018), rng.uniform(-.014, .014), rng.uniform(-.014, .014)))
    modifier = obj.modifiers.new("Worn stone edges", "BEVEL")
    modifier.width = bevel
    modifier.segments = 2
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.materials.append(material or rng.choice(STONES))
    uv = obj.data.uv_layers.new(name="UVMap")
    for polygon in obj.data.polygons:
        normal = polygon.normal
        axis = max(range(3), key=lambda index: abs(normal[index]))
        axes = [index for index in range(3) if index != axis]
        for loop_index in polygon.loop_indices:
            point = obj.matrix_world @ obj.data.vertices[obj.data.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (point[axes[0]] / 2.1, point[axes[1]] / 2.1)
    if collection is not None:
        move_to_collection(obj, collection)
    return obj


def add_ivy_leaf(
    name: str,
    position: tuple[float, float, float],
    scale: tuple[float, float, float],
    rotation: tuple[float, float, float],
    collection: bpy.types.Collection | None = None,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.rotation_euler = rotation
    obj.data.materials.append(IVY)
    if collection is not None:
        move_to_collection(obj, collection)
    return obj


def export_selected(path: Path, objects: list[bpy.types.Object]) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        bake_anim=False,
        add_leaf_bones=False,
        use_mesh_modifiers=True,
    )


def triangle_count(objects: list[bpy.types.Object]) -> int:
    total = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    low = Vector((float("inf"), float("inf"), float("inf")))
    high = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        if obj.type != "MESH":
            continue
        for vertex in obj.data.vertices:
            point = obj.matrix_world @ vertex.co
            low.x = min(low.x, point.x)
            low.y = min(low.y, point.y)
            low.z = min(low.z, point.z)
            high.x = max(high.x, point.x)
            high.y = max(high.y, point.y)
            high.z = max(high.z, point.z)
    return low, high


def create_kit_root(name: str, description: str, collection: bpy.types.Collection) -> bpy.types.Object:
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "CUBE"
    root.empty_display_size = .18
    root["kit_id"] = name
    root["origin_rule"] = description
    root["unit"] = "1 Blender unit = 1 metre"
    collection.objects.link(root)
    return root


def kit_brick(
    root: bpy.types.Object,
    name: str,
    position: tuple[float, float, float],
    scale: tuple[float, float, float],
    rng: random.Random,
    *,
    bevel: float = .04,
    material: bpy.types.Material | None = None,
    rotation: tuple[float, float, float] | None = None,
) -> bpy.types.Object:
    obj = brick(name, position, scale, bevel, material, True, rng, KIT_COLLECTION)
    if rotation is not None:
        obj.rotation_euler = rotation
        apply_transform(obj)
    parent_keep_world(obj, root)
    return obj


def kit_leaf(
    root: bpy.types.Object,
    name: str,
    position: tuple[float, float, float],
    scale: tuple[float, float, float],
    rotation: tuple[float, float, float],
) -> bpy.types.Object:
    obj = add_ivy_leaf(name, position, scale, rotation, KIT_COLLECTION)
    parent_keep_world(obj, root)
    return obj


def build_masonry_panel(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("MasonryPanel", "bottom-centre on outer wall plane; local +Y faces outward", KIT_COLLECTION)
    width = 4.4
    count = 7
    step = width / count
    for row in range(8):
        z = row * .4 + .2
        for column in range(count):
            x = -width / 2 + (column + .5) * step + (.07 if row % 2 else -.07)
            depth = .32 + rng.random() * .1
            kit_brick(root, f"Panel brick {row:02}-{column}", (x, -depth, z), (step - .025, depth, .365), rng)
    return root


def build_masonry_arch_panel(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("MasonryArchPanel", "bottom-centre on outer wall plane; arch opening centred on origin", KIT_COLLECTION)
    width = 4.4
    count = 7
    step = width / count
    for row in range(8):
        z = row * .4 + .2
        for column in range(count):
            x = -width / 2 + (column + .5) * step + (.07 if row % 2 else -.07)
            if abs(x) < .59 and .15 < z < 1.95:
                continue
            if abs(x) < .43 and 1.95 <= z < 2.35:
                continue
            depth = .32 + rng.random() * .1
            kit_brick(root, f"Arch panel brick {row:02}-{column}", (x, -depth, z), (step - .025, depth, .365), rng)
    for side in (-1, 1):
        for row in range(5):
            kit_brick(root, f"Arch jamb block {side}-{row}", (side * .70, -.36, .2 + row * .35), (.26, .36, .32), rng, bevel=.025)
    for index in range(11):
        angle = (index + .5) * math.pi / 11
        kit_brick(
            root,
            f"Arch wedge {index:02}",
            (.7 * math.cos(angle), -.36, 1.9 + .7 * math.sin(angle)),
            (.29, .38, .35),
            rng,
            bevel=.028,
            rotation=(0, angle - math.pi / 2, 0),
        )
    return root


def build_corner_pillar(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("CornerPillar", "bottom outer corner; mass extends into local -X and -Y", KIT_COLLECTION)
    for row in range(8):
        z = row * .51 + .25
        kit_brick(root, f"Pillar block {row:02}", (-.46, -.46, z), (.46, .46, .48), rng, bevel=.055)
    return root


def build_cornice_straight(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("CorniceStraight", "bottom-centre on outer wall plane; local +Y faces outward", KIT_COLLECTION)
    width = 4.65
    for column in range(6):
        x = -width / 2 + (column + .5) * width / 6
        kit_brick(root, f"Cornice block {column}", (x, -.50, .14), (width / 6 - .018, .50, .28), rng, bevel=.045)
    return root


def build_cornice_corner(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("CorniceCorner", "bottom outer corner; arms extend along local -X and -Y", KIT_COLLECTION)
    for index in range(3):
        offset = -.42 - index * .73
        kit_brick(root, f"Corner X arm {index}", (offset, -.50, .14), (.36, .50, .28), rng, bevel=.045)
        kit_brick(root, f"Corner Y arm {index}", (-.50, offset, .14), (.50, .36, .28), rng, bevel=.045)
    kit_brick(root, "Corner keystone", (-.50, -.50, .14), (.50, .50, .28), rng, bevel=.045)
    return root


def build_arch_jamb(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("ArchJamb", "bottom at jamb foot; local +Y faces outward", KIT_COLLECTION)
    for row in range(5):
        kit_brick(root, f"Jamb block {row}", (0, -.36, row * .35 + .18), (.26, .36, .32), rng, bevel=.025)
    return root


def build_arch_voussoir(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("ArchVoussoir", "centre of wedge at arch radius; rotate around local Y to arc", KIT_COLLECTION)
    kit_brick(root, "Wedge", (0, -.38, .18), (.29, .38, .35), rng, bevel=.028, rotation=(0, -math.pi / 2, 0))
    return root


def build_ledge_deck(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("LedgeDeck", "top-centre standing surface; local +Y faces away from tower", KIT_COLLECTION)
    for index in range(3):
        kit_brick(root, f"Deck stone {index}", ((index - 1) * .46, 0, -.14), (.455, .86, .28), rng, bevel=.055)
    return root


def build_ledge_corbel(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("LedgeCorbel", "attachment plane on tower at local -Y; deck support extends +Y", KIT_COLLECTION)
    kit_brick(root, "Corbel", (0, .20, -.39), (.64, .48, .32), rng, bevel=.065)
    return root


def build_crown_post(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("CrownPost", "bottom outer corner of battlement post", KIT_COLLECTION)
    kit_brick(root, "Crown post", (-.68, -.68, .625), (.68, .68, 1.25), rng, bevel=.065)
    return root


def build_crown_cap(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("CrownCap", "bottom centre of crown cap", KIT_COLLECTION)
    kit_brick(root, "Crown cap", (0, 0, .11), (.84, .84, .22), rng, bevel=.05)
    return root


def build_ivy_cluster(rng: random.Random) -> bpy.types.Object:
    root = create_kit_root("IvyCluster", "wall attachment point; leaves project in local +Y", KIT_COLLECTION)
    x = 0.0
    for index in range(16):
        z = 2.7 - index * .17
        x += rng.uniform(-.11, .11)
        kit_leaf(
            root,
            f"Ivy leaf {index:02}",
            (x, .018, z),
            (rng.uniform(.04, .085), .018, rng.uniform(.08, .13)),
            (0, rng.uniform(-.6, .6), rng.uniform(-.5, .5)),
        )
    return root


KIT_COLLECTION = new_collection("KIT · modular source")


def build_kit() -> list[bpy.types.Object]:
    rng = random.Random(1515)
    builders = [
        build_masonry_panel,
        build_masonry_arch_panel,
        build_corner_pillar,
        build_cornice_straight,
        build_cornice_corner,
        build_arch_jamb,
        build_arch_voussoir,
        build_ledge_deck,
        build_ledge_corbel,
        build_crown_post,
        build_crown_cap,
        build_ivy_cluster,
    ]
    roots = [builder(rng) for builder in builders]
    for root in roots:
        objects = [root, *descendants(root)]
        export_selected(KIT_OUT / f"{root.name}.fbx", objects)
        mesh_objects = [obj for obj in objects if obj.type == "MESH"]
        low, high = bounds(mesh_objects)
        print(
            f"KIT_REPORT name={root.name} tris={triangle_count(mesh_objects)} "
            f"min=({low.x:.3f},{low.y:.3f},{low.z:.3f}) "
            f"max=({high.x:.3f},{high.y:.3f},{high.z:.3f})"
        )
    return roots


def build_approved_assembly() -> tuple[list[bpy.types.Object], list[bpy.types.Object]]:
    """Keep the approved composition's detailed construction and proportions."""
    random.seed(1402)
    brick("Structural masonry core", (0, 0, 7), (4.18, 4.18, 14), .06, MORTAR, False)
    for row in range(35):
        z = row * .4 + .2
        width = 4.4 - .018 * z
        count = 7
        step = width / count
        for side in range(4):
            for column in range(count):
                a = -width / 2 + (column + .5) * step + (.07 if row % 2 else -.07)
                if abs(a - .7) < .59 and 4.8 < z < 6.6:
                    continue
                if abs(a - .7) < .43 and 6.6 <= z < 7:
                    continue
                if side % 2 == 0:
                    position = (a, (-1 if side == 0 else 1) * (width / 2), z)
                    scale = (step - .025, .32 + random.random() * .10, .365)
                else:
                    position = ((-1 if side == 1 else 1) * (width / 2), a, z)
                    scale = (.32 + random.random() * .1, step - .025, .365)
                brick(f"Masonry_{side}_{row:02}_{column}", position, scale)

    for height in [-.2, 3.2, 7.6, 11.6, 13.85]:
        width = 4.65 - .018 * max(0, height)
        for side in range(4):
            for column in range(6):
                a = -width / 2 + (column + .5) * width / 6
                position = (a, (-1 if side == 0 else 1) * width / 2, height) if side % 2 == 0 else ((-1 if side == 1 else 1) * width / 2, a, height)
                scale = (width / 6 - .018, .5, .28) if side % 2 == 0 else (.5, width / 6 - .018, .28)
                brick("Cornice", position, scale, .045)

    for x in [-2.18, 2.18]:
        for y in [-2.18, 2.18]:
            for row in range(27):
                brick("Corner pier", (x, y, row * .51 + .25), (.46, .46, .48), .055)
            brick("Crown post", (x, y, 14.25), (.68, .68, 1.25), .065)
            brick("Crown cap", (x, y, 14.93), (.84, .84, .22), .05)

    for side in [-1, 1]:
        for row in range(5):
            brick("Arch jamb", (.7 + side * .70, -2.35, 4.8 + row * .35), (.26, .36, .32), .025)
    for index in range(11):
        angle = (index + .5) * math.pi / 11
        wedge = brick("Arch voussoir", (.7 + .7 * math.cos(angle), -2.35, 6.5 + .7 * math.sin(angle)), (.29, .38, .35), .028)
        wedge.rotation_euler[1] = angle - math.pi / 2

    for _ in range(5):
        x = random.uniform(-2, 2)
        top = random.uniform(7, 13)
        for index in range(random.randint(12, 23)):
            z = top - index * .17
            x += random.uniform(-.11, .11)
            add_ivy_leaf(
                "Ivy leaf",
                (x, -2.37, z),
                (random.uniform(.04, .085), .018, random.uniform(.08, .13)),
                (0, random.uniform(-.6, .6), random.uniform(-.5, .5)),
            )

    bpy.ops.object.select_all(action="DESELECT")
    for material in [*STONES, MORTAR, IVY]:
        group = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.data.materials and obj.data.materials[0] == material]
        for obj in group:
            obj.select_set(True)
        if group:
            bpy.context.view_layer.objects.active = group[0]
            bpy.ops.object.join()
            bpy.context.object.name = "Tower_" + material.name
        bpy.ops.object.select_all(action="DESELECT")

    tower_meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name.startswith("Tower_")]
    export_selected(OUT / "Tower.fbx", tower_meshes)

    ledge: list[bpy.types.Object] = []
    for index in range(3):
        ledge.append(brick("Ledge stone", ((index - 1) * .46, 0, -.14), (.455, .86, .28), .055))
    ledge.append(brick("Ledge corbel", (0, .2, -.39), (.64, .48, .32), .065))
    export_selected(OUT / "Ledge.fbx", ledge)
    return tower_meshes, ledge


clear_scene()

if KIT_ONLY:
    kit_roots = build_kit()
    print(f"KIT_ONLY_READY pieces={len(kit_roots)} out={KIT_OUT}")
else:
    tower_meshes, ledge_meshes = build_approved_assembly()
    assembly_collection = new_collection("ASSEMBLY · approved tower")
    for obj in [*tower_meshes, *ledge_meshes]:
        move_to_collection(obj, assembly_collection)
    kit_roots = build_kit()
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "art" / "tower-kit.blend"))
    print(
        f"TOWER_AND_KIT_READY tower_tris={triangle_count(tower_meshes)} "
        f"ledge_tris={triangle_count(ledge_meshes)} pieces={len(kit_roots)}"
    )
