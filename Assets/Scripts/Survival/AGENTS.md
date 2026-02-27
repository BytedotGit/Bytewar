# Survival - Agent Guidance

## Key classes (auto-generated)

| Class                        | Base               | Interfaces                 | Network | RPCs                                                 |
| ---------------------------- | ------------------ | -------------------------- | ------- | ---------------------------------------------------- |
| `EnemyAI`                    | `NetworkBehaviour` | IDamageable, ICombatTarget | Yes     | -                                                    |
| `EnemySpawner`               | `NetworkBehaviour` | -                          | Yes     | -                                                    |
| `EquippedLoadoutState`       | `struct`           | -                          | -       | -                                                    |
| `EquipmentComponent`         | `NetworkBehaviour` | -                          | Yes     | -                                                    |
| `GearVisualProfile`          | `ScriptableObject` | -                          | -       | -                                                    |
| `GearVisualProfileRegistry`  | `ScriptableObject` | -                          | -       | -                                                    |
| `FireballProjectile`         | `NetworkBehaviour` | -                          | Yes     | PlayHitEffectClientRpc                               |
| `InventoryComponent`         | `NetworkBehaviour` | IInventoryHolder           | Yes     | NotifyItemAddedClientRpc, NotifyItemRemovedClientRpc |
| `Item`                       | `ScriptableObject` | -                          | -       | -                                                    |
| `DestructibleProfile`        | `ScriptableObject` | -                          | -       | -                                                    |
| `DestructibleComponent`      | `NetworkBehaviour` | IDamageable                | Yes     | -                                                    |
| `DestructibleRuntimeState`   | `struct`           | -                          | -       | -                                                    |
| `DamageResolver`             | `static class`     | -                          | -       | -                                                    |
| `DestructibleStateEvaluator` | `static class`     | -                          | -       | -                                                    |
| `SupportProfile`             | `ScriptableObject` | -                          | -       | -                                                    |
| `ResourceNode`               | `NetworkBehaviour` | IDamageable, IInteractable | Yes     | PlayGatherHitClientRpc, PlayResourceDeathClientRpc   |
| `SurvivalStats`              | `NetworkBehaviour` | -                          | Yes     | -                                                    |

## Invariants

- Damage, death, drops are server-authoritative.
- Gathering should be end-to-end tested (node destroyed, items received).
- Material-aware damage typing for destructibles is data-driven through profile assets.

## Tests

- Maintain PlayMode smoke coverage for combat + gathering.
- Maintain PlayMode smoke coverage for server-authoritative destructible requests.
