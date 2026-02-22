# Bytewar — Agent Execution Contract

This repository is designed to be developed with GitHub Copilot Chat using **Plan mode** and **Agent mode**.

**ROADMAP**: Always consult `.github/ROADMAP.md` to understand the current project state and the next steps required to reach a playable build. You MUST automatically update it whenever a significant feature, phase, or bugfix is completed.

**Testing & Debugging**: See `.github/instructions/testing.instructions.md` for strict enforcement rules on test coverage, smoke tests, logging, file size limits, and the self-testing loop.

## Mode responsibilities

### Plan mode (Phase 1)

- No code/asset changes.
- Must output, in order:
  1. **System Snapshot** (what exists + where)
  2. **Performance Budget** (feature-specific)
  3. **Chosen Scope** (exactly one primary goal)
  4. **FMEA** (5+ failure modes)
  5. **Acceptance Criteria** (testable, includes automated + manual)
  6. **Asset & Bloat Review**
  7. **To-do list** (dependency ordered)

### Agent mode (Phase 2)

- Implement only what Phase 1 approved.
- Must include:
  - extensive logs (but avoid per-frame spam)
  - tests and smoke tests
  - batch build + `-autoTest` run + Player.log verification
- Must update `.github/ROADMAP.md` when a meaningful milestone is completed.

## Non-negotiables

1. **Warnings are defects**

- Compiler warnings, missing references, or build warnings that imply runtime risk are treated as failures.

2. **Dynamic tests for what changed**

- Every user-facing behavior change must be backed by:
  - at least one automated test (EditMode and/or PlayMode)
  - an `AutoTester` scenario step or assertion when the change affects a playable build flow

3. **Anti-refactor drift**

- Only touch files required for the chosen scope.
- If you spot unrelated issues, add an entry to `TECH_DEBT.md`.

3b. **Defect tracking**

- If a user-visible defect is reported and not fully resolved in the same session, add/update an entry in `.github/ERROR_LOG.md` with repro steps and the most relevant logs.

4. **Atomic state sync**

- Code + docs + tracking must not drift.
- If behavior changes, update:
  - `.github/ROADMAP.md`
  - `CHANGELOG.md`
  - relevant folder `AGENTS.md` invariants

## Build/self-test loop (mandatory)

1. Build the game (batchmode).
2. Launch the executable with `-autoTest` (e.g., `Start-Process -FilePath "Builds\Windows\ByteWar.exe" -ArgumentList "-autoTest" -NoNewWindow`). DO NOT use OS-level input simulation that steals focus.
3. Wait for `AutoTester` to finish and close the game, then inspect Player.log for:
   - no exceptions
   - PASS markers for the feature being changed
4. Keep iterating until the fix/feature is verified. ONLY THEN let the user know to test.

## General Unity C# Conventions

- Use modern C# features where applicable.
- Prefer `SerializeField` over `public` for inspector variables.
- Keep `Update` methods clean; use events or coroutines for complex logic.
- Use `ScriptableObject`s for data-driven design (e.g., items, talents, abilities).

## Networking (Netcode for GameObjects — NGO)

- Use `NetworkBehaviour` instead of `MonoBehaviour` for networked objects.
- Use `NetworkVariable` for state synchronization (e.g., Health, Mana).
- Use `ServerRpc` for client-to-server requests (e.g., casting a spell, placing a building).
- Use `ClientRpc` for server-to-client broadcasts (e.g., playing visual effects).
- Ensure critical logic (damage, spawning, inventory) is server-authoritative.

## Custom Ability System Architecture

- The game uses a lightweight, custom implementation of the Gameplay Ability System (GAS).
- `AbilitySystemComponent` manages a player's spells, cooldowns, and effects.
- `AttributeSet` manages stats like Health, Mana, and Stamina.
- `GameplayEffect` handles buffs, debuffs, and damage over time.
- See `.github/instructions/abilities.instructions.md` for detailed ability/talent creation rules.
