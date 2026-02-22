# Plan: Production-Grade Agent-Driven Unity Workspace for ByteWar

## TL;DR

Rename the project from SurvivalRPG → ByteWar, then overhaul the workspace to production-grade standards: ECS (DOTS) for simulation, dedicated server build, architectural abstractions (interfaces, state machines, event bus, config system), robust agent-autonomy infrastructure (CI/CD, guardrails, conventions, specialized agents), and comprehensive instruction files with worked ECS templates — all designed so AI agents build the game autonomously with minimal human input.

---

## Master TODO List

Legend: `[ ]` = not started, `[~]` = in progress, `[x]` = done

---

### PHASE 1: Project Rename (SurvivalRPG → ByteWar)

> **Precondition**: None. Do this first — everything else references the project name.
> **Risk mitigation**: This touches nearly every file. Run full test suite before AND after. Commit the rename as a single atomic commit.

- [ ] **1.1** Update `ProjectSettings/ProjectSettings.asset`:
  - `productName: SurvivalRPG` → `productName: ByteWar`
  - `companyName: DefaultCompany` → `companyName: BytedotGit`
  - `metroPackageName` and `metroApplicationDescription` → `ByteWar`
- [ ] **1.2** Rename assembly definition files (+ their `.meta` files):
  - `SurvivalRPG.asmdef` → `ByteWar.asmdef` (update `name`, `rootNamespace`)
  - `SurvivalRPG.Editor.asmdef` → `ByteWar.Editor.asmdef` (update `name`, `rootNamespace`, refs)
  - `SurvivalRPG.Tests.EditMode.asmdef` → `ByteWar.Tests.EditMode.asmdef` (update `name`, `rootNamespace`, refs)
  - `SurvivalRPG.Tests.PlayMode.asmdef` → `ByteWar.Tests.PlayMode.asmdef` (update `name`, `rootNamespace`, refs)
- [ ] **1.3** Global find-replace in all `.cs` files:
  - `namespace SurvivalRPG` → `namespace ByteWar`
  - `using SurvivalRPG` → `using ByteWar`
  - `SurvivalRPG.` (FQN refs) → `ByteWar.`
  - `InternalsVisibleTo("SurvivalRPG.` → `InternalsVisibleTo("ByteWar.`
- [ ] **1.4** Update `CreateAssetMenu` `menuName` strings in 7 ScriptableObjects:
  - `"SurvivalRPG/..."` → `"ByteWar/..."`
- [ ] **1.5** Update `MenuItem` strings in 10+ editor scripts:
  - `"SurvivalRPG/..."` → `"ByteWar/..."`
- [ ] **1.6** Update build output path in `BuildScript.cs`:
  - `"Builds/Windows/SurvivalRPG.exe"` → `"Builds/Windows/ByteWar.exe"`
- [ ] **1.7** Update `Tools/UnityMCP/index.js`:
  - All assembly-qualified names: `SurvivalRPG.Editor.MCPCommands, SurvivalRPG.Editor` → `ByteWar.Editor.MCPCommands, ByteWar.Editor`
- [ ] **1.8** Rename root project files (+ update content):
  - `SurvivalRPG.slnx` → `ByteWar.slnx` (update all project refs inside)
  - `SurvivalRPG.csproj` → `ByteWar.csproj` (update RootNamespace, AssemblyName, asmdef path)
  - `SurvivalRPG.Editor.csproj` → `ByteWar.Editor.csproj` (same + ProjectReference)
  - `SurvivalRPG.Tests.EditMode.csproj` → `ByteWar.Tests.EditMode.csproj`
  - `SurvivalRPG.Tests.PlayMode.csproj` → `ByteWar.Tests.PlayMode.csproj`
- [ ] **1.9** Update all documentation references:
  - `AGENTS.md`: `SurvivalRPG.exe` → `ByteWar.exe`
  - `.github/ROADMAP.md`: title and any refs
  - `.github/instructions/testing.instructions.md`: exe path + Player.log path (`DefaultCompany\SurvivalRPG` → `BytedotGit\ByteWar`)
  - `Tools/UnityMCP/README.md`: project name + paths
  - `Assets/Art/Characters/Mixamo/README.md`: menu name ref
