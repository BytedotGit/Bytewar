# Hybrid Gear Visibility - Implementation Spec

## Goal

Implement a production-ready hybrid gear system where:

1. Armor pieces are skinned mesh replacements.
2. Weapons and carried props are socket attachments.
3. Body masking prevents clipping and visual overlap.

## Scope

- Runtime equip/unequip visuals.
- Network replication of equipped item IDs.
- Deterministic mapping from item ID to visual prefab/mesh.
- Body-region visibility masks per equipped armor slot.

## Non-Goals

- Cosmetic dye/transmog systems.
- Full cloth simulation.
- Runtime procedural mesh generation.

## Core Data Model

1. `GearVisualProfile` (ScriptableObject)
- `itemId`
- `slot` (`Helmet`, `Chest`, `Legs`, `Gloves`, `Boots`, `MainHand`, `OffHand`, `Back`)
- `visualType` (`SkinnedReplacement` or `SocketAttachment`)
- `prefabOrMeshRef`
- `bodyMaskFlags`

2. `BodyMaskFlags`
- Bitmask for `Head`, `Torso`, `Arms`, `Hands`, `Legs`, `Feet`.

3. `EquippedLoadoutState`
- Network-safe IDs only.
- No runtime object refs in sync payload.

## Runtime Architecture

1. `GearVisibilityController` (owner + remote visuals)
- Resolves `itemId -> GearVisualProfile`.
- Applies skinned replacements for armor slots.
- Applies/updates socket attachments for hand/back slots.
- Applies body mask changes after each equip mutation.

2. `BodyMaskController`
- Toggles renderer submeshes or child renderers by body region.
- Maintains deterministic "effective mask" from all equipped pieces.

3. `SocketAttachmentController`
- Owns named sockets (`RightHandSocket`, `LeftHandSocket`, `BackSocket`, etc.).
- Swaps attachment prefab instances atomically.

## Networking Model

- Server-authoritative equip state.
- Client requests equip changes via RPC.
- Server validates ownership/inventory and commits `EquippedLoadoutState`.
- State replicates through `NetworkVariable` or equivalent payload.
- Clients resolve visuals locally from replicated IDs.

## Equip Flow

1. Client requests equip item.
2. Server validates and updates loadout state.
3. Replicated state change triggers local visual refresh on all peers.
4. `GearVisibilityController` diffs old/new state and only swaps changed slots.

## Failure Handling

- Missing profile: fallback to empty slot and warn once.
- Missing socket: fallback to hidden attachment and warn once.
- Missing skinned mesh: keep prior visual and raise recoverable error.

## Test Plan

## EditMode

1. Profile resolution by item ID and slot.
2. Body mask composition with multi-slot armor.
3. Deterministic slot diff behavior.

## PlayMode

1. Equip/unequip replication host-client.
2. Late-join client reconstructs correct visuals.
3. Rapid slot swaps do not leak instances.

## AutoTester Assertions

1. Equipping starter armor changes visible silhouette.
2. Main-hand weapon appears on hand socket when equipped.
3. Unequipped weapon appears on back socket if configured.

## Milestone Breakdown

1. Data scaffolding (`GearVisualProfile`, registry, IDs).
2. Local-only visual swap prototype.
3. Network authority and replication integration.
4. Body masking and clipping pass.
5. Tests + AutoTester coverage.