# Mixamo (Local-Only)

This folder is intended for **local developer imports** of Mixamo FBX files.

## Why the FBX files are not committed

Bytewar is a **public** repo. To reduce licensing risk and repo bloat, we do **not** commit third-party raw FBX files.

## How to enable Mixamo usage locally

Create this file (it is intentionally ignored by git):

`Assets/Art/Characters/Mixamo/ENABLE_MIXAMO_LOCAL.txt`

When this marker exists:
- `MixamoProcessor` will process Mixamo assets.
- `AnimatorGenerator` will search Mixamo clips.
- `PrefabGenerator` will prefer the Mixamo character model.

When this marker does **not** exist:
- The project uses **procedural fallback** visuals and **placeholder** animation clips.

## Expected files (if you import Mixamo)

- `Character.fbx`
- `Idle.fbx`
- `Walking.fbx`
- `Running.fbx`
- `Jump.fbx`
- `Attack.fbx`
