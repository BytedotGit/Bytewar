# ByteWar Roadmap

This roadmap tracks all project progress. It is divided into two tracks:

- **Gameplay Track** (Phases 1–15): Feature implementation for the playable build. See completed phases below.
- **Infrastructure Track** (Phases I–IX): Architecture, tooling, CI/CD, and agent-autonomy overhaul. See [MASTER_PLAN.md](MASTER_PLAN.md) for the full breakdown and current status.

All AI agents must consult this roadmap AND the MASTER_PLAN to understand project state and next steps.

---

## Current Focus

**Infrastructure**: MASTER_PLAN Phase 2 (Core Abstractions & Interfaces) — interfaces defined, classes extracted; encapsulation + tests remaining. Phases 6/7/8 partially complete (spec files, validation tools, agent definitions created).

**Gameplay**: Phase 15 (Visuals & Audio Polish) — not started.

**Tooling**: Blender MCP server for headless asset export into `Assets/Art/` (in progress).

---

## Phase 1-11: Core Systems, Generation & Testing (COMPLETED)

- [x] Agent Customization & Workspace Setup
- [x] Project Architecture & Folder Structure
- [x] Core Systems Implementation (NGO, Attributes, Abilities)
- [x] Inventory & Gear Progression
- [x] Crafting System
- [x] Advanced Talents (Mage)
- [x] PlayMode Smoke Tests & Networking Validation
- [x] Input System & Player Controller (WoW-style movement)
- [x] UI & HUD (Vitals, Action Bar, Inventory, Crafting, Network Diagnostics)
- [x] Procedural Asset Generation (Terrain, Characters, Environment, Prefabs, Scenes)
- [x] MCP Server Integration for Unity Automation
- [x] Playable Build & Polish (Standalone Windows client, Auto-testing loop)

## Phase 12: Mixamo Character Integration & Animations (COMPLETED)

**Goal**: Replace procedural capsule models with high-quality Mixamo rigged character and animations.

- [x] Downloaded Mixamo Y Bot character (Character.fbx) and 5 animations (Idle, Walking, Running, Jump, Attack) via browser automation.
- [x] Placed FBX files in `Assets/Art/Characters/Mixamo/`.
- [x] Updated `MixamoProcessor.cs` — two-pass Humanoid rig import (Character → CreateFromThisModel, animations → CopyFromOther).
- [x] Updated `AnimatorGenerator.cs` — reliable FBX sub-asset clip lookup + Blend Tree for locomotion.
- [x] Updated `MCPCommands.cs` — MixamoProcessor runs before AnimatorGenerator in the generation pipeline.
- [x] Ran `Tools/MCP/Generate All` — all 5 animation clips found and embedded into PlayerAnimatorController.
- [x] Built Windows client (101 MB) — all Mixamo FBX assets included, build succeeded.
- [x] Auto-test PASSED: player spawns, moves on terrain, no exceptions.

## Phase 13: Gameplay Loop & Combat Polish (COMPLETED)

**Goal**: Ensure the core gameplay loop (combat, gathering, crafting) feels responsive and fun in a networked environment.

- [x] **Combat Responsiveness**: `FireballProjectile` networked, `FireballAbility` server-spawns and applies `GameplayEffect` damage to `EnemyAI`. Melee click-attack wired via `AttackEnemyServerRpc` in `PlayerInteraction`.
- [x] **Enemy AI**: `EnemyAI` detects player, navigates terrain (Y-snap), attacks on cooldown, fires `OnEnemyDied` event on death and despawns. `EnemySpawner` tracks deaths and maintains pool.
- [x] **Resource Gathering**: `PlayerInteraction.GatherResourceServerRpc` calls `ResourceNode.TakeDamage` which drops items into `InventoryComponent` via `AddItem`. ClientRpc notifications sent to UI.
- [x] **Animations**: `NetworkPlayer` drives `Speed`, `IsGrounded`, `Jump`, `Attack` on `Animator`/`ClientNetworkAnimator`. `PlayerInteraction` fires `Attack` and `Gather` triggers on interact.
- [x] **Enemy Target UI**: `EnemyTargetTracker` (static event bus) + `EnemyHealthUI` display targeted enemy health bar, auto-hides on death.
- [x] **Inventory UI**: `InventoryUI` upgraded to event-driven (`OnItemAdded`/`OnItemRemoved`) instead of polling every frame.
- [x] **Tests**: `EnemyTargetTrackerTests` (EditMode), `MeleeAttackSmokeTests` (PlayMode), plus existing combat/resource smoke tests. Build: ✅ Exit 0. AutoTest: ✅ PASS. No exceptions.
- [x] **Input Hardening**: Enforced `Active Input Handling = Both` (`activeInputHandler: 2`) via editor-load enforcer + build preprocessor; AutoTester now fails fast if `ENABLE_INPUT_SYSTEM` isn’t defined or devices/actions aren’t active.

