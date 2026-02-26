# Changelog

All notable changes to this project are documented here.

## Unreleased

### Developer Asset Deploy Flow

- Restored WoW-style keyboard turning behavior: `A`/`D` now rotate in place (no strafe) when RMB is not held, and the third-person camera yaw follows character turning.
- Corrected RMB movement facing to remain camera-facing during strafe/backpedal (WoW mouse-look semantics) instead of rotating toward movement direction.
- Hardened Core AutoTester turn validation by forcing neutral mouse-button state before turn-only checks and asserting camera-follow yaw change during `A`/`D` turning.
- Added an in-browser preview section for both structure entries and developer assets (thumbnail when `Preview` texture exists, contextual preview card otherwise).
- Developer assets now appear in the browser when they are displayable (collider + `LODGroup`) even if not network-ready; network-ready assets keep server-authoritative placement, and non-network-ready assets fall back to local-only developer placement with explicit status messaging.
- Replaced hold-B radial interaction with a tap-B development asset browser (IMGUI list/tree): LMB selects category/item, item selection closes browser and enters placement mode.
- Placement mode now remains active for repeated world placements; Esc deterministically exits placement mode and closes the browser when open.
- Browser-open state now suppresses build-placement input to prevent accidental world placement clicks while selecting in UI.
- Aligned deploy offer reliability with server authority: catalog/browser now filter to prefabs that satisfy required spawn components (`NetworkObject` on root, plus collider and `LODGroup`).
- Replaced the hold Shift+Tab drag deploy workflow with a hold-B two-step radial deploy flow.
- Added nested radial categories (Structures + Developer Assets) with live 3D preview.
- Added a developer-stage no-cost building policy controlled by the `GameConstants` building-cost switch.
- Added a server-authoritative developer asset placement path in `BuildingController`.
- Fixed a legacy toggle-path regression where `B` could still enter direct structure placement before radial selection (internal build toggle now disabled by default/runtime and in `NetworkPlayer` prefab).
- Improved radial label readability and clipping behavior.
- Fixed movement input suppression while radial and developer placement UI are active.
- Added radial back navigation via Backspace and Back segment.
- Updated radial release behavior to commit the last valid hovered segment for more reliable hold/release selection.
- Corrected radial up/down visual mapping.
- Improved top radial header layout to avoid clipping.
- Normalized radial 3D preview scale across asset sizes.

### Blender MCP Server (Tooling)

- Added `Tools/BlenderMCP/` MCP server to drive headless Blender and export FBX assets into `Assets/Art/...`.
- Added repo guidance for Blender exports alongside the existing Mixamo placeholder pipeline.

### Blender E2E Prop + In-Game Deploy UI

- Added deterministic Blender-generated `BlenderE2EProp` (UVs, LOD0/1/2, collider mesh) plus headless-rendered preview PNG.
- Added Unity import/postprocess + prefab generation to produce a Resources-loadable prefab at `Generated/BlenderE2EProp/BlenderE2EProp`.
- Updated TestScene generation to place the prop near spawn and include `AssetDeployUI` (hold Shift+Tab) to deploy additional copies at runtime.
- Added tests + AutoTester coverage for `BlenderE2EProp` and `AssetDeployUI`; PlayMode, build, and AutoTester pass.

### Asset Deploy UI Browser

- `AssetDeployUI` now shows a folder/subfolder browser (mirrors `Resources` paths) with preview thumbnails loaded from `Resources`.
- Keybind changed to **hold Shift+Tab** to open; releasing either key hides the UI.
- Shift+Tab deploy UI panel is now centered on screen.
- Fixed deploy UI thumbnails not rendering when catalog preview paths included file extensions (e.g., `.png`); preview paths are now normalized for `Resources.Load`.

### Controls + Blender Solidity Fix

- Camera controls updated: LMB hold+drag orbits camera without changing character facing; RMB hold+drag orbits camera and drives character facing (WoW-style).
- Movement updated: without RMB, A/D turns (no strafe) and movement is relative to facing; with RMB, movement is camera-relative with strafe.
- Fixed gameplay input getting stuck suppressed when multiple overlays are involved (e.g., DevConsole + AssetDeployUI), restoring WASD/Space and other keybindings.
- Blender E2E prop collider generation hardened (baked transforms + bounds validation) to prevent players standing inside solid props.

