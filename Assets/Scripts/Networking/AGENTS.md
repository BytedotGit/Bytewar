# Networking - Agent Guidance

## Key classes (auto-generated)

| Class | Base | Interfaces | Network | RPCs |
|-------|------|-----------|---------|------|
| `ClientNetworkAnimator` | `NetworkAnimator` | - | Yes | - |
| `CustomNetworkManagerHUD` | `MonoBehaviour` | - | - | - |
| `NetworkBootstrapper` | `MonoBehaviour` | - | - | - |
| `NetworkPlayer` | `NetworkBehaviour` | - | Yes | - |
| `VisualDiagnostics` | `` | - | - | - |
| `PlayerMovement` | `NetworkBehaviour` | - | Yes | - |
| `PlayerVisualSetup` | `NetworkBehaviour` | - | Yes | - |

## Invariants

- Server authoritative for damage/spawning/inventory.
- Use ServerRpc for client requests and ClientRpc for broadcast.
- Log network IDs (ClientId, NetworkObjectId) for debugging.

## Tests

- Add PlayMode smoke tests for end-to-end net flows.
