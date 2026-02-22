# SurvivalRPG Roadmap to Playable Build

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

## Phase 13.1: Player Feel Hotfix (NEXT)

**Goal**: Lock down the core "feels like WoW" fundamentals so we don't regress into camera/grounding/input issues.

- [x] **Camera Pivot Sanity**: Prevent stacked vertical offsets (CameraTarget + pivotHeight). AutoTester validates pivot delta is within a sane band.
- [x] **Grounding Sanity**: Deterministic CharacterController config + visual grounding. AutoTester validates capsule bottom is within epsilon of sampled terrain.
- [x] **Action Bar Correctness**: Ability keys only trigger cast animation when a cast request actually executes (no empty-slot cast animation).
- [ ] **Manual Feel Pass**: Confirm camera framing is WoW-default and character feels grounded in Editor play (no obvious hovering).

## Phase 14: Building System & Persistence

**Goal**: Allow players to build structures and save their progress.

- [ ] **Building Placement**: Finalize the logic for snapping and placing building pieces (foundations, walls) using the `BuildingController`.
- [ ] **World Persistence**: Implement a save/load system for the host to save the state of the world (buildings, inventory, player stats) to disk.

## Phase 15: Visuals & Audio Polish

**Goal**: Upgrade the game from a prototype to a polished vertical slice.

- [ ] **VFX**: Add particle effects for spells, gathering, and building.
- [ ] **SFX**: Add sound effects for footsteps, UI clicks, combat, and ambient environment sounds.
- [ ] **Lighting & Post-Processing**: Refine the procedural skybox, fog, and add post-processing volumes (Bloom, Color Grading).