### DevConsole

- Hardened `/dev` DevMode toggle by making `GameConstants.Instance` fall back to transient defaults when the `Resources` asset is missing (prevents AutoTester regressions).

### Valheim-Style Pure Nearest-Pair Snap Algorithm

- **Snap Algorithm Rewrite**: Replaced the 3-tier type-compatible/kind-matching/fallback snap system with Valheim's pure nearest-pair algorithm. `TryAdjacencySnap` now finds the globally closest snap-point pair without type filtering, fixing wall-on-wall centering and foundation-to-wall connectivity bugs.
- **SnapPointMarker Simplified**: `AreCompatible()` marked `[Obsolete]` — no longer used for snap matching. `SnapPointType` enum retained for structural integrity visuals.
- **BuildingPreview Simplified**: Removed `_snapPointTypes` list and `SnapPointTypes` property — types no longer needed for pure nearest-pair matching.
- **BuildingController Updated**: Snap call simplified to use position-only `TryAdjacencySnap` overload.
- **Tests Updated**: Removed `ClassifySnapPoint`/`SnapKind` tests (types removed). Added `WallOnWall_StacksVertically` test verifying walls snap at y=3 instead of centering. Legacy `AreCompatible` tests retained with `#pragma warning disable CS0618`.
- **Build & AutoTest**: 183 EditMode tests pass (3 pre-existing unrelated failures). Build succeeds. AutoTest: all scenarios PASS, exit code 0.

### Valheim-Style Building System Enhancements (Phase 14.5)

- **Tag-Aware Snap Points**: `SnapPointMarker` enhanced with `SnapPointType` enum (WallBottom, WallTop, FloorEdge, RoofPeak, RoofBase, FoundationCorner, FoundationEdge, PoleEnd, BeamEnd, StairsBottom, StairsTop, Generic). `CanSnapTo()` logic ensures type-compatible connections (e.g., WallTop snaps to FloorEdge/RoofBase, not to WallBottom).
- **Structural Integrity System**: `StructuralIntegrity` component propagates support values from ground up. `StructuralMaterial` enum (Wood, Stone, Iron, Thatch) determines max support distance. Pieces beyond max support range collapse. BFS-based `RecalculateAll()` with `OnSupportChanged` event for visual feedback.
- **Comfort System**: `ComfortSystem` calculates area comfort from nearby `BuildingPiece` items within a configurable radius. `ComfortGroup` enum (Fire, Bed, Seating, Decoration, Shelter) prevents stacking same-group bonuses. Broadcasts `OnComfortChanged` event.
- **New Building Piece Types**: Added `DoorFrame`, `Window`, and `HalfWall` to `BuildingPieceType` enum with full support rules, snap points, visuals, colliders, and recipes.
- **22.5-Degree Rotation**: `BuildingController` rotation step reduced from 45° to 22.5° for finer placement control.
- **Placement Restrictions**: `BuildingPiece` extended with `PlacementRestriction` flags (RequireFlat, RequireFoundation, RequireRoof, RequireFireplace, OutdoorOnly, IndoorOnly) for Valheim-style build validation.
- **Prefab Generation**: `PrefabGenerator` updated to generate all 12 piece types with type-specific snap points and register them as NetworkPrefabs.
- **World Persistence**: `WorldPersistence` updated with prefab registry and serialization support for all 12 piece types.
- **Recipe Assets**: `AssetGenerator` creates `BuildingRecipe` ScriptableObjects for DoorFrame, Window, and HalfWall.
- **Tests**: 185+ EditMode tests pass. New tests for snap point type compatibility, structural integrity propagation, comfort calculation, new piece types, and tag-aware snap matching. AutoTester validates 12 registered prefabs.
- **Build & AutoTest**: Build succeeds (95 MB). AutoTest: Core + BuildingVisuals + ConsoleAndFixes all PASS, exit code 0, no exceptions.

### Agent Instruction Streamlining