## Phase 13.1: Player Feel Hotfix (COMPLETED)

**Goal**: Lock down the core "feels like WoW" fundamentals so we don't regress into camera/grounding/input issues.

- [x] **Camera Pivot Sanity**: Prevent stacked vertical offsets (CameraTarget + pivotHeight). AutoTester validates pivot delta is within a sane band.
- [x] **Capsule Grounding Sanity**: Deterministic CharacterController config. AutoTester validates capsule bottom is within epsilon of sampled terrain.
- [x] **Visual Grounding Sanity**: Runtime one-time VisualRoot correction removes obvious hovering. AutoTester validates visual bottom is within epsilon of sampled terrain.
- [x] **First-Person Zoom Stability**: ThirdPersonCamera uses explicit first-person mode (no collision, stable yaw/pitch rotation, renderer hiding). AutoTester validates camera pivot lock + renderers hidden.
- [x] **Action Bar Correctness**: Ability keys only trigger cast animation when a cast request actually executes (no empty-slot cast animation).
- [x] **Mixamo Visual Proofing**: Generated `NetworkPlayer` stamps visual mode (Mixamo vs fallback). AutoTester asserts stamp matches runtime visuals and logs the proven mode.
- [x] **Mixamo Animation Proofing**: AutoTester asserts the player has a valid AnimatorController and that a humanoid bone rotates during simulated movement (catches T-pose/retarget regressions).
- [x] **Manual Feel Pass**: Visual grounding fixed — humanoid bone-based foot sampling replaces unreliable RendererBounds. AutoTester confirms visual bottom within 0.12 of ground. Confirm camera framing is WoW-default in Editor play.

## Phase 14: Building System & Persistence (COMPLETED)

**Goal**: Allow players to build structures and save their progress.

- [x] **Greybox World**: Replaced procedural `Terrain` with greybox primitive colliders (flat ground plane, raised platform, ramp, walls, scattered rocks). Terrain-independent spawn/grounding in `NetworkPlayer` and `AutoTester` (raycast-based).
- [x] **Valheim-Like Building Loop**: `BuildingController` rewritten with Valheim-inspired mechanics:
  - **Build Mode Toggle** (B key) with camera zoom suppression.
  - **Scroll Wheel Rotation** (configurable step, default 45°).
  - **Number Keys (1-9)** select building recipes; spell casting gated while in build mode.
  - **Edge-to-Edge Adjacency Snapping** via `SnapPoint` metadata on `BuildingPiece` (target pivot positions, auto-orient walls perpendicular to foundation edges).
  - **Left-Click Place**, **Middle-Click Remove** (with resource refund), **Right-Click Repair**.
  - **Support/Stability**: Foundations place freely; Walls require adjacency to a Foundation.
  - **Preview**: `MaterialPropertyBlock` tinting (green valid, red invalid); stripped NetworkObject/physics from preview.