- [ ] **1.10** Delete stale build artifacts: `Builds/Windows/`, `build_log.txt`, `TestResults_EditMode.xml`, `dotnet_build.txt`
- [ ] **1.11** Update `CHANGELOG.md` with rename entry
- [ ] **1.12** Verification: compile (zero warnings) → EditMode tests → PlayMode tests → build client → AutoTest → Player.log check (new company/product path)
- [ ] **1.13** Single atomic git commit for the rename

---

### PHASE 2: Core Abstractions & Interfaces

> **Precondition**: Phase 1 complete (all refs are ByteWar).
> **Risk mitigation (from Further Considerations)**: Splitting `NetworkPlayer` touches the most critical runtime class. Run all existing tests before AND after the split. If any test breaks, the split is wrong — revert and re-approach.

- [ ] **2.1** Create `Assets/Scripts/Core/Interfaces/` folder
- [ ] **2.2** Define `IDamageable.cs` — `void TakeDamage(float amount, ulong instigatorClientId)`
- [ ] **2.3** Define `IInteractable.cs` — `bool CanInteract(ulong clientId)`, `void Interact(ulong clientId)`
- [ ] **2.4** Define `IInventoryHolder.cs` — `HasItem()`, `AddItem()`, `RemoveItem()`
- [ ] **2.5** Define `ICombatTarget.cs` — `AttributeSet Attributes`, `bool IsAlive`
- [ ] **2.6** Define `IPersistable.cs` — `string Serialize()`, `void Deserialize(string)`
- [ ] **2.7** Define `IGameState.cs` — `void Enter()`, `void Exit()`, `void Tick()`
- [ ] **2.8** Implement interfaces on existing classes:
  - `EnemyAI` → `IDamageable`, `ICombatTarget`
  - `ResourceNode` → `IDamageable`, `IInteractable`
  - `InventoryComponent` → `IInventoryHolder`
  - `AbilitySystemComponent` → `IAbilityExecutor` (new interface)
  - `WorldPersistence` / `BuildingPiece` → `IPersistable`
- [ ] **2.9** Refactor `PlayerInteraction.PerformRaycastInteraction()` to use `IInteractable`/`IDamageable` instead of concrete type checks
- [ ] **2.10** Encapsulate public mutable fields (ALL ScriptableObjects + components):
  - `Ability`, `Item`, `Talent`, `GameplayEffect` → `[SerializeField] private` + read-only getters
  - `InventoryComponent.Items` → `IReadOnlyList<Item>`
  - `AbilitySystemComponent.LearnedAbilities/Talents` → `IReadOnlyList<>`
  - `AbilitySystemComponent.CooldownReductions/ManaCostReductions` → read-only accessor methods
  - `EquipmentComponent.EquippedWeapon/Armor` → properties with server-only setters
  - All UI public fields → `[SerializeField] private`
- [ ] **2.11** Extract `PlayerMovement.cs` from `NetworkPlayer.cs`:
  - Move: movement constants, `HandleMovement()`, gravity, jump, velocity
  - NetworkPlayer keeps: lifecycle, camera, spawn, OnNetworkSpawn coordination
- [ ] **2.12** Extract `PlayerVisualSetup.cs` from `NetworkPlayer.cs`:
  - Move: visual grounding, animator repair, visual mode detection, VisualDiagnostics
- [ ] **2.13** Extract `BuildingPreview.cs` from `BuildingController.cs`:
  - Move: preview rendering, validity tinting, rotation
- [ ] **2.14** Extract `BuildingSnap.cs` from `BuildingController.cs`:
  - Move: grid snapping, adjacency snapping logic
- [ ] **2.15** Create `GameConstants.cs` ScriptableObject:
  - Absorb: MoveSpeed, Gravity, JumpForce, TurnSpeed (from NetworkPlayer)
  - Absorb: melee damage `10f` (from PlayerInteraction)
  - Absorb: fireball fallback damage `25f` (from FireballProjectile)
  - Absorb: gather damage (from PlayerInteraction)
  - Absorb: frame rate cap (from GameManager)
  - Absorb: all AutoTester thresholds
