# Editor - Agent Guidance

## Key classes (auto-generated)

| Class | Base | Interfaces | Network | RPCs |
|-------|------|-----------|---------|------|
| `AnimatorGenerator` | `` | - | - | - |
| `AssetGenerator` | `` | - | - | - |
| `AudioClipGenerator` | `` | - | - | - |
| `BatchTestRunner` | `` | - | - | - |
| `Callbacks` | `` | ICallbacks | - | - |
| `BuildingMeshBuilder` | `` | - | - | - |
| `BuildScript` | `` | - | - | - |
| `CharacterGenerator` | `` | - | - | - |
| `EnvironmentGenerator` | `` | - | - | - |
| `InputHandlingBuildPreprocessor` | `` | IPreprocessBuildWithReport | - | - |
| `InputHandlingEditorEnforcer` | `` | - | - | - |
| `InputHandlingUtility` | `` | - | - | - |
| `MCPCommands` | `` | - | - | - |
| `MixamoDevEnforcer` | `` | - | - | - |
| `MixamoLocalAssets` | `` | - | - | - |
| `MixamoProcessor` | `` | - | - | - |
| `PostProcessingSetup` | `` | - | - | - |
| `PrefabGenerator` | `` | - | - | - |
| `SceneGenerator` | `` | - | - | - |
| `TerrainGenerator` | `` | - | - | - |
| `TMPEssentialsBootstrap` | `` | - | - | - |
| `UIGenerator` | `` | - | - | - |
| `UnityMCPServer` | `` | - | - | - |
| `ExecuteRequest` | `` | - | - | - |
| `VFXPrefabGenerator` | `` | - | - | - |

## Invariants

- Generation must be deterministic and safe to re-run.
- Batchmode compatibility is required.
- Do not break prefab/asset references; prefer repair strategies.