- [x] **Building Pieces**: `BuildingPiece` NetworkBehaviour with 9-type enum (Foundation, Wall, Floor, Ramp, Roof26, Stairs, Pole, Beam, AngledWall), health (damage/repair/destroy), `PlacedByClientId` ownership, `IsSupported` flag.
- [x] **Valheim-Style Snap Points**: `SnapPointMarker` child GameObjects on every building prefab define authored snap positions. Nearest-pair matching in `BuildingSnap.TryAdjacencySnap` aligns any snap point to any other, enabling multi-story, roof, and complex structural connections.
- [x] **Expanded Support Rules**: Foundations place freely. Walls supported by Foundation/Floor/Wall (multi-story stacking). Floor by Foundation/Wall/Floor. Ramp/Stairs by Foundation/Floor/Wall. Roof by Wall/Floor/Foundation. Pole/Beam/AngledWall by any adjacent piece.
- [x] **Building Recipes**: `BuildingRecipe` ScriptableObject with `CanAfford()`/`ConsumeResources()`. 9 recipes covering all piece types.
- [x] **Prefab Generation**: `PrefabGenerator` creates 9 building piece prefabs with authored `SnapPointMarker` children, registers as NetworkPrefabs, and wires `BuildingController` + `WorldPersistence` with all prefabs + recipes.
- [x] **World Persistence**: `WorldPersistence` server-side JSON save/load with full 3-axis rotation (RotX, RotY, RotZ) for all 9 piece types.
- [x] **Tests**: 40+ tests covering snap point counts per type, `SnapPointMarker` world/local positions, grid snapping, `TryAdjacencySnap` nearest-pair, `CheckSupport` per type, recipe cost, pitch clamping, JSON serialization. AutoTester validates BuildingController (9 recipes) + WorldPersistence + grounding. Build: Exit 0. AutoTest: PASS (all 8 checks). No exceptions.
- [x] **Type-Specific Building Visuals**: `BuildingMeshBuilder` generates procedural meshes for Ramp (wedge), Roof26 (gable), and AngledWall (triangular prism). Stairs use 4 stacked cube steps. Foundation/Wall/Floor/Pole/Beam retain cube visuals. `PrefabGenerator` dispatches visual creation by piece type. Collider bounding boxes updated to match actual geometry. 7 new EditMode tests verify mesh geometry, bounds, and normals.
- [x] **Zero-Failure Test Enforcement & Scenario AutoTester**: All tests (EditMode + PlayMode) enforced to pass with zero failures. DevMode defaults to false everywhere (code + asset); tests explicitly inject DevMode state. Visual mode stamp made truthful via runtime detection. AutoTester refactored into scenario-driven architecture (`IAutoTestScenario`, `AutoTestScenarioCore`, `AutoTestScenarioBuildingVisuals`); each feature change requires its own scenario with PASS markers. `BuildingVisuals` scenario validates 9 prefab registrations, stairs steps, ramp/roof/angled-wall mesh geometry, and collider dimensions. EditMode: 142/142 pass. PlayMode: 34/34 pass. AutoTest: Core (10 PASS) + BuildingVisuals (2 PASS), exit 0.

## Phase 14.3: In-Game Console, Camera & Enemy Fixes (COMPLETED)

**Goal**: Add dev tooling accessible at runtime, fix visual regressions, and restore WoW-style camera controls.