- **Single Source of Truth**: Consolidated all duplicated rules (warnings-are-defects, 800 LOC, anti-refactor drift, atomic state sync, test coverage) into `copilot-instructions.md`. Each rule now exists in exactly one file.
- **Namespace Fix**: Fixed `SurvivalRPG.Editor.*` → `ByteWar.Editor.*` in build commands (`copilot-instructions.md`). Eliminates guaranteed first-attempt build failures.
- **Removed Phase 1/Phase 2 Ceremony**: Deleted the mandatory Plan-then-Implement two-phase execution model and FMEA (5+ failure modes) requirement from all governance files. Replaced with a single Definition of Done checklist.
- **GAME_PROTOCOL.md**: Reduced from 95 lines (full execution protocol) to 18 lines (quick checklist redirect).
- **AGENTS.md (root)**: Slimmed from ~75 to ~43 lines. Removed duplicated non-negotiables, mode responsibilities, and build commands.
- **agent-workflow.instructions.md**: Removed ~50 lines of duplicated build commands and Self-Validation Checklist (now in copilot-instructions.md). Simplified pre-flight to 3 mandatory + 2 conditional reads.
- **testing.instructions.md**: Removed duplicated 800 LOC rule and Defect Tracking section (both have canonical homes elsewhere).
- **abilities.instructions.md**: Removed duplicated 100% coverage and 800 LOC rules.
- **MASTER_PLAN.md**: Collapsed completed Phase 1 (13 detailed items → 1 summary line). MASTER_PLAN is now a conditional read (only for infrastructure phase tasks).
- **Pre-flight reduction**: Mandatory reading before coding reduced from ~1,350 lines / 20 files to ~430 lines / 5 files (68% reduction). GAME_DESIGN.md and MASTER_PLAN.md are now conditional reads.

### Building Snap — Edge-to-Edge Alignment Fix

- **Kind-Matching Snap Algorithm**: `BuildingSnap.TryAdjacencySnap()` now classifies snap points as Corner, Edge, or Other and prefers kind-matching pairs (edge↔edge, corner↔corner) over mismatched pairs. This prevents floors/roofs from centering on adjacent pieces instead of snapping edge-to-edge.
- **Stairs Snap Points**: Added bottom and top edge-midpoint snap points (6 total, up from 4) so floors/walls snap cleanly to either end.
- **Roof26 Snap Points**: Added 4 base edge-midpoint snap points (10 total, up from 6) for proper edge alignment with walls and floors.
- **Tests**: Updated Roof26/Stairs snap point count tests; added `ClassifySnapPoint` unit tests and a `TryAdjacencySnap_PrefersEdgeToEdge_OverEdgeToCorner` regression test.

### Game Design Document Integration

- **GAME_DESIGN.md**: Created authoritative Game Design Document covering the Core Gameplay Triangle (Wilds/Stronghold/Arena), classless loadout system, hybrid sustain combat model, Stronghold PvE sieges, Eclipse events, Scavenger economy, weight-based inventory, blueprint progression, player economy, anti-griefing escalation matrix, and 10 non-negotiable design invariants.
- **Governance Wiring**: Updated `agent-workflow.instructions.md` pre-flight, `copilot-instructions.md` read order, root `AGENTS.md`, `designer.agent.md` pre-flight, and `GAME_PROTOCOL.md` cross-reference to include GAME_DESIGN.md as a required read.

### Spec-Driven Infrastructure (Phase 6/7/8 partial)

- **architecture.instructions.md**: Comprehensive instruction file covering interface-first design, encapsulation rules, class size limits, magic number prohibition, state management, event communication, registry pattern, performance anti-patterns, naming conventions, and folder structure — all with worked code templates.
- **copilot-instructions.md**: Global Copilot context with governance file read order, quality gates, critical rules, agent escalation table, and build/test commands.
- **agent-workflow.instructions.md**: Decision tree, error recovery protocol (max 3 retries), ambiguity protocol, self-validation checklist, and mandatory instruction-file preflight.
- **Tools/generate-agents-md.ps1**: PowerShell script that auto-generates AGENTS.md files for all script folders by scanning C# files for class names, base classes, interfaces, NetworkBehaviour usage, and RPC methods. Preserves hand-written sections (Invariants, Tests, UX correctness, etc.).
- **Tools/validate-pr.ps1**: PR validation script that checks governance file existence, AGENTS.md freshness, CHANGELOG updates, 800 LOC limit, and public mutable field violations.
- **Building/AGENTS.md**: New AGENTS.md for the Building folder with system overview, key classes, and invariants.
- **Agent Definitions**: Created `architect.agent.md`, `debugger.agent.md`, `reviewer.agent.md`; updated `designer.agent.md` with pre-flight, output format, and economy analysis rules.
- **AGENTS.md Auto-Generated**: All 7 script folder AGENTS.md files regenerated with auto-generated class tables.
- **MASTER_PLAN.md**: Updated Phase 6, 7, 8 progress markers to reflect completed items.

