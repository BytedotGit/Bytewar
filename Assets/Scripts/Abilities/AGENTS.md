# Abilities — Agent Guidance

## Invariants
- Abilities are ScriptableObjects and execute server-authoritatively.
- Apply attribute changes via GameplayEffects, not direct mutation.

## UX correctness
- Only trigger cast/attack animations when an ability actually executes.

## Tests
- Every new ability/effect must include unit tests + at least one PlayMode smoke.
