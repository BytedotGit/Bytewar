---
description: Defines architectural conventions, design patterns, and code quality standards for all runtime C# code in ByteWar.
applyTo: "Assets/Scripts/**/*.cs"
---

# Architecture Instructions

## 1. Interface-First Design

Every runtime class that participates in gameplay interactions MUST implement at least one interface from `Assets/Scripts/Core/Interfaces/`.

### Existing interfaces

| Interface | Purpose | Required methods |
|-----------|---------|-----------------|
| `IDamageable` | Any entity that can take damage | `void TakeDamage(float amount, ulong instigatorClientId)` |
| `IInteractable` | Any entity a player can interact with | `bool CanInteract(ulong clientId)`, `void Interact(ulong clientId)` |
| `IInventoryHolder` | Any entity that holds items | `bool HasItem(Item)`, `void AddItem(Item)`, `bool RemoveItem(Item)` |
| `ICombatTarget` | Any entity that can be targeted in combat | `AttributeSet Attributes { get; }`, `bool IsAlive { get; }` |
| `IPersistable` | Any entity whose state is saved to disk | `string Serialize()`, `void Deserialize(string)` |
| `IGameState` | State machine states | `void Enter()`, `void Exit()`, `void Tick()` |
| `IAbilityExecutor` | Manages ability execution | Defined in `IAbilityExecutor.cs` |

### When to create a new interface

Create a new interface when:
- Two or more classes share a behavioral contract
- You need to decouple systems (e.g., UI reading data without knowing the concrete provider)
- You want to enable polymorphic interaction (raycast hits something — what can we do with it?)

### Template: Adding an interface

```csharp
// Assets/Scripts/Core/Interfaces/IRepairable.cs
namespace ByteWar
{
    public interface IRepairable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool NeedsRepair { get; }
        void Repair(float amount, ulong repairerClientId);
    }
}
```

Then implement on the class:
```csharp
// In BuildingPiece.cs
public class BuildingPiece : NetworkBehaviour, IDamageable, IRepairable
{
    public float CurrentHealth => _health.Value;
    public float MaxHealth => _maxHealth;
    public bool NeedsRepair => _health.Value < _maxHealth;

    public void Repair(float amount, ulong repairerClientId)
    {
        if (!IsServer) return;
        _health.Value = Mathf.Min(_health.Value + amount, _maxHealth);
        Debug.Log($"[BuildingPiece] Repaired by client {repairerClientId}, health: {_health.Value}/{_maxHealth}");
    }
}
```

## 2. Encapsulation Rules

### Rule: No public mutable fields in runtime code

All fields exposed to the Inspector MUST use `[SerializeField] private`:

```csharp
// ❌ BAD
public float moveSpeed = 5f;
public List<Item> items = new();

// ✅ GOOD
[SerializeField] private float _moveSpeed = 5f;
[SerializeField] private List<Item> _items = new();

public float MoveSpeed => _moveSpeed;
public IReadOnlyList<Item> Items => _items;
```

### Rule: Collections are read-only externally

```csharp
// ❌ BAD — caller can .Add(), .Remove(), .Clear()
public List<Ability> LearnedAbilities;

// ✅ GOOD — caller can only read
[SerializeField] private List<Ability> _learnedAbilities = new();
public IReadOnlyList<Ability> LearnedAbilities => _learnedAbilities;
```

### Rule: ScriptableObject fields

ScriptableObjects (Ability, Item, Talent, GameplayEffect, BuildingRecipe) define *data*. Their fields should be `[SerializeField] private` with read-only getters. They should NEVER contain mutable runtime state.

```csharp
// ✅ GOOD ScriptableObject pattern
[CreateAssetMenu(menuName = "ByteWar/Abilities/NewAbility")]
public class Ability : ScriptableObject
{
    [SerializeField] private string _abilityName;
    [SerializeField] private float _manaCost;
    [SerializeField] private float _cooldown;
    [SerializeField] private Sprite _icon;

    public string AbilityName => _abilityName;
    public float ManaCost => _manaCost;
    public float Cooldown => _cooldown;
    public Sprite Icon => _icon;
}
```

## 3. Class Size & Single Responsibility

### Rule: 800 LOC maximum per file

If a class exceeds 800 lines, refactor by extracting responsibilities into new classes.

### Extraction pattern

When extracting from a god class (like `NetworkPlayer`):
1. Identify a cohesive responsibility (e.g., movement, visual setup, combat)
2. Create a new `MonoBehaviour` (or `NetworkBehaviour` if it needs network state)
3. Move the fields, methods, and logic for that responsibility
4. The original class coordinates via `GetComponent<>()` or direct reference
5. Use `[RequireComponent]` if the extracted class is always needed alongside the parent

```csharp
// ✅ GOOD — extracted responsibility
[RequireComponent(typeof(NetworkPlayer))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _gravity = -20f;

    private CharacterController _controller;

    public override void OnNetworkSpawn()
    {
        _controller = GetComponent<CharacterController>();
    }

    public void HandleMovement(Vector2 input, bool jump)
    {
        // movement logic here
    }
}
```

## 4. Magic Number Prohibition