### Gameplay Bug Fixes — Walkability, LOS, Shader Robustness (Phase 14.4)

- **Stairs/Ramp Walkability**: `PrefabGenerator.AddCollider()` now uses MeshCollider for Ramp, Roof26, AngledWall pieces, and compound per-step BoxColliders for Stairs (12 steps, 0.25 unit rise each — proportionate to the 2-unit character model). Player CharacterController `stepOffset` set to 0.4 and `slopeLimit` to 50.
- **Proportionate Stairs**: Redesigned from 4 giant steps (0.75 unit rise) to 12 realistic steps (0.25 unit rise). Each step is ankle-height relative to the character — visually and physically walkable without jumping.
- **Pink Enemy Fix (Root Cause)**: `CharacterGenerator.Mat()` and `EnvironmentGenerator.Mat()` now **skip material re-creation** when the existing `.mat` file already has a valid Standard shader. This prevents `-nographics` builds from overwriting good materials with Diffuse/error fallback (the actual root cause of persistent pink enemies).
- **Line-of-Sight for Enemies**: `EnemyAI.AttackTarget()` performs `Physics.Raycast` from eye position before attacking; blocked by walls/buildings means no damage dealt.
- **Tests**: 4 new EditMode tests (EnemyAI LOS blocked/unblocked, ramp MeshCollider, stairs compound colliders). Total EditMode 156/156 pass.
- **AutoTester**: Scenarios validate 12-step stairs, collider shapes, stepOffset/slopeLimit, enemy shaders.
- **Results**: EditMode 156/156 pass. AutoTest: Core + BuildingVisuals + ConsoleAndFixes all PASS, exit code 0.

### In-Game Console, Camera & Enemy Fixes (Phase 14.3)

- **DevConsole**: New IMGUI overlay (`Assets/Scripts/UI/DevConsole.cs`) toggled with backtick key. Supports `/dev` (toggles DevMode), `/help` (lists commands), and `/clear`. Input to game suppressed while console is open via `PlayerInputHandler.InputSuppressed`.
- **DevMode Runtime Toggle**: `GameConstants.SetDevMode(bool)` enables runtime DevMode toggling. Default remains `false` in code and serialized asset.
- **Pink Enemy Fix**: `CharacterGenerator` and `EnvironmentGenerator` enforce `Standard` shader on all procedural materials instead of URP Lit (which renders pink on Built-in RP). `Standard` shader added to Always Included Shaders in GraphicsSettings.
- **WoW-Style Camera**: `ThirdPersonCamera` orbit now requires RMB hold (was always-on orbit). Added `IsLeftMouseHeld`/`IsRightMouseHeld` properties. `PlayerMovement` faces camera direction only while RMB is held. Cursor locked only during RMB hold.
- **AutoTester**: New `AutoTestScenarioConsole` validates DevConsole presence, `/dev` toggle, `/help` output, enemy shader correctness, and camera RMB properties.
- **Tests**: 7 new EditMode tests (DevConsole commands, DevMode toggle, camera RMB-only orbit, input suppression).
- **Results**: EditMode 149/149 pass. PlayMode 34/34 pass. AutoTest: Core + BuildingVisuals + ConsoleAndFixes, exit code 0, no exceptions.

### Zero-Failure Test Enforcement & Scenario AutoTester

