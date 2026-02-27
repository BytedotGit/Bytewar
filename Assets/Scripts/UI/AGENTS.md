# UI - Agent Guidance

## Key classes (auto-generated)

| Class | Base | Interfaces | Network | RPCs |
|-------|------|-----------|---------|------|
| `ActionBarUI` | `MonoBehaviour` | - | - | - |
| `ActionSlot` | `` | - | - | - |
| `CraftingUI` | `MonoBehaviour` | - | - | - |
| `DevConsole` | `MonoBehaviour` | - | - | - |
| `ConsoleLine` | `` | - | - | - |
| `EnemyHealthUI` | `MonoBehaviour` | - | - | - |
| `EnemyTargetTracker` | `` | - | - | - |
| `InventoryUI` | `MonoBehaviour` | - | - | - |
| `KeybindingHUD` | `MonoBehaviour` | - | - | - |
| `PlayerVitalsUI` | `MonoBehaviour` | - | - | - |

## Invariants

- Prefer event-driven UI updates over per-frame polling.
- UI must not spam logs per frame.
- Keep `AssetDeployUI` split across partial files by responsibility. `AssetDeployUI.RadialNode.cs` should only contain radial tree data structure behavior.

## Tests

- Update UI tests when binding logic changes.
