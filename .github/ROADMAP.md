# ByteWar Roadmap to Playable Build

This roadmap outlines the steps required to reach a fully playable, multiplayer proof-of-concept (PoC) build. All AI agents must consult this roadmap to understand the current project state and next steps.

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
- [x] **Building Pieces**: `BuildingPiece` NetworkBehaviour with type enum (Foundation/Wall), snap point metadata, health (damage/repair/destroy), `PlacedByClientId` ownership, `IsSupported` flag.
- [x] **Building Recipes**: `BuildingRecipe` ScriptableObject with `CanAfford()`/`ConsumeResources()`. Foundation costs 4 Wood + 2 Stone; Wall costs 3 Wood + 1 Stone.
- [x] **Prefab Generation**: `PrefabGenerator` creates Foundation/Wall prefabs (box primitives with materials), registers as NetworkPrefabs, and wires `BuildingController` with prefabs + recipes.
- [x] **World Persistence**: `WorldPersistence` server-side JSON save/load at `Application.persistentDataPath/world_save.json`.
- [x] **Tests**: `BuildingSystemTests` (EditMode) — grid snapping, snap points, rotation, stability, recipe cost, JSON round-trip. `BuildingPlacementSmokeTests` (PlayMode) — placement with/without resources, piece spawn. AutoTester validates BuildingController + WorldPersistence + grounding on greybox surfaces. Build: Exit 0. AutoTest: PASS (all 8 checks). No exceptions.

## Phase 14.1: Core Abstractions & Architecture (COMPLETED)

**Goal**: Establish interface-first design, encapsulation, and centralized infrastructure for maintainable, testable code.

- [x] **Interfaces**: Created 7 core interfaces (`IDamageable`, `IInteractable`, `IInventoryHolder`, `ICombatTarget`, `IPersistable`, `IGameState`, `IAbilityExecutor`) and implemented on EnemyAI, ResourceNode, InventoryComponent, AbilitySystemComponent, BuildingPiece, WorldPersistence.
- [x] **Encapsulation**: Converted public fields to `[SerializeField]` private + read-only properties across InventoryComponent, AbilitySystemComponent, EquipmentComponent, ScriptableObjects, CraftingStation, SurvivalStats. Added `internal` setters with `InternalsVisibleTo` for test assemblies.
- [x] **PlayerInteraction Refactor**: Rewrote to use `IDamageable`/`IInteractable` interfaces instead of concrete `GetComponent<EnemyAI>()` and `GetComponent<ResourceNode>()` casts.
- [x] **Architectural Extraction**: Extracted `PlayerMovement` (movement/gravity/jumping/abilities), `PlayerVisualSetup` (animator repair/visual grounding/visual mode detection), `BuildingPreview` (preview ghost management/tinting), `BuildingSnap` (grid snap/adjacency snap/support checks) from their parent classes.
- [x] **GameConstants ScriptableObject**: Centralized magic numbers (move speed 6, turn speed 14, gravity -18, jump force 6, melee/gather damage 10, fireball fallback 25, interaction range 100, target frame rate 60) into a runtime-loadable `Resources/GameConstants` asset with static fallback accessors.
- [x] **GameStateMachine**: Lightweight state machine (Initializing → MainMenu → Connecting → Loading → Playing → Paused → Disconnected) hosted by `GameManager` with `OnStateChanged` events.
- [x] **GameEventBus**: Type-safe event channels (`GameEvent<T>`) with auto-cleanup on scene unload. 6 channels: EnemyDied, EnemyTargeted, ItemAdded, ItemRemoved, BuildingPlaced, GameStateChanged. Migrated EnemyAI and EnemyTargetTracker from static events.
- [x] **PlayerRegistry**: Static registry replacing `FindGameObjectsWithTag("Player")`. Maintained by `NetworkPlayer.OnNetworkSpawn/Despawn`. Provides `GetNearest()` utility. Integrated into EnemyAI targeting.
- [x] **Tests**: All 62 EditMode + 18 PlayMode tests pass. Build: Exit 0. AutoTest: 8/8 PASS. No exceptions.

## Phase 15: Visuals & Audio Polish (COMPLETED)

**Goal**: Upgrade the game from a prototype to a polished vertical slice.

- [x] **VFX System**: `VFXManager` (NetworkBehaviour singleton) with 6 prefab slots (FireballMuzzle, FireballImpact, GatherHit, ResourceDeath, BuildingPlace, ItemPickup). Server calls `PlayEffect()`, all clients receive `PlayEffectClientRpc`. `VFXPrefabGenerator` builds procedural particle prefabs at build time. Wired into FireballProjectile, AbilitySystemComponent, ResourceNode, BuildingController, PlayerInteraction.
- [x] **Audio System**: `AudioManager` (NetworkBehaviour singleton) with typed `SFXType` enum, synthesized AudioClips generated by `AudioClipGenerator`. `FootstepController` attached to NetworkPlayer drives footstep sounds at runtime, driven by CharacterController velocity. `AudioSource` wired on NetworkPlayer prefab.
- [x] **Post-Processing**: Editor-only `PostProcessingSetup` (static class with `ByteWar/Setup Post-Processing` menu item) creates a `PostProcessProfile` with Bloom (intensity 0.6, threshold 0.9) and Color Grading (temperature -5, saturation +10, contrast +15). Adds a global `PostProcessVolume` and `PostProcessLayer` with TAA on the Main Camera. `SceneGenerator` calls this on every regeneration.
- [x] **Tests**: `VFXAudioSystemTests` (EditMode, 8 tests) — VFXType/SFXType enum coverage, VFXManager/AudioManager lifecycle, singleton pattern, client-only `PlayEffectLocal`. `VFXSmokeTests` (PlayMode, 2 tests) — VFXManager instantiation in scene. `GeneratedPrefabIntegrityTests` extended with `NetworkPlayerPrefab_HasAudioSource` and `NetworkPlayerPrefab_HasFootstepController`.
- [x] **AutoTester**: 4 new PASS checks — VFXManager present, AudioManager present, Player has FootstepController, Player has AudioSource.
- [x] **Verification**: EditMode 72/72 PASS. PlayMode 18/18 PASS. Build: 95 MB, Exit 0. AutoTest: 12/12 PASS. No exceptions.

## Phase 16: Multiplayer Hardening & Network Polish (NEXT)

**Goal**: Ensure the game is rock-solid in a real 2-player networked session.

- [ ] **Lag Compensation**: Client-side prediction for player movement.
- [ ] **Network Diagnostics**: Real-time ping/latency display, packet loss indicator.
- [ ] **Server Browser**: Simple lobby system with host/join UI.
- [ ] **Disconnect Handling**: Graceful reconnect flow, state cleanup on player leave.
- [ ] **Load Testing**: AutoTester extended with 2-client simulation.
