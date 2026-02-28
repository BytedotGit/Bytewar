# Building - Agent Guidance

## Key classes (auto-generated)

| Class | Base | Interfaces | Network | RPCs |
|-------|------|-----------|---------|------|
| `BuildingController` | `NetworkBehaviour` | - | Yes | PlaceBuildingServerRpc, PlaceDeveloperAssetServerRpc, RemoveBuildingServerRpc, RepairBuildingServerRpc, PlayBuildingEffectClientRpc |
| `BuildingPiece` | `NetworkBehaviour` | IDamageable, IPersistable | Yes | - |
| `BuildingPreview` | `` | - | - | - |
| `BuildingRecipe` | `ScriptableObject` | - | - | - |
| `BuildingSnap` | `` | - | - | - |
| `ComfortSystem` | `MonoBehaviour` | - | - | - |
| `RecipeIngredient` | `` | - | - | - |
| `CraftingRecipe` | `ScriptableObject` | - | - | - |
| `CraftingStation` | `NetworkBehaviour` | - | Yes | - |
| `SnapPointMarker` | `MonoBehaviour` | - | - | - |
| `StructuralIntegrity` | `MonoBehaviour` | - | - | - |
| `BuildingSaveEntry` | `` | - | - | - |
| `WorldSaveData` | `` | - | - | - |
| `WorldPersistence` | `NetworkBehaviour` | IPersistable | Yes | - |

## Invariants

- Placement, removal, and repair MUST be server-authoritative (`ServerRpc`).
- `BuildingPiece` implements `IDamageable` and `IPersistable` — do not remove these.
- Crafting deductions MUST happen on server before spawning outputs.
- World persistence serialization must be deterministic and re-entrant.
- `BuildingPreview` is client-only — never send preview state over the network.
- `SnapPointMarker.SnapPointType` determines compatibility — only matching types can snap together (see `CanSnapTo()`).
- `StructuralIntegrity` propagates support from ground; pieces beyond `StructuralMaterial` max distance collapse.
- `ComfortSystem` prevents stacking bonuses from the same `ComfortGroup`.
- 12 building piece types: Foundation, Wall, Floor, Ramp, Roof26, Stairs, Pole, Beam, AngledWall, DoorFrame, Window, HalfWall.
- Rotation step is 22.5° (Valheim-style fine placement).

## Tests

- PlayMode smoke tests for placement flow (`BuildingPlacementSmokeTests`).
- EditMode tests for recipe validation and building system logic (`BuildingSystemTests`).
- Any new building feature must include tests covering the server-authoritative path.
