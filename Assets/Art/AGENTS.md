# Art — Agent Guidance

## Legality / licensing

- Prefer manifest-driven local import for third-party assets.
- Do not commit third-party raw FBX/texture/audio unless license is explicitly compatible.
- CC0-first policy: prefer assets with clear permissive licensing and no attribution requirement for baseline production.
- Paid assets are allowed after style lock and budget approval; record license proof and usage terms in import notes.

## Performance

- High-poly characters are allowed but require LOD.
- Environment assets should be low-poly and aggressively LOD’d.

## Production-ready art contract (high level)

- Prefer **readable silhouettes** over micro-detail for environment props (Valheim-like readability).
- Reserve richer surface detail for **hero props** and characters.
- Prefer **few materials** per asset (ideally 1) to support batching/instancing.
- Use **chunky stylized** character silhouettes and keep gameplay readability at mid/far camera zoom.
- Environments should remain low-poly stylized even when character detail increases.

## Locked sourcing + style policy

- Primary visual references: Valheim (environment readability) and Overwatch (character readability/polish).
- Approved sources (license-checked): Quaternius, Kenney, Poly Haven, Unity Asset Store, Synty Store, Sketchfab, OpenGameArt.
- Keep one primary environment pack family to avoid style drift; treat secondary packs as gap-fill only.

## Primary model sourcing policy

- Premade, license-verified assets are the default source for production models.
- Blender-generated first-party models are fallback only when no approved premade option meets style, performance, and licensing gates.
- If a fallback is used, the manifest must include a reason in `license.notes` and point to a preferred premade replacement candidate.

## Asset intake checklist (required)

- License verified and compatible with commercial distribution.
- Source, author, and license type logged in import notes.
- Naming and folder placement follow repo conventions.
- LODs present or planned (`LOD0/LOD1/LOD2`) for runtime assets.
- Collider strategy defined (`_COL`, `_TRIG`, or generated colliders).
- Material count minimized and shader selection consistent with project rendering pipeline.
- Destruction compatibility tagged where relevant (trees, rocks, build pieces).

## Asset manifest workflow

- Use `Assets/Art/asset_manifest.template.json` as the canonical manifest format for third-party and imported first-party assets.
- Keep an evolving candidate list in `Assets/Art/asset_shortlist.md` before bulk importing packs.

## Hero polish priorities

- First polish targets:
  1. Player base body.
  2. Starter armor set.
  3. Starter weapon set.
  4. Core tree family.
  5. Core rock/ore family.
  6. Main build set (wall/floor/roof).
  7. Signature biome landmark.

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
- Do not treat Blender MCP output as the first choice for production character/prop models when approved premade assets are available.

## Mixamo placeholder policy (temporary)

- Mixamo FBX files remain local-only under `Assets/Art/Characters/Mixamo/`.
- Do not commit third-party raw FBX unless license is explicitly compatible.