- [ ] **2.16** Update `AssetGenerator` to generate `GameConstants` ScriptableObject
- [ ] **2.17** Create `GameStateMachine.cs`:
  - States: Initializing → MainMenu → Connecting → Loading → Playing → Paused → Disconnected
  - Host in `GameManager` (replace bare singleton with state machine host)
- [ ] **2.18** Create `GameEventBus.cs` with `GameEvent<T>`:
  - Channels: `EnemyDied`, `EnemyTargeted`, `ItemAdded`, `ItemRemoved`, `BuildingPlaced`, `GameStateChanged`
  - Auto-cleanup on scene unload
- [ ] **2.19** Migrate static events to GameEventBus:
  - `EnemyAI.OnEnemyDied` → `GameEventBus.EnemyDied`
  - `EnemyTargetTracker.OnEnemyTargeted` → `GameEventBus.EnemyTargeted`
- [ ] **2.20** Create `PlayerRegistry.cs` (static `HashSet<Transform>`):
  - Maintained by `NetworkPlayer.OnNetworkSpawn/Despawn`
  - Replace `EnemyAI.FindGameObjectsWithTag("Player")` with `PlayerRegistry.GetNearest()`
- [ ] **2.21** Fix per-frame anti-patterns:
  - `BuildingController`: cache `FindObjectsByType<BuildingPiece>()`, invalidate on place/destroy events
  - `CraftingUI`: bind `CraftingStation` once in `OnEnable`, not in Update path
- [ ] **2.22** Update ALL existing tests to work with encapsulation + interface changes
- [ ] **2.23** Write new tests:
  - `GameStateMachineTests.cs` (EditMode)
  - `GameEventBusTests.cs` (EditMode)
  - `PlayerMovementTests.cs` (EditMode + PlayMode if needed)
  - `InterfaceImplementationTests.cs` (EditMode — verify all expected classes implement interfaces)
- [ ] **2.24** Verification: compile → EditMode → PlayMode → build → AutoTest → Player.log
- [ ] **2.25** Update `CHANGELOG.md` and `ROADMAP.md`

---

### PHASE 3: ECS / DOTS Integration

> **Precondition**: Phase 2 complete (interfaces exist, responsibilities separated).
> **Risk mitigation (from Further Considerations)**: ECS has less agent training data. The ECS instruction file (step 3.12) MUST include 2-3 fully worked code templates (component + system + bridge) that agents can use as copy-paste references.

- [ ] **3.1** Add DOTS packages to `Packages/manifest.json`:
  - `com.unity.entities`
  - `com.unity.physics` (if spatial queries needed in ECS)
  - Verify `com.unity.burst` and `com.unity.collections` already present
- [ ] **3.2** Create `Assets/Scripts/ECS/` folder
- [ ] **3.3** Create `Assets/Scripts/ECS/ByteWar.ECS.asmdef`:
  - References: `Unity.Entities`, `Unity.Burst`, `Unity.Collections`, `Unity.Mathematics`, `ByteWar`
- [ ] **3.4** Create `Assets/Scripts/ECS/AGENTS.md` with ECS-specific conventions
- [ ] **3.5** Define ECS components in `Assets/Scripts/ECS/Components/`:
  - `EnemyTag.cs` (IComponentData, tag)
  - `HealthData.cs` (IComponentData: Current, Max)
  - `MoveSpeedData.cs` (IComponentData: Value)
  - `TargetData.cs` (IComponentData: Entity Value)
  - `AttackCooldownData.cs` (IComponentData: Remaining, Interval, Damage, Range)
  - `ProjectileData.cs` (IComponentData: Speed, Damage, MaxLifetime, Elapsed)
  - `SpawnTimerData.cs` (IComponentData: Remaining, Interval)
- [ ] **3.6** Implement ECS systems in `Assets/Scripts/ECS/Systems/`:
  - `EnemyAISystem.cs` — burst-compiled enemy tick (target acquisition, movement, attack cooldown)
  - `ProjectileSystem.cs` — projectile movement + lifetime + collision detection
  - `StatusEffectSystem.cs` — tick effect durations, remove expired
  - `SpawnerSystem.cs` — timed entity spawning
