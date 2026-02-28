# Assets/Scripts — Agent Guidance

Applies to all runtime C# under `Assets/Scripts`.

## Non-negotiables

- Follow `.github/GAME_PROTOCOL.md` and the root `AGENTS.md`.
- Add tests and logs for every change.
- Avoid per-frame allocations and log spam.

## Dynamic testing rule

- If you change user-facing gameplay behavior, you must add/update:
  - at least one automated test (EditMode and/or PlayMode)
  - and (if it affects the playable build) an `AutoTester` scenario step or assertion.

## Implementation spec docs

- Gear visibility baseline spec: `Assets/Scripts/Docs/HybridGearVisibilitySpec.md`.
- Server-authoritative destruction baseline spec: `Assets/Scripts/Docs/ServerAuthoritativeDestructionSpec.md`.
