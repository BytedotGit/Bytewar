# Core - Agent Guidance

## Key classes (auto-generated)

| Class                             | Base               | Interfaces        | Network | RPCs                                                                                                     |
| --------------------------------- | ------------------ | ----------------- | ------- | -------------------------------------------------------------------------------------------------------- |
| `AudioManager`                    | `MonoBehaviour`    | -                 | -       | -                                                                                                        |
| `AutoTester`                      | `MonoBehaviour`    | -                 | -       | -                                                                                                        |
| `AutoTestLogFilterHandler`        | ``                 | ILogHandler       | -       | -                                                                                                        |
| `AutoTesterContext`               | ``                 | -                 | -       | -                                                                                                        |
| `AutoTestScenarioBuildingVisuals` | ``                 | IAutoTestScenario | -       | -                                                                                                        |
| `AutoTestScenarioConsole`         | ``                 | IAutoTestScenario | -       | -                                                                                                        |
| `AutoTestScenarioCore`            | ``                 | IAutoTestScenario | -       | -                                                                                                        |
| `AutoTestScenarioDestruction`     | ``                 | IAutoTestScenario | -       | -                                                                                                        |
| `FootstepController`              | `MonoBehaviour`    | -                 | -       | -                                                                                                        |
| `GameConstants`                   | `ScriptableObject` | -                 | -       | -                                                                                                        |
| `GameEvent`                       | ``                 | -                 | -       | -                                                                                                        |
| `GameEventBus`                    | ``                 | -                 | -       | -                                                                                                        |
| `EnemyDiedEvent`                  | ``                 | -                 | -       | -                                                                                                        |
| `EnemyTargetedEvent`              | ``                 | -                 | -       | -                                                                                                        |
| `ItemEvent`                       | ``                 | -                 | -       | -                                                                                                        |
| `BuildingPlacedEvent`             | ``                 | -                 | -       | -                                                                                                        |
| `GameStateChangedEvent`           | ``                 | -                 | -       | -                                                                                                        |
| `GameManager`                     | `NetworkBehaviour` | -                 | Yes     | -                                                                                                        |
| `GameStateMachine`                | ``                 | -                 | -       | -                                                                                                        |
| `PlayerInputHandler`              | `MonoBehaviour`    | -                 | -       | -                                                                                                        |
| `PlayerInteraction`               | `NetworkBehaviour` | -                 | Yes     | AttackEnemyServerRpc, GatherResourceServerRpc, RequestDestructibleDamageServerRpc, PlayMeleeHitClientRpc |
| `PlayerRegistry`                  | ``                 | -                 | -       | -                                                                                                        |
| `ScreenLogger`                    | `MonoBehaviour`    | -                 | -       | -                                                                                                        |
| `Entry`                           | ``                 | -                 | -       | -                                                                                                        |
| `ThirdPersonCamera`               | `MonoBehaviour`    | -                 | -       | -                                                                                                        |
| `VFXManager`                      | `NetworkBehaviour` | -                 | Yes     | PlayEffectClientRpc                                                                                      |
| `VisualGroundingUtility`          | ``                 | -                 | -       | -                                                                                                        |

## Invariants

- Input must use the new Input System; input regressions are treated as P0.
- Camera must not allocate per frame.
- No per-frame Debug.Log in camera or input loops (throttle logs).

## Required validations when changing Core

- Extend `AutoTester` when the playable build flow changes.
- Add a regression test if you change camera pivot math or grounding behavior.