- [ ] **3.7** Create `ECSNetworkBridge.cs` (MonoBehaviour on NetworkManager):
  - Server-only, configurable tick rate (default 20Hz)
  - Syncs ECS Health → NGO NetworkVariable on corresponding EnemyAI NetworkBehaviour
  - Syncs ECS ProjectileData → NGO FireballProjectile position
- [ ] **3.8** Migrate `EnemyAI.Update()` logic to `EnemyAISystem` (keep EnemyAI as thin NGO wrapper)
- [ ] **3.9** Migrate `FireballProjectile.Update()` logic to `ProjectileSystem`
- [ ] **3.10** Migrate `EnemySpawner.Update()` logic to `SpawnerSystem`
- [ ] **3.11** Write ECS tests:
  - `ECSComponentTests.cs` (EditMode) — component data integrity
  - `ECSSystemTests.cs` (EditMode) — systems process entities correctly
  - `ECSBridgeTests.cs` (EditMode) — bridge syncs data correctly
- [ ] **3.12** Create `.github/instructions/ecs.instructions.md` with:
  - MB vs ECS decision tree
  - Naming conventions (Data suffix, Tag suffix, System suffix)
  - 3 fully worked templates: simple component, system with burst job, bridge sync pattern
  - Burst/Jobs constraints: no managed refs, no allocations, no Debug.Log in jobs
- [ ] **3.13** Verification: compile → EditMode → PlayMode → build → AutoTest
- [ ] **3.14** Update `CHANGELOG.md` and `ROADMAP.md`

---

### PHASE 4: Dedicated Server

> **Precondition**: Phase 2 complete (state machine separates client/server concerns). Phase 3 optional but recommended.

- [ ] **4.1** Create server build profile in Unity (Build Profiles or scripted)
- [ ] **4.2** Add `#if UNITY_SERVER` / `#if !UNITY_SERVER` guards to strip client-only code:
  - Strip: `ThirdPersonCamera`, `PlayerInputHandler`, `ScreenLogger`, `VisualGroundingUtility`
  - Strip: all `*UI` scripts (PlayerVitalsUI, InventoryUI, EnemyHealthUI, ActionBarUI, CraftingUI)
  - Strip: Mixamo processing, visual grounding, animator visual diagnostics
  - Keep: `NetworkPlayer` (server-side movement validation), `GameManager`, all Networking
- [ ] **4.3** Add `BuildServerWindows()` and `BuildServerLinux()` methods to `BuildScript.cs`
- [ ] **4.4** Server-authoritative movement validation:
  - Server validates client-reported position (max speed check, teleport detection)
  - Log violations, rubber-band on excessive delta
- [ ] **4.5** RPC rate limiting:
  - `CastAbilityServerRpc` — min interval matching cooldown
  - `AttackEnemyServerRpc` — min interval
  - `GatherResourceServerRpc` — min interval
  - `PlaceBuildingServerRpc` — min interval
  - Log and drop rate-limited requests
- [ ] **4.6** Create `.github/instructions/networking.instructions.md`:
  - Server authority rules
  - `#if UNITY_SERVER` stripping rules
  - RPC validation patterns
  - Rate limiting patterns
  - Anti-cheat guidelines
- [ ] **4.7** Write server-specific tests:
  - `ServerBuildStrippingTests.cs` (EditMode) — verify client-only types have `#if !UNITY_SERVER`
  - `RateLimitingTests.cs` (EditMode) — verify rate limiting logic
- [ ] **4.8** Build and verify both client and server
- [ ] **4.9** Update `CHANGELOG.md` and `ROADMAP.md`

---

### PHASE 5: Performance & Code Quality

> **Precondition**: Phases 2-3 complete.

- [ ] **5.1** Implement object pooling using `UnityEngine.Pool.ObjectPool<T>`:
  - Pool: `FireballProjectile` instances
  - Pool: `EnemyAI` instances (via EnemySpawner)
  - Create `PoolManager.cs` in Core/ as a central registry
- [ ] **5.2** Implement `SpatialHashGrid.cs` in Core/:
  - Grid-based spatial partitioning for proximity queries
  - Replace `Physics.OverlapSphere` in `FrostNovaAbility`
  - Replace `FindGameObjectsWithTag` in `EnemyAI` (already replaced by PlayerRegistry, but spatial hash is better for 1000+ entities)
