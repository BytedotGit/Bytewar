# Changelog

All notable changes to this project are documented here.

## Unreleased

### Project Rename: SurvivalRPG → ByteWar (Phase 1)

- **Rename**: Project renamed from SurvivalRPG to ByteWar across all code, assets, and documentation.
- **Company**: Updated company name from DefaultCompany to BytedotGit in ProjectSettings.
- **Assemblies**: All 4 assembly definitions renamed (ByteWar, ByteWar.Editor, ByteWar.Tests.EditMode, ByteWar.Tests.PlayMode).
- **Namespaces**: Global find-replace of `namespace SurvivalRPG` → `namespace ByteWar` across all 83 C# files.
- **Menus**: All `CreateAssetMenu` and `MenuItem` strings updated to use `ByteWar/` prefix.
- **Build**: Output path changed to `Builds/Windows/ByteWar.exe`.
- **MCP**: Assembly-qualified names in `Tools/UnityMCP/index.js` updated.
- **Docs**: ROADMAP, AGENTS, testing instructions, and Mixamo README updated.
- **Cleanup**: Stale build artifacts deleted.

### Greybox World & Valheim-Like Building (Phase 14 - Enhanced)

- **Greybox World**: SceneGenerator now produces a flat 200×200m greybox ground plane (primitives with colliders + materials) instead of a procedural Terrain. Includes raised platform, ramp, obstacle walls, and 20 scattered rock props.
- **Terrain-Independent Grounding**: `NetworkPlayer` spawn/grounding and `AutoTester` sanity checks use raycasting instead of `Terrain.SampleHeight`, supporting any surface geometry.
- **Valheim-Like Building Loop** — `BuildingController` rewritten:
  - B-key toggle for build mode (with camera zoom suppression via `ThirdPersonCamera.SuppressZoom`).
  - Scroll wheel rotation (configurable 45° step).
  - Number keys (1-9) for recipe selection; spell casting gated while build mode is active.
  - Edge-to-edge adjacency snapping via `SnapPoint` metadata on `BuildingPiece`.
  - Left-click place, middle-click remove (with resource refund), right-click repair.
  - Support/stability: Foundations place freely; Walls require adjacency snap to a Foundation.
  - Preview uses `MaterialPropertyBlock` tinting (green/valid, red/invalid); preview has all networking and physics stripped.
- **BuildingPiece** enhanced: health system (damage/repair/destroy), `PlacedByClientId` ownership, `IsSupported` flag, Valheim-like snap points (target pivot positions with auto-orientation).
- **AutoTester**: grounding epsilon widened for greybox flat surfaces; first-person camera re-synced between frames to avoid timing drift.
- **Tests**: EditMode and PlayMode building tests updated for new snap semantics, rotation, stability, and runtime-created prefab hash handling.
- Camera: stable first-person zoom mode (explicit mode w/ hysteresis; collision disabled in first-person; avoids LookAt singularity).
- Visuals: runtime one-time VisualRoot hover correction after spawn (visual-only; no CharacterController physics changes) and camera pivot refresh.
- AutoTester: added PASS-marked first-person zoom sanity + visual grounding sanity (visual bottom vs terrain), and fails fast on regressions.
- Tests: added EditMode + PlayMode regression coverage for first-person and visual grounding.

## v0.1.1 - 2026-02-22

- AutoTester: reduce flaky UTP port bind failures via UDP port probing + small backoff between retries; temporarily suppress known NGO shutdown warning spam during retries (counted and reported).
- Build pipeline: generator/build logs standardized with a greppable `[BuildGen]` prefix (BuildScript + generators).

## v0.1.0 - 2026-02-22

- Added execution protocol + per-folder AGENTS guidance + Git LFS + Unity gitignore.
- Phase 13.1 feel hotfix: camera pivot de-stacking, deterministic grounding, and action-bar animation gating.
- AutoTester expanded to validate camera pivot sanity + grounding sanity in builds.
- AutoTester hardened: deterministic host startup for `-autoTest` (HUD auto-host disabled, random high port + retries) and animation sanity (fails fast on missing controller / T-pose regressions).
