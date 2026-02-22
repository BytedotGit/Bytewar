# Networking — Agent Guidance

## Invariants
- Server authoritative for damage/spawning/inventory.
- Use ServerRpc for client requests and ClientRpc for broadcast.
- Log network IDs (ClientId, NetworkObjectId) for debugging.

## Tests
- Add PlayMode smoke tests for end-to-end net flows.