- [x] **In-Game DevConsole**: IMGUI overlay toggled with backtick (\`). Supports `/dev` command to toggle DevMode at runtime (free resources, no build costs). `/help` lists available commands. Input to game is suppressed while console is open (`PlayerInputHandler.InputSuppressed`).
- [x] **DevMode Runtime Toggle**: `GameConstants.SetDevMode(bool)` allows runtime toggling. Default remains `false`.
- [x] **Pink Enemy Fix**: `CharacterGenerator` and `EnvironmentGenerator` enforce `Standard` shader on all procedural materials (was using URP Lit which produces pink on BIRP). `Standard` shader added to `Always Included Shaders` in GraphicsSettings.
- [x] **WoW-Style Camera Restoration**: `ThirdPersonCamera` orbit now requires RMB hold (was always-on). Added `IsLeftMouseHeld`/`IsRightMouseHeld` properties. `PlayerMovement` updated to face camera direction only when RMB is held. Cursor lock managed per-frame based on RMB state.
- [x] **AutoTester Scenario**: `AutoTestScenarioConsole` validates DevConsole presence, `/dev` toggle, `/help` output, enemy shader correctness, and camera RMB properties.
- [x] **Tests**: EditMode tests for DevConsole commands, DevMode toggle, camera RMB-only orbit, and input suppression. All pass.
- [x] **Results**: EditMode 149/149 pass. PlayMode 34/34 pass. AutoTest: Core (10 PASS) + BuildingVisuals (2 PASS) + ConsoleAndFixes (5 PASS), exit code 0, no exceptions.

## Phase 14.4: Gameplay Bug Fixes — Walkability, LOS, Shader Robustness (COMPLETED)

**Goal**: Fix stairs/ramp walkability, prevent enemies attacking through walls, and permanently fix pink enemy materials.

- [x] **Proportionate Stairs**: Redesigned from 4 giant steps (0.75 unit rise) to 12 realistic steps (0.25 unit rise each). Each step is ankle-height relative to the 2-unit character model. Both visual cubes and per-step BoxColliders updated.
- [x] **Pink Enemy Root Cause Fix**: `CharacterGenerator.Mat()` and `EnvironmentGenerator.Mat()` skip material re-creation when the existing `.mat` file already has a valid Standard shader. This prevents `-nographics` builds from overwriting correct materials with Diffuse/error fallback.
- [x] **LOS for Enemy Attacks**: `EnemyAI.AttackTarget()` performs `Physics.Raycast` from eye position; walls/buildings block damage.
- [x] **Ramp/Roof Colliders**: MeshCollider (convex) for Ramp, Roof26, AngledWall. Player `stepOffset=0.4`, `slopeLimit=50`.
- [x] **Tests**: 4 new EditMode tests (LOS, MeshCollider, compound colliders). Total: EditMode 156/156 pass.
- [x] **AutoTester**: Validates 12-step stairs, collider shapes, stepOffset/slopeLimit, enemy shaders. All PASS, exit code 0.

## Phase 14.5: Valheim-Style Building Enhancements (COMPLETED)

**Goal**: Replicate Valheim's building mechanics — pure nearest-pair snap points, structural integrity, comfort system, new piece types, and fine-grained placement.

- [x] **Pure Nearest-Pair Snap Algorithm**: Rewrote `BuildingSnap.TryAdjacencySnap` to match Valheim's actual snap behavior — finds the globally closest snap-point pair with no type filtering or priority tiers. Fixes wall-on-wall centering and cross-piece connectivity bugs. `SnapPointMarker.AreCompatible()` deprecated.
- [x] **Structural Integrity System**: `StructuralIntegrity` component with BFS support propagation. `StructuralMaterial` enum (Wood/Stone/Iron/Thatch) controls max support distance. Unsupported pieces collapse. `OnSupportChanged` event for visual feedback.
- [x] **Comfort System**: `ComfortSystem` calculates area comfort from nearby building pieces within configurable radius. `ComfortGroup` enum prevents same-group stacking. `OnComfortChanged` event.
- [x] **New Piece Types**: Added DoorFrame, Window, HalfWall to `BuildingPieceType` (12 total). Full support rules, snap points, visuals, colliders, and recipes for each.
- [x] **22.5° Rotation**: `BuildingController` rotation step halved from 45° to 22.5° for finer placement.
- [x] **Placement Restrictions**: `BuildingPiece` extended with `PlacementRestriction` flags (RequireFlat, RequireFoundation, RequireRoof, RequireFireplace, OutdoorOnly, IndoorOnly).
- [x] **Prefab & Persistence**: `PrefabGenerator` and `WorldPersistence` updated for all 12 types. `AssetGenerator` creates new recipes.
- [x] **Tests & Validation**: 183+ EditMode tests pass (incl. wall-on-wall stacking test). AutoTester validates 12 registered prefabs. Build: 95 MB, exit 0. AutoTest: Core + BuildingVisuals + ConsoleAndFixes all PASS, no exceptions.

## Phase 15: Visuals & Audio Polish

**Goal**: Upgrade the game from a prototype to a polished vertical slice.

- [ ] **VFX**: Add particle effects for spells, gathering, and building.
- [ ] **SFX**: Add sound effects for footsteps, UI clicks, combat, and ambient environment sounds.
- [ ] **Lighting & Post-Processing**: Refine the procedural skybox, fog, and add post-processing volumes (Bloom, Color Grading).
