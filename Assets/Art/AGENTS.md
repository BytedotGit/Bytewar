# Art — Agent Guidance

## Legality / licensing

- Prefer manifest-driven local import for third-party assets.
- Do not commit third-party raw FBX/texture/audio unless license is explicitly compatible.

## Performance

- High-poly characters are allowed but require LOD.
- Environment assets should be low-poly and aggressively LOD’d.

## Production-ready art contract (high level)

- Prefer **readable silhouettes** over micro-detail for environment props (Valheim-like readability).
- Reserve richer surface detail for **hero props** and characters.
- Prefer **few materials** per asset (ideally 1) to support batching/instancing.

## Collision conventions (Blender-generated)

- Solid collider mesh: `<AssetName>_COL`.
- Trigger volume mesh: `<AssetName>_TRIG` (water, detection).

## Blender MCP pipeline (first-party assets)

- Use `Tools/BlenderMCP/` to generate/export first-party 3D assets into `Assets/Art/`.
- Prefer exporting into category folders rather than dropping loose FBX files:
  - `Assets/Art/Characters/Player/`, `Enemies/`, `NPCs/`
  - `Assets/Art/Environment/Props/`, `Rocks/`, `Vegetation/`, `Structures/`
  - `Assets/Art/Weapons/Melee/`, `Ranged/`, `Shields/`
  - `Assets/Art/Buildings/`, `Assets/Art/Animations/`
- Premade assets should be staged under `Assets/Art/Premade/` and cleaned/re-exported before use.

## Mixamo placeholder policy (temporary)

- Mixamo FBX files remain local-only under `Assets/Art/Characters/Mixamo/`.
- Do not commit third-party raw FBX unless license is explicitly compatible.
