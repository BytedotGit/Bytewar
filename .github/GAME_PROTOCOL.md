# GAME_PROTOCOL.md — Engineering Execution Protocol (Bytewar)

This document is the **system contract** for autonomous agent work and disciplined human development in Bytewar.

It is adapted from the protocol you provided and aligned with this repository’s workflow:

- Copilot **Plan mode** = Phase 1 (plan/review)
- Copilot **Agent mode** = Phase 2 (implement/test)

## Part 1: Agentic SWE Principles (How We Work)

1. **Two-Phase Execution (Think, Then Act)**

- Phase 1 is purely analytical (Architecture, Risk, Failure Modes, Acceptance Criteria).
- Phase 2 is implementation.
- If Phase 1 is skipped, the work is rejected.

2. **Warnings are Defects (Zero Tolerance)**

- Treat any compiler warning, missing reference, import warning that implies runtime risk, or test warnings as failures.
- A warning today is a crash, perf regression, or memory leak tomorrow.

3. **Anti-Refactor Drift (Scope Containment)**

- Only touch files required for the chosen scope.
- If a nearby system is messy but not blocking, log it to `TECH_DEBT.md`.

4. **Continuous Footprint Reduction**

- Every iteration includes a “Footprint Check”:
  - unused prefabs/assets
  - orphaned references
  - log spam in update loops
  - avoidable allocations/GC

5. **Atomic State Syncing**

- Code + documentation + tracking are updated together:
  - `.github/ROADMAP.md`
  - `CHANGELOG.md`
  - relevant folder `AGENTS.md`

6. **FMEA (Failure Mode and Effects Analysis)**

- Before building, list how it will break (5+ edge cases).
- Ensure tests/log assertions cover those failure modes.

## Part 2: Mandatory Phase 1/2 Execution Checklist

### PHASE 1: PLAN ONLY (No Code/Asset Changes)

Rules:

- ❌ No code changes
- ❌ No file edits
- ❌ No asset imports
- ✅ Read and analyze only

Required sections (in order):

1. System Snapshot
2. Performance Budget
3. Chosen Scope (ONE primary fix/feature)
4. FMEA (5+ edge cases)
5. Acceptance Criteria (must include 1+ automated test and 1+ manual playtest scenario)
6. Asset & Bloat Review
7. To-Do list (dependency ordered)

Hard stop:

- If any self-check fails, fix Phase 1 before implementing.

### PHASE 2: IMPLEMENT (Execute the Plan)

Rules:

- ✅ Execute only after Phase 1 is complete
- ✅ Follow the to-do list; mark DONE/NOT DONE
- ❌ No extra scope

Required sections (in order):
A) Change Summary
B) Files & Assets Changed
C) Verification Results (compiler + tests + build + autoTest + Player.log)
D) Performance/GC Check (no allocations in hot loops)
E) Playtest Results (feel + FMEA edge cases)
F) Footprint Changes
G) ROADMAP / CHANGELOG updates

## Definition of Done (Game-Specific Validation Loop)

A change is Done only if:

- Phase 1 complete
- Code compiles with ZERO warnings
- Tests pass
- Asset validation passes (no missing refs; import settings within budget)
- `AutoTester` validates the changed user-facing behavior when applicable
- Player.log has no exceptions
- Footprint check performed
- ROADMAP + CHANGELOG updated

## Hot Loop rule

- Never instantiate/destroy or do heavy string/log work in `Update()`/`FixedUpdate()`/ticks.
- Cache references in `Awake()`/`Start()`.
