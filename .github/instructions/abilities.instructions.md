---
description: Instructions for creating new spells and talents using the custom Ability System.
applyTo: "Assets/Scripts/Abilities/**/*.cs"
---

# Ability System Guidelines

When creating new abilities or talents, adhere to the following rules:

## Abilities

- All abilities must inherit from the base `Ability` class (which should be a `ScriptableObject`).
- Abilities must define their cost (Mana/Stamina), cooldown, and execution logic.
- Execution logic must be server-authoritative. Clients should only predict or request the execution via `ServerRpc`.
- Use `GameplayEffect`s to apply damage, healing, or status changes. Do not modify attributes directly in the ability logic.

## Talents

- Talents are implemented as `ScriptableObject`s.
- A talent should define its prerequisites (if any), max rank, and the specific modifiers it applies to the player's `AttributeSet` or specific `Ability`.
- When a talent is learned, its effects should be applied via the `AbilitySystemComponent`.

## Effects

- `GameplayEffect`s should handle duration (instant, duration, infinite) and modifiers (add, multiply, override).
- Ensure effects are properly synchronized across the network if they have visual components.

## Testing

- Add detailed `Debug.Log` statements in `Execute` and `ApplyTalent` methods to track execution flow, mana consumption, and cooldown triggers.