- [ ] **5.3** Optimize remaining FindObject calls:
  - Verify no `FindObjectOfType`/`FindObjectsByType` in any Update/FixedUpdate/Tick
- [ ] **5.4** Write performance tests:
  - `PoolingTests.cs` (EditMode) — acquire/release cycle, capacity
  - `SpatialHashTests.cs` (EditMode) — insert, query, remove
- [ ] **5.5** Verification: compile → tests → build → AutoTest
- [ ] **5.6** Update `CHANGELOG.md`

---

### PHASE 6: Instruction Files & Agent Conventions

> **Precondition**: Phases 2-4 complete (conventions must match implemented architecture).
> **Note**: Step 6.2 (agent-workflow) has no code dependency and CAN be done earlier / in parallel with Phase 2.

- [ ] **6.1** Create `.github/instructions/architecture.instructions.md`:
  - Hybrid MB/ECS architecture rules
  - Interface-first design mandate
  - Encapsulation rules (no public mutable fields)
  - God-class prevention (800 LOC, single responsibility)
  - Magic number prohibition (GameConstants or per-system SO)
  - State management (GameStateMachine)
  - Event communication (GameEventBus for cross-system, instance events for parent-child)
- [ ] **6.2** Create `.github/instructions/agent-workflow.instructions.md`:
  - Agent decision tree: "Given a task, how do I decide what to do?"
  - When to escalate to user (aesthetic/taste choices ONLY)
  - Ambiguity protocol: implement simpler option + log alternative to TECH_DEBT.md
  - Self-validation checklist (compile → tests → build → autoTest → Player.log)
  - How to read/update ROADMAP.md, CHANGELOG.md, ERROR_LOG.md
  - How to use Designer agent for balance decisions
  - Convention: agents MUST read instruction files before modifying any folder
  - Error recovery: max 3 retries, then ERROR_LOG.md + escalate
- [ ] **6.3** Create `.github/copilot-instructions.md` (global Copilot context):
  - Architecture summary (hybrid MB/ECS, NGO networking, data-driven SOs)
  - Links to all instruction files
  - Quality gates: zero warnings, 100% test coverage, AutoTester validation
  - Project name: ByteWar
- [ ] **6.4** Update existing AGENTS.md files:
  - Root `AGENTS.md`: ECS refs, dedicated server refs, new instruction file links, ByteWar name, exe path
  - `Assets/Scripts/AGENTS.md`: interface-first rule, encapsulation rule
  - `Assets/Scripts/Core/AGENTS.md`: state machine, event bus, constants rules
  - `Assets/Scripts/Networking/AGENTS.md`: dedicated server build rules
  - `Assets/Scripts/Abilities/AGENTS.md`: link to architecture instructions
  - `Assets/Scripts/Survival/AGENTS.md`: link to architecture instructions
  - `Assets/Scripts/UI/AGENTS.md`: link to architecture instructions
  - `Assets/Scripts/Tests/AGENTS.md`: link to testing + architecture instructions
  - `Assets/Scripts/Editor/AGENTS.md`: link to architecture instructions
  - `Assets/Art/AGENTS.md`: no change needed
- [ ] **6.5** Create `Assets/Scripts/Building/AGENTS.md`:
  - Building system conventions, persistence rules
- [ ] **6.6** Create `Assets/Scripts/ECS/AGENTS.md` (if not done in 3.4):
  - ECS-specific conventions
- [ ] **6.7** Update `.github/GAME_PROTOCOL.md`:
  - Add error recovery protocol (max 3 retries before escalation)
  - Add ECS build/test step to Definition of Done
  - Add server build step to Definition of Done
- [ ] **6.8** Update `.github/instructions/testing.instructions.md`:
  - Reference `Tools/verify.ps1` (created in Phase 7)
  - Add ECS test rules
  - Add architectural convention test rules
  - Reduce reflection guidance (prefer internal + InternalsVisibleTo)
  - Update Player.log path to `BytedotGit\ByteWar`
  - Update exe path to `ByteWar.exe`

---

### PHASE 7: CI/CD & Verification Infrastructure

> **Precondition**: Phase 4 step 4.3 (server build method exists). Phase 6.2 can be in parallel.

