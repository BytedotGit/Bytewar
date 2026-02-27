# Server-Authoritative Destruction - Implementation Spec

## Goal

Implement a deterministic multiplayer destruction system for:

1. Trees.
2. Rocks/ore nodes.
3. Build pieces with structural support and collapse.

The server is the source of truth for damage, state transitions, collapse, and drops.

## Scope

- Damage application by material-aware damage types (`Axe`, `Pick`, `Fire`, `Blunt`).
- Multi-state destructible lifecycle (`Intact`, `Damaged`, `Destroyed`).
- Structural support checks for build pieces.
- Authoritative loot/drop spawning.

## Non-Goals

- Fully deformable terrain mesh.
- Per-vertex fracture simulation.
- Physics-heavy dynamic destruction for all world objects.

## Core Data Model

1. `DestructibleProfile` (ScriptableObject)
- `destructibleClass` (`Tree`, `Rock`, `BuildPiece`, `OreNode`)
- `maxHealth`
- `damageMultipliers` by damage type
- `stateThresholds`
- `dropTableId`

2. `DestructibleState`
- `state` enum (`Intact`, `Damaged`, `Destroyed`)
- `currentHealth`
- `lastDamageType`

3. `SupportProfile` (build pieces)
- Material support range
- Adjacency rules
- Collapse delay settings

## Runtime Architecture

1. `DestructibleComponent` (networked)
- Receives server-side damage events.
- Updates state and triggers visual state changes.
- Invokes drops on terminal state.

2. `DamageResolver`
- Resolves effective damage from base amount, tool type, and profile multipliers.

3. `SupportSystem`
- Recomputes support graph for nearby structures on placement/destruction.
- Marks unsupported pieces for collapse.

4. `DropSpawner`
- Server-spawns loot/drops from drop tables.
- Ensures ownership/collection rules are respected.

## Networking Model

- Client sends `RequestDamage(targetId, toolType, hitInfo)` RPC.
- Server validates range/ownership/rate limits.
- Server applies damage and updates authoritative state.
- State replication drives visuals on all clients.
- Collapse and drops are initiated by server events only.

## State Transition Rules

1. `Intact -> Damaged`
- Triggered when health falls below configured threshold.

2. `Damaged -> Destroyed`
- Triggered when health reaches 0.

3. `BuildPiece Collapse`
- Triggered when support path is invalid after recompute.
- Optional short delay for readability before removal/spawn of debris state.

## Destruction Flow

1. Client action attempts damage.
2. Server validates and resolves effective damage.
3. Server updates health/state.
4. Visual state sync occurs on all clients.
5. If destroyed, server spawns drops and terminal visuals.
6. For structures, server recomputes support and propagates collapse.

## Anti-Exploit Requirements

1. Server-side cooldown/rate limiting per attacker-target pair.
2. Distance and line-of-sight checks for damage requests.
3. Rejection and telemetry for invalid damage RPCs.

## Test Plan

## EditMode

1. Damage multiplier resolution by type/profile.
2. State threshold transitions.
3. Support graph recompute correctness.

## PlayMode

1. Host-client damage replication for trees/rocks/build pieces.
2. Concurrent attackers produce deterministic terminal state.
3. Late-join clients see correct destroyed/intact states.

## AutoTester Assertions

1. Tree can be destroyed by axe and drops expected items.
2. Rock/ore can be destroyed by pick and drops expected items.
3. Unsupported build pieces collapse after support loss.

## Milestone Breakdown

1. Destructible data profiles + resolver.
2. Server damage authority wiring.
3. State visuals + drop spawning.
4. Support/collapse propagation.
5. Test and AutoTester coverage.