- **DevMode Deterministic**: `GameConstants._devMode` defaults to `false` in both code and serialized asset. Tests that need DevMode explicitly inject via `GameConstants.SetInstanceForTesting()`.
- **Truthful Visual Stamp**: `PrefabGenerator` now detects actual visual mode via `PlayerVisualSetup.DetectActualVisualMode()` after building the hierarchy, instead of trusting the code path. Fixes Mixamo/fallback stamp mismatches.
- **Lazy Visual Setup Init**: `NetworkPlayer.DetectActualVisualMode()` lazily initializes `_visualSetup` via `GetComponent` when `Awake()` hasn't run (EditMode tests).
- **Scenario-Driven AutoTester**: `AutoTester` refactored from monolithic inline checks to an orchestrator pattern. New `IAutoTestScenario` interface, `AutoTesterContext` shared context, and scenario implementations:
  - `AutoTestScenarioCore`: player visual sanity, camera pivot, first-person zoom, grounding, visual grounding, building system presence, animation, movement.
  - `AutoTestScenarioBuildingVisuals`: validates 9 building prefabs registered, stairs 4-step renderers, ramp/roof/angled-wall mesh geometry, collider dimensions.
- **Scenario Selection**: `-autoTestScenario Core -autoTestScenario BuildingVisuals` CLI args for targeted scenario runs. All scenarios run by default.
- **Test Fixture Fix**: `BuildingPlacementSmokeTests.CreateBuildingPrefab` adds `SnapPointMarker` children (required by snap-point assertions).
- **Instruction Enforcement**: Updated `AGENTS.md` and `testing.instructions.md` to explicitly require zero failing tests and feature-specific AutoTester PASS markers.
- **Results**: EditMode 142/142 pass. PlayMode 34/34 pass. AutoTest: Core (10 PASS) + BuildingVisuals (2 PASS), exit code 0, no exceptions.

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

### Type-Specific Building Visuals (Phase 14.2)

- **BuildingMeshBuilder**: New procedural mesh generator (`Assets/Scripts/Editor/BuildingMeshBuilder.cs`) creates accurate geometry for Ramp (wedge that slopes from front ground to back top), Roof26 (gable with ridge along Z-axis), and AngledWall (triangular prism filling wall-to-roof gap).
- **Stairs Visual**: 4 stacked cube steps instead of a single block, each step rising from ground to its height level.
- **PrefabGenerator Dispatch**: `CreateBuildingVisual` dispatcher routes visual creation by piece type. Custom meshes saved as assets under `GeneratedPrefabs/Meshes/`.
- **Collider Updates**: Ramp bounding box updated to 4×3×4 (was 4×0.15×4), Stairs to 4×3×4, Roof26 and AngledWall match their actual peak heights.
- **Tests**: 7 new EditMode tests: `BuildRampMesh_HasValidGeometry`, `BuildRampMesh_BoundsMatchDimensions`, `BuildRampMesh_NormalsArePopulated`, `BuildGableRoofMesh_HasValidGeometry`, `BuildGableRoofMesh_RidgeHeightMatchesPeak`, `BuildAngledWallMesh_HasValidGeometry`, `BuildAngledWallMesh_PeakHeightMatchesSpec`. All pass. Build: Exit 0. AutoTest: 8/8 PASS.

### Valheim-Style Snap Point Overhaul (Phase 14.1)

- **New Building Pieces**: Added Pole, Beam, and AngledWall types (total: 9 piece types).
- **Authored Snap Points**: Replaced legacy calculated `SnapPoint` system with `SnapPointMarker` child GameObjects on prefabs. Each piece type has hand-authored snap point positions enabling Valheim-style nearest-pair connections.
- **Nearest-Pair Snap Logic**: `BuildingSnap.TryAdjacencySnap` finds the closest snap-point pair between preview and placed pieces, enabling multi-story stacking, roof placement, and complex structural connections.
- **Expanded Support Rules**: Walls now supported by Wall (multi-story), Floor supported by Wall (second-story floors), Roof by Wall/Foundation, Pole/Beam/AngledWall by any adjacent piece.
- **BuildingPreview**: Caches snap point local positions from prefabs for efficient runtime matching.
- **PrefabGenerator**: Generates 9 prefabs with `SnapPointMarker` children (Foundation: 8pts, Wall: 8pts, Floor: 8pts, Ramp: 6pts, Roof26: 6pts, Stairs: 4pts, Pole: 2pts, Beam: 2pts, AngledWall: 3pts).
- **AssetGenerator**: Generates recipes for all 9 piece types.
- **Tests**: 40+ EditMode tests covering snap point counts, marker positions, nearest-pair snap, CheckSupport per type, pitch clamping, and serialization. All pass. Build: Exit 0. AutoTest: All 8 PASS markers.

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
