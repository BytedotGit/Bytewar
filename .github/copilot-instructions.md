# Unity & NGO Guidelines

**CRITICAL AI DIRECTIVE**: All AI agents working on this repository MUST proactively write tests, smoke tests, and extensive debugging logs for any code they generate or modify. Do not wait for the user to ask. See `.github/instructions/testing.instructions.md` for strict enforcement rules.

**AI SELF-TESTING LOOP**: Before asking the user to test a build or verify a fix, you MUST:

1. Build the game yourself.
2. Launch the executable in a background terminal using the `-autoTest` argument (e.g., `Start-Process -FilePath "Builds\Windows\SurvivalRPG.exe" -ArgumentList "-autoTest" -NoNewWindow`). DO NOT use OS-level input simulation that steals focus.
3. Wait a few seconds for the `AutoTester` to finish and close the game, then read the `Player.log` to verify that the game runs without exceptions and that your specific fix/feature is working as intended (e.g., by checking specific debug logs you added).
4. Keep iterating on the code and self-testing until you have achieved the task or resolved the issue.
5. ONLY THEN let the user know to test it themselves.

**ROADMAP**: Always consult `.github/ROADMAP.md` to understand the current project state and the next steps required to reach a playable build.
**CRITICAL**: You MUST automatically update `.github/ROADMAP.md` whenever a significant feature, phase, or bugfix is completed. Do not wait for the user to ask you to update the roadmap. Keep it accurate and reflective of the current state of the project.

## General Unity C# Conventions

- Use modern C# features where applicable.
- Prefer `SerializeField` over `public` for inspector variables.
- Keep `Update` methods clean; use events or coroutines for complex logic.
- Use `ScriptableObject`s for data-driven design (e.g., items, talents, abilities).

## Testing, Debugging & Maintainability

- **100% Test Coverage**: All new features and systems must include comprehensive unit tests and PlayMode tests to achieve 100% coverage.
- **Smoke Tests**: Create smoke tests for critical paths (e.g., player spawning, basic combat, crafting) to ensure end-to-end stability.
- **Debugging & Visibility**: Include extensive debugging and logging (`Debug.Log`, `Debug.LogWarning`, `Debug.LogError`) for all implemented logic to ensure full visibility when things go wrong. Include context (e.g., GameObject name, NetworkObjectId).
- **File Size Limit**: No single file should exceed 800 lines of code (LOC). Refactor and split classes to maintain readability and maintainability.

## Networking (Netcode for GameObjects - NGO)

- Use `NetworkBehaviour` instead of `MonoBehaviour` for networked objects.
- Use `NetworkVariable` for state synchronization (e.g., Health, Mana).
- Use `ServerRpc` for client-to-server requests (e.g., casting a spell, placing a building).
- Use `ClientRpc` for server-to-client broadcasts (e.g., playing visual effects).
- Ensure critical logic (damage, spawning, inventory) is server-authoritative.

## Custom Ability System Architecture

- The game uses a lightweight, custom implementation of the Gameplay Ability System (GAS).
- `AbilitySystemComponent` manages a player's spells, cooldowns, and effects.
- `AttributeSet` manages stats like Health, Mana, and Stamina.
- `GameplayEffect` handles buffs, debuffs, and damage over time.