No magic numbers in runtime code. All tuning values MUST come from:
1. **`GameConstants` ScriptableObject** — for global game values (move speed, gravity, frame rate cap)
2. **Per-system ScriptableObjects** — for system-specific values (ability damage, recipe costs)
3. **`[SerializeField]` fields** — for component-specific values tunable in the Inspector

```csharp
// ❌ BAD
float damage = 25f;
if (distance < 10f)

// ✅ GOOD
[SerializeField] private float _damage = 25f;
[SerializeField] private float _attackRange = 10f;
```

Exception: mathematical constants (`0`, `1`, `-1`, `0.5f`, `Mathf.PI`), array indices, and comparison thresholds in tests.

## 5. State Management

### GameStateMachine

The `GameStateMachine` in `Assets/Scripts/Core/GameStateMachine.cs` manages top-level game state. States implement `IGameState`.

States: `Initializing → MainMenu → Connecting → Loading → Playing → Paused → Disconnected`

When adding new game-level state transitions, modify the state machine — do NOT add boolean flags to `GameManager`.

## 6. Event Communication

### Cross-System Events → `GameEventBus`

Use `GameEventBus` (in `Assets/Scripts/Core/GameEventBus.cs`) for communication between unrelated systems:

```csharp
// Publishing
GameEventBus.EnemyDied?.Invoke(enemyId);

// Subscribing (in OnEnable/OnDisable)
void OnEnable() => GameEventBus.EnemyDied += OnEnemyDied;
void OnDisable() => GameEventBus.EnemyDied -= OnEnemyDied;
```

### Parent-Child Events → Instance events

For communication between a class and its direct dependents, use regular C# events:

```csharp
public event System.Action<float> OnHealthChanged;
```

### Do NOT use:
- `SendMessage` / `BroadcastMessage` — too fragile, no type safety
- Static events on gameplay classes — use `GameEventBus` instead
- Polling in Update — use events or callbacks

## 7. Registry Pattern

For frequently queried entity sets (players, enemies, buildings), use the registry pattern:

```csharp
// PlayerRegistry.cs pattern
public static class PlayerRegistry
{
    private static readonly HashSet<Transform> _players = new();

    public static void Register(Transform player) => _players.Add(player);
    public static void Unregister(Transform player) => _players.Remove(player);

    public static Transform GetNearest(Vector3 position)
    {
        // find nearest without FindGameObjectsWithTag
    }
}
```

Register in `OnNetworkSpawn`, unregister in `OnNetworkDespawn`.

## 8. Performance Anti-Patterns

### NEVER do these in Update/FixedUpdate/Tick:
- `FindObjectOfType` / `FindObjectsByType` / `FindGameObjectsWithTag`
- `Physics.OverlapSphere` without caching the results array
- `string` concatenation (use string interpolation or `StringBuilder`)
- `Instantiate` / `Destroy` (use object pooling)
- `Debug.Log` without throttling (use a frame counter or timer)
- `GetComponent<T>()` — cache in Awake/Start/OnNetworkSpawn

### DO:
- Cache component references in `Awake()` / `Start()` / `OnNetworkSpawn()`
- Use registries (`PlayerRegistry`) or events for entity discovery
- Use `UnityEngine.Pool.ObjectPool<T>` for frequently spawned objects
- Use `[NonSerialized]` for runtime-only fields
- Use `#if UNITY_EDITOR` for editor-only debug visualization

## 9. Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Private fields | `_camelCase` | `_moveSpeed` |
| Public properties | `PascalCase` | `MoveSpeed` |
| Methods | `PascalCase` | `HandleMovement()` |
| Interfaces | `IPascalCase` | `IDamageable` |
| ScriptableObjects | PascalCase, descriptive | `FireballAbility` |
| Test methods | `MethodName_Scenario_ExpectedResult` | `TakeDamage_WhenDead_DoesNothing` |
| Constants | `PascalCase` or `UPPER_SNAKE` | `MaxHealth` or `MAX_HEALTH` |
| Enums | PascalCase | `BuildingType.Foundation` |
| Events | `On` prefix for instance, descriptive for bus | `OnHealthChanged`, `GameEventBus.EnemyDied` |

## 10. Folder Structure

```
Assets/Scripts/
├── Abilities/          # Ability system: abilities, talents, effects
│   └── Mage/           # Class-specific abilities
│   └── Warrior/
│   └── Talents/
├── Building/           # Building system: placement, pieces, recipes, persistence
├── Core/               # Core systems: input, camera, state machine, events, constants
│   └── Interfaces/     # All interfaces
├── Editor/             # Editor-only: generators, build scripts, processors
├── Networking/         # NGO: NetworkPlayer, movement, visual setup, bootstrapper
├── Survival/           # Survival systems: enemies, items, resources, equipment
├── UI/                 # All UI components
└── Tests/
    ├── EditMode/       # Pure logic tests, prefab/config integrity
    └── PlayMode/       # End-to-end flows, networked behavior
```

New folders require:
1. An `AGENTS.md` file (auto-generated via `Tools/generate-agents-md.ps1`)
2. An `.asmdef` file if the folder has unique assembly dependencies
