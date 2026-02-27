# Tests — Agent Guidance

## Test taxonomy

- EditMode: pure logic + prefab/config invariants.
- PlayMode: end-to-end flows (networked spawn, movement, combat, gathering).

## Rule

- If you change gameplay behavior, update or add a PlayMode smoke test.
- Keep EditMode coverage for ScriptableObject data contracts used by gear visibility and destruction systems.
- Keep PlayMode smoke coverage for server-authoritative destruction request validation (success + rejection paths).