- [ ] **7.1** Create `Tools/verify.ps1`:
  - Single-command local verification loop:
    1. EditMode tests (batchmode)
    2. PlayMode tests (batchmode)
    3. Client build
    4. Server build
    5. Launch client with `-autoTest`
    6. Parse Player.log for PASS/FAIL + exceptions
    7. Summary report with exit code 0/1
- [ ] **7.2** Create `Tools/validate-conventions.ps1`:
  - Check: no `public List<>` / `public Dictionary<>` fields in non-test runtime scripts
  - Check: no `FindObjectOfType`/`FindObjectsByType` in `Update()`/`FixedUpdate()`
  - Check: no unthrottled `Debug.Log` in Update/FixedUpdate
  - Check: file size < 800 LOC for all runtime scripts
  - Check: interface implementation requirements
  - Exit 0 if all pass, exit 1 with details if any fail
- [ ] **7.3** Create `Tools/validate-tests.ps1`:
  - Verify every runtime `.cs` file has a corresponding test file
  - Report untested files
- [ ] **7.4** Create `.github/workflows/unity-ci.yml`:
  - Trigger: push to `main`, all PRs
  - Jobs:
    a. Lint: run `validate-conventions.ps1`
    b. EditMode tests
    c. PlayMode tests
    d. Build client
    e. Build server
    f. AutoTest client
    g. Parse Player.log
  - Uses: `game-ci/unity-builder` or `game-ci/unity-test-runner`
  - Artifacts: build output, test results XML, Player.log
  - Requires: Unity license `.ulf` as GitHub secret (one-time manual step — see 7.6)
- [ ] **7.5** Create `.github/workflows/pr-checks.yml`:
  - Lightweight PR gate
  - Check: ROADMAP updated if phase changed
  - Check: CHANGELOG updated
  - Check: no files > 800 LOC
  - Check: run `validate-conventions.ps1`
- [ ] **7.6** Generate Unity license activation file:
  - Run `Unity -batchmode -manualLicenseFile`
  - Upload as GitHub Secret `UNITY_LICENSE`
  - (This is a ONE-TIME manual step by the user)
- [ ] **7.7** Verification: push to branch, confirm CI passes
- [ ] **7.8** Update `CHANGELOG.md`

---

### PHASE 8: Agent Guardrails & Custom Agents

> **Precondition**: Phase 6 complete (conventions exist to enforce).

- [ ] **8.1** Create `.github/agents/architect.agent.md`:
  - Specializes in system design, interface design, ECS vs MB decisions
  - References architecture.instructions.md and ecs.instructions.md
- [ ] **8.2** Create `.github/agents/debugger.agent.md`:
  - Specializes in Player.log analysis, test failure diagnosis
  - Methodical root-cause approach: read error → identify file:line → propose fix → test
  - References AutoTester exit code table
- [ ] **8.3** Create `.github/agents/reviewer.agent.md`:
  - Convention enforcement agent
  - Runs `validate-conventions.ps1` mentally on proposed changes
  - Checks: encapsulation, interfaces, 800 LOC, test coverage, naming
- [ ] **8.4** Update `.github/agents/designer.agent.md`:
  - Add reference to GameConstants ScriptableObject for balance values
  - Add ECS performance considerations for large entity counts
- [ ] **8.5** Define structured agent logging format:
  - `[AgentAction] scope=<phase> action=<create|modify|delete> file=<path> reason=<brief>`
  - Add `.gitignore` entry for `SESSION_LOG.md`
- [ ] **8.6** Update error recovery protocol in `GAME_PROTOCOL.md`:
  - Compile failure → revert specific change + try alternative
  - Test failure → read output + root-cause + fix
  - AutoTest failure → match exit code → fix
  - 3 retry max → ERROR_LOG.md + escalate to user
  - NEVER brute force retry the same approach
- [ ] **8.7** Update `CHANGELOG.md`

---

### PHASE 9: Test Infrastructure Hardening

> **Precondition**: Phases 2-4 complete (things to test exist).

