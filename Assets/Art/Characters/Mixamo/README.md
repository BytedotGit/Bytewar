# Mixamo (Local-Only)

This folder is intended for **local developer imports** of Mixamo FBX files.

## Why the FBX files are not committed

Bytewar is a **public** repo. To reduce licensing risk and repo bloat, we do **not** commit third-party raw FBX files.

## How to enable Mixamo usage locally

Drop the expected FBX files into this folder locally.

The project will **auto-detect** whether the required Mixamo files exist:
- If present, generators will process and prefer the Mixamo character/animations.
- If missing, the project uses **procedural fallback** visuals and **placeholder** animation clips.

## Expected files (if you import Mixamo)

- `Character.fbx`
- `Idle.fbx`
- `Walking.fbx`
- `Running.fbx`
- `Jump.fbx`
- `Attack.fbx`
