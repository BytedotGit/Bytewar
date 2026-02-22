# Bytewar — Agent Execution Contract

This repository is designed to be developed with GitHub Copilot Chat using **Plan mode** and **Agent mode**.

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

4. **Atomic state sync**

- Code + docs + tracking must not drift.
- If behavior changes, update:
  - `.github/ROADMAP.md`
  - `CHANGELOG.md`
  - relevant folder `AGENTS.md` invariants

## Build/self-test loop (mandatory)

1. Build the game (batchmode).
2. Run `Builds/Windows/SurvivalRPG.exe -autoTest`.
3. Inspect Player.log for:
   - no exceptions
   - PASS markers for the feature being changed
