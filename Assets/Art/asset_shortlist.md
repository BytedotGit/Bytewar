# Asset Shortlist (Batch 01)

This file tracks the first executable import pass for production art.

Direction lock:

- Low-poly stylized environment readability (Valheim-like).
- Chunky stylized character/gear readability (Overwatch-inspired silhouette clarity).
- CC0-first and license-verified before import.

## Approved Source Pool

| Source            | License posture    | Primary use                               | Status      |
| ----------------- | ------------------ | ----------------------------------------- | ----------- |
| Quaternius        | CC0                | Environment vegetation/rocks/props        | Active      |
| Kenney            | CC0                | Modular filler props and lightweight sets | Active      |
| Poly Haven        | CC0                | Support textures and lighting references  | Active      |
| Unity Asset Store | Per-asset EULA     | Targeted hero assets only                 | Conditional |
| Synty Store       | Commercial license | Style-consistent pack fallback            | Conditional |

Policy note:

- Premade assets are primary for production models.
- Blender-generated models are fallback only.

## Batch 01 - Ten Asset Targets

| ID     | Category               | Target                                          | Source                         | License         | Priority | Owner       | Status      |
| ------ | ---------------------- | ----------------------------------------------- | ------------------------------ | --------------- | -------- | ----------- | ----------- |
| B01-01 | Characters/Player      | Player base body refresh                        | Unity Asset Store / Synty      | EULA/Commercial | P0       | Art         | Planned     |
| B01-02 | Characters/Gear        | Starter armor set (chest/legs/helm)             | Unity Asset Store / Synty      | EULA/Commercial | P0       | Art         | Planned     |
| B01-03 | Weapons/Melee          | Starter weapon set (axe/sword)                  | Kenney / Unity Asset Store     | CC0/EULA        | P0       | Art         | Planned     |
| B01-04 | Environment/Vegetation | Tree family A (seedling->adult)                 | Quaternius                     | CC0             | P0       | Art         | In progress |
| B01-05 | Environment/Rocks      | Rock/ore family A                               | Quaternius                     | CC0             | P0       | Art         | Planned     |
| B01-06 | Buildings              | Main build set materials pass (wall/floor/roof) | First-party                    | Internal        | P0       | Art         | Planned     |
| B01-07 | Environment/Props      | Biome prop kit (logs, stumps, foliage clutter)  | Kenney/Quaternius              | CC0             | P1       | Art         | Planned     |
| B01-08 | Environment/Landmark   | Signature biome landmark                        | Quaternius / Unity Asset Store | CC0/EULA        | P1       | Art         | Planned     |
| B01-09 | VFX support mesh       | Spell/gathering impact helper meshes            | First-party                    | Internal        | P1       | Tech Art    | Planned     |
| B01-10 | UI/Preview             | Deploy radial preview stand-ins                 | First-party                    | Internal        | P1       | UI/Tech Art | Planned     |

## First Three Integration Targets (Execute Next)

These are tied to existing runtime assets already present in repo and can be wired immediately.

| Target                          | Existing Runtime Path                                                 | Integration Goal                                                                           | Auto Validation Hook                                           |
| ------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ | -------------------------------------------------------------- |
| Tree family A                   | `Assets/Resources/Generated/LargeTree/LargeTree.prefab`               | Replace placeholder visuals with production variants while keeping collider/LOD guarantees | `AutoTestScenarioLargeTree` + focused EditMode geometry checks |
| Deployable hero prop A          | `Assets/Resources/Generated/BlenderE2EProp/BlenderE2EProp.prefab`     | Swap visual mesh/material to production-ready asset, preserve deploy pipeline behavior     | `AutoTestScenarioBlenderE2EProp`                               |
| Starter build set material pass | `Assets/GeneratedPrefabs/Building_Wall.prefab` (and floor/roof peers) | Improve readability/material polish without breaking snap/collider behavior                | `AutoTestScenarioBuildingVisuals`                              |

## Exit Criteria Per Asset

1. Manifest completed from `Assets/Art/asset_manifest.template.json` with license proof path.
2. LOD0/1/2 and collider strategy declared and verified.
3. Runtime prefab path and integration owner assigned.
4. At least one automated validation path linked (AutoTest scenario or EditMode test).
5. Mid/far camera readability reviewed and signed off.

## Immediate Follow-up

1. Create three concrete manifest entries for the first three integration targets.
2. Perform one visual replacement per target (tree, prop, building material pass).
3. Run `./Tools/validate-pr.ps1 -ProjectRoot . -RunPlayMode` before widening batch scope.

## B01 Premade Import Checklist (Ready to Execute)

Use this checklist exactly as written when dropping premade assets into the repo.

### B01-04 Tree Family A (Premade Primary)

| Item | Required Value |
| --- | --- |
| Manifest | `Assets/Art/Manifests/Batch01/B01-04_tree_family_a_adult.manifest.json` |
| Source provider | Quaternius |
| Destination folder | `Assets/Art/Premade/Quaternius/Environment/Vegetation/TreeFamilyA/Adult/` |
| Required mesh filename | `tree_family_a_adult.fbx` |
| Optional preview filename | `Preview.png` |
| Optional collider mesh suffix | `_COL` |
| Runtime target | `Assets/Resources/Generated/LargeTree/LargeTree.prefab` |

Execution steps:
1. Place `tree_family_a_adult.fbx` in `Assets/Art/Premade/Quaternius/Environment/Vegetation/TreeFamilyA/Adult/`.
2. If provided, place `Preview.png` in the same folder.
3. Run Unity batch generation: `ByteWar.Editor.MCPCommands.GenerateLargeTreeVariationPrefabs`.
4. Validate with `./Tools/validate-pr.ps1 -ProjectRoot . -RunPlayMode`.
5. Update manifest fields: `technical.mesh.triangleCountLOD0/1/2`, `qa.visualReviewed`, `qa.autotestReviewed`.

### B01-11 Deployable Hero Prop A (Premade Primary)

| Item | Required Value |
| --- | --- |
| Manifest | `Assets/Art/Manifests/Batch01/B01-11_deployable_hero_prop_a.manifest.json` |
| Source provider | Kenney |
| Destination folder | `Assets/Art/Premade/Kenney/Environment/Props/DeployableHeroPropA/` |
| Required mesh filename | `hero_prop_a.fbx` |
| Optional preview filename | `Preview.png` |
| Optional collider mesh suffix | `_COL` |
| Runtime target | `Assets/Resources/Generated/BlenderE2EProp/BlenderE2EProp.prefab` |

Execution steps:
1. Place `hero_prop_a.fbx` in `Assets/Art/Premade/Kenney/Environment/Props/DeployableHeroPropA/`.
2. If provided, place `Preview.png` in the same folder.
3. Run Unity batch generation: `ByteWar.Editor.MCPCommands.GenerateBlenderE2EProp`.
4. Validate with `./Tools/validate-pr.ps1 -ProjectRoot . -RunPlayMode`.
5. Update manifest fields: `technical.mesh.triangleCountLOD0/1/2`, `qa.visualReviewed`, `qa.autotestReviewed`.

### Definition of Done (for each imported premade)

1. Correct file path and filename match the checklist.
2. Manifest has real triangle counts and reviewed flags updated.
3. Generation command exits with code `0`.
4. `validate-pr` returns `Errors: 0`.
