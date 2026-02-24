# Survival - Agent Guidance

## Key classes (auto-generated)

| Class | Base | Interfaces | Network | RPCs |
|-------|------|-----------|---------|------|
| `EnemyAI` | `NetworkBehaviour` | IDamageable, ICombatTarget | Yes | - |
| `EnemySpawner` | `NetworkBehaviour` | - | Yes | - |
| `EquipmentComponent` | `NetworkBehaviour` | - | Yes | - |
| `FireballProjectile` | `NetworkBehaviour` | - | Yes | PlayHitEffectClientRpc |
| `InventoryComponent` | `NetworkBehaviour` | IInventoryHolder | Yes | NotifyItemAddedClientRpc, NotifyItemRemovedClientRpc |
| `Item` | `ScriptableObject` | - | - | - |
| `ResourceNode` | `NetworkBehaviour` | IDamageable, IInteractable | Yes | PlayGatherHitClientRpc, PlayResourceDeathClientRpc |
| `SurvivalStats` | `NetworkBehaviour` | - | Yes | - |

## Invariants

- Damage, death, drops are server-authoritative.
- Gathering should be end-to-end tested (node destroyed, items received).

## Tests

- Maintain PlayMode smoke coverage for combat + gathering.
