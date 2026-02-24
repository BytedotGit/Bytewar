---
description: Blender MCP workflow, export rules, and asset pipeline for ByteWar.
applyTo: "Tools/BlenderMCP/**/*"
---

# Blender MCP — Workflow & Export Rules

This repository supports a **headless Blender MCP server** at `Tools/BlenderMCP/`.

## Goals

- Generate and iterate on **first-party** (licensed-to-us) 3D assets in Blender.
- Export assets into Unity under `Assets/Art/`.
- Keep the current **Mixamo placeholder pipeline** working until custom assets replace it.

## Non-negotiables

- **Localhost only**: Blender bridge must never listen on non-localhost interfaces.
- **No auto-downloading**: the pipeline must not fetch third-party assets.
- **Deterministic**: tool operations should be safe to re-run.

## Export target layout

Export into these folders (created as needed):

- `Assets/Art/Characters/Player/<AssetName>/<AssetName>.fbx`
- `Assets/Art/Characters/Enemies/<AssetName>/<AssetName>.fbx`
- `Assets/Art/Characters/NPCs/<AssetName>/<AssetName>.fbx`
- `Assets/Art/Environment/.../<AssetName>/<AssetName>.fbx`
- `Assets/Art/Weapons/.../<AssetName>/<AssetName>.fbx`
- `Assets/Art/Buildings/<AssetName>/<AssetName>.fbx`
- `Assets/Art/Animations/<AssetName>/<AssetName>.fbx`
- `Assets/Art/Premade/<SourceName>/...` (staging only)

## Naming

- Use `PascalCase` for `AssetName` (no spaces).
- Do not rely on file names with special characters.

## Units / orientation

- Blender must export FBX in a **Unity-friendly axis mapping** (Forward = `-Z`, Up = `Y`).
- Apply transforms before export (**location + rotation + scale**). Do not ship Blender objects with non-identity transforms.

## Production-ready requirements (Blender → Unity)

These rules exist so generated assets behave correctly when deployed in-game (collision, placement, shading, LODs), and so agents can follow a strict checklist.

### Transforms

- Apply **all** transforms before export: Location/Rotation/Scale.
- Environment props should typically sit on the ground plane with pivot/origin at the base center.

### LOD naming

- If an asset is expected to be visible at varying distances, export LOD meshes in the same FBX using:
  - `<AssetName>_LOD0`, `<AssetName>_LOD1`, `<AssetName>_LOD2`

### Collision / solidity

Collision is convention-driven so Unity generators can be deterministic:

- **Solid** assets (rocks, props, buildings) must include a collider mesh named:
  - `<AssetName>_COL` (Unity will create a solid collider; `isTrigger=false`)
- **Non-solid but detectable** assets (water volumes, detection zones) must include:
  - `<AssetName>_TRIG` (Unity will create a trigger collider; `isTrigger=true`)
- If both exist, `_TRIG` takes precedence.
- Collider meshes must not render in-game; Unity prefab generation will disable their renderers.
- If no collider mesh exists, the Unity generator falls back to a bounds-based `BoxCollider` (solid).

### Materials (project baseline)

- This project uses the Built-in Render Pipeline **Standard** shader baseline. Avoid URP/HDRP-specific materials.
- Prefer fewer materials per asset; one material per prop is ideal for batching and performance.

## LOD policy

- Characters/enemies may be high-poly, but must have an LOD plan.
- Environment props should be low/mid-poly and aggressively LOD’d.

## Mixamo coexistence (temporary)

- Mixamo assets are local-only and live under `Assets/Art/Characters/Mixamo/`.
- Do not change or break `MixamoProcessor`/`MixamoLocalAssets` behavior.
- Custom Blender assets should be introduced in parallel, then take over via generation/prefab wiring when ready.
