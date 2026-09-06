# Bot asset

Compact toy/service robot for the tower scene. It faces **-Y**, uses **Z up**,
and `BotRoot` sits on the sole plane at `(0, 0, 0)` for the CharacterController.
The source pose reaches up and forward with the right hand; all limbs remain
separate transformable mesh objects for simple Unity animation.

## Build

```sh
/Users/bruno/Applications/Blender.app/Contents/MacOS/Blender \
  --background --python art/build_bot.py
```

Outputs:

- `art/bot.blend`
- `game/Assets/Art/Bot/Bot.fbx`

The FBX export selects only `BotRoot` and Bot mesh geometry. It contains no
camera or light.

## Material slots

| Material | Use |
| --- | --- |
| `BotShell` | warm white rounded shell plates |
| `BotJoint` | matte black articulated joints and soles |
| `BotVisor` | black wrap-around faceplate |
| `BotOrange` | orange emitters, antenna tip, bands and badge |
| `BotMetal` | backpack and chest inset |

## Animation hierarchy

`BotRoot` → `Torso_Core` is the scene root. The important independently
rotatable chains are `Arm_[L|R]_ShoulderJoint` → upper shell → elbow joint →
forearm shell → wrist joint → mitten hand, and `Leg_[L|R]_HipJoint` → thigh
shell → knee joint → shin shell → ankle joint → foot. `Head_Helmet` owns the
visor, emitters, side discs, and antenna.

## Measured export

- 50 mesh objects, 3,932 triangles
- bounds min `(-0.4210, -0.4290, 0.0000)`
- bounds max `(0.4925, 0.3760, 1.4000)`
- size `(0.9135, 0.8050, 1.4000)` Unity units

The FBX was imported back into a clean Blender scene: all five required
material names, all 50 meshes, the root, named limb pivots, and the measured
bounds survived the round trip.
