# Abilities - Agent Guidance

## Key classes (auto-generated)

| Class | Base | Interfaces | Network | RPCs |
|-------|------|-----------|---------|------|
| `Ability` | `ScriptableObject` | - | - | - |
| `AbilitySystemComponent` | `NetworkBehaviour` | IAbilityExecutor | Yes | CastAbilityServerRpc, PlayAbilityEffectClientRpc |
| `AttributeSet` | `NetworkBehaviour` | - | Yes | - |
| `GameplayEffect` | `ScriptableObject` | - | - | - |
| `Talent` | `ScriptableObject` | - | - | - |
| `BlinkAbility` | `Ability` | - | - | - |
| `FireballAbility` | `Ability` | - | - | - |
| `FrostNovaAbility` | `Ability` | - | - | - |
| `AbilityModifierTalent` | `Talent` | - | - | - |
| `CleaveAbility` | `Ability` | - | - | - |

## Invariants

- Abilities are ScriptableObjects and execute server-authoritatively.
- Apply attribute changes via GameplayEffects, not direct mutation.

## Tests

- Every new ability/effect must include unit tests + at least one PlayMode smoke.

## UX correctness

- Only trigger cast/attack animations when an ability actually executes.
