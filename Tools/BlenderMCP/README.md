# Blender MCP Server

This is a Model Context Protocol (MCP) server that lets an AI assistant drive **headless Blender** to generate and export 3D assets into the ByteWar Unity project.

It follows the same shape as the existing Unity MCP server in `Tools/UnityMCP/`:

- Node.js MCP server (stdio)
- Localhost HTTP bridge inside the target app (Blender)

## Prerequisites

- Node.js v18+
- Blender installed locally
  - If Blender is not on PATH, set `BLENDER_PATH` to your `blender.exe`.

## Setup

```bash
cd Tools/BlenderMCP
npm install
npm start
```

The first Blender tool call will launch a background Blender process and start a localhost-only bridge on `http://127.0.0.1:8766/`.

## Environment variables

- `BLENDER_PATH`: Full path to Blender executable.
- `BLENDER_MCP_PORT`: Override bridge port (default: `8766`).
- `BLENDER_MCP_NO_LAUNCH=1`: Prevents launching Blender (useful for debugging).

## Available tools (initial set)

- `blender_status`
- `blender_reset_scene`
- `blender_list_objects`
- `blender_add_primitive`
- `blender_delete_object`
- `blender_export_fbx`
- `blender_export_fbx_to_unity`
- `blender_execute_script`
- `blender_generate_large_tree` (default `styleProfile=GeometricLowPoly`)
- `blender_render_large_tree_preview`
- `blender_generate_large_tree_variations`

### LargeTree style/variation presets

`blender_generate_large_tree` supports optional parameters:

- `styleProfile`:
  - `GeometricLowPoly` (default, faceted blocky low-poly style)
  - `LegacyBroadleaf` / `StylizedLowPolyForestV2_CleanCanopy` (legacy branch-heavy style)
- `variation` (used by `GeometricLowPoly`):
  - `Seedling`
  - `Sapling`
  - `Young`
  - `Mature`
  - `Adult`

### Batch variation generation

`blender_generate_large_tree_variations` runs one deterministic generation/export pass per variation and writes outputs to:

- `Assets/Art/Environment/Vegetation/LargeTree/Variants/<Variation>/LargeTree_<Variation>.fbx`
- `Assets/Art/Environment/Vegetation/LargeTree/Variants/<Variation>/Preview.png`

It also renders a contact-sheet preview for the generated set:

- `Assets/Art/Environment/Vegetation/LargeTree/Variants/LargeTreeVariationsSheet.png`

## Notes for ByteWar

- Export destinations are under `Assets/Art/...`.
- This repo intentionally avoids committing third-party raw FBX files (see `Assets/Art/Characters/Mixamo/README.md`).

## Production-ready export contract

Generated assets should be ready to deploy in-game (correct scale, collision, LODs, and deterministic import).

### Required transforms

- Apply **Location + Rotation + Scale** before export.

### LOD naming (single FBX)

- Export meshes using:
  - `<AssetName>_LOD0`, `<AssetName>_LOD1`, `<AssetName>_LOD2`

### Collision / solidity naming

- Solid collider mesh: `<AssetName>_COL` (Unity prefab generation produces a solid collider; `isTrigger=false`).
- Trigger volume mesh: `<AssetName>_TRIG` (Unity prefab generation produces a trigger collider; `isTrigger=true`).
- If both exist, `_TRIG` wins.