- [ ] **9.1** Create `Assets/Scripts/Tests/TestUtilities.cs`:
  - Factory methods: `CreateTestEnemy()`, `CreateTestPlayer()`, `CreateTestAbility()`, `CreateTestItem()`
  - Encapsulate all reflection-heavy setup currently duplicated across tests
  - Use `internal` access + `InternalsVisibleTo` instead of reflection where possible
- [ ] **9.2** Refactor existing PlayMode tests to use `TestUtilities` instead of raw reflection
- [ ] **9.3** Refactor existing EditMode tests to use `TestUtilities`
- [ ] **9.4** Create `ArchitecturalConventionTests.cs` (EditMode):
  - Scan runtime assemblies via reflection:
    - Assert no public mutable `List<>` / `Dictionary<>` fields
    - Assert no classes > 800 LOC
    - Assert all non-abstract, non-SO runtime classes implement at least one interface
    - Assert no `FindObjectOfType` in Update/FixedUpdate (source file regex scan)
  - These tests FAIL THE BUILD if conventions are violated
- [ ] **9.5** Add ECS tests (if not done in 3.11):
  - System unit tests using `World.CreateSystem<T>()`
  - Bridge sync tests
- [ ] **9.6** Expand AutoTester:
  - ECS validation: entity count > 0, systems updating
  - State machine validation: transitions through expected states
  - Server mode detection (when running as server)
- [ ] **9.7** Verification: all tests pass, AutoTest passes
- [ ] **9.8** Update `CHANGELOG.md` and `ROADMAP.md` (mark architectural overhaul phase complete)

---

## Dependency Graph

```
PHASE 1 (Rename)
  └─→ PHASE 2 (Abstractions) ─┬─→ PHASE 3 (ECS) ──→ PHASE 5 (Perf)
                               ├─→ PHASE 4 (Server) ─→ PHASE 5 (Perf)
                               │                       │
                               └─→ PHASE 6 (Instructions) ◄── Phases 2-4 done
                                     │
                                     ├─→ PHASE 7 (CI/CD) ◄── Phase 4.3 (server build)
                                     ├─→ PHASE 8 (Agents) ◄── Phase 6
                                     └─→ PHASE 9 (Tests)  ◄── Phases 2-4
```

**Parallelism opportunities**:

- 6.2 (agent-workflow instructions) can start during Phase 2
- Phase 3 (ECS) and Phase 4 (Server) can run in parallel after Phase 2
- Phase 5 runs after both 3 and 4
- Phases 7, 8, 9 can run in parallel after Phase 6

---

## Decisions

| Decision                                | Rationale                                                                                        |
| --------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Rename SurvivalRPG → ByteWar as Phase 1 | Eliminates name confusion early; single atomic commit                                            |
| Company name: BytedotGit                | Matches GitHub org name for consistency                                                          |
| Hybrid MB/ECS                           | NGO doesn't support ECS entities natively; full Netcode for Entities would be a complete rewrite |
| ECS instruction templates               | 3 fully worked examples to compensate for less agent training data on DOTS                       |
| NetworkPlayer split first in Phase 2    | Highest-risk refactor; must be done early with full test validation before/after                 |
| No DI framework                         | Interfaces + GetComponent<IFoo> + RequireComponent is sufficient; VContainer if needed later     |
| CI + local verification                 | Complementary: local = fast agent feedback; CI = safety net on push                              |
| Max 3 retries in error recovery         | Prevents infinite agent loops; forces escalation                                                 |
| Single scene + additive loading         | Confirmed by user; no multi-scene architecture needed                                            |

---

## Estimated File Count

| Category               | New Files | Modified Files       |
| ---------------------- | --------- | -------------------- |
| Phase 1 (Rename)       | 0         | ~80+ (global rename) |
| Phase 2 (Abstractions) | ~15       | ~40                  |
| Phase 3 (ECS)          | ~12       | ~8                   |
| Phase 4 (Server)       | ~3        | ~15                  |
| Phase 5 (Perf)         | ~3        | ~5                   |
| Phase 6 (Instructions) | ~6        | ~12                  |
| Phase 7 (CI/CD)        | ~5        | ~2                   |
| Phase 8 (Agents)       | ~4        | ~2                   |
| Phase 9 (Tests)        | ~3        | ~20                  |
| **Total**              | **~51**   | **~80+**             |
