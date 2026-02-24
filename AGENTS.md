# Bytewar — Agent Execution Contract

This repository is designed to be developed with GitHub Copilot Chat.

**Rules & Quality Gates**: See `.github/copilot-instructions.md` — the single source of truth for critical rules, Definition of Done, and build commands.

**ROADMAP**: Consult `.github/ROADMAP.md` for current project state. Update it when a significant feature, phase, or bugfix is completed.

**GAME_DESIGN**: Consult `.github/GAME_DESIGN.md` before implementing any gameplay feature.

**Testing**: See `.github/instructions/testing.instructions.md` for test coverage, smoke tests, logging, and self-testing loop details.

## Build/self-test loop (mandatory)

1. Build the game (batchmode) — see build commands in `copilot-instructions.md`.
2. Launch the executable with `-autoTest` (e.g., `Start-Process -FilePath "Builds\Windows\ByteWar.exe" -ArgumentList "-autoTest" -NoNewWindow`). DO NOT use OS-level input simulation that steals focus.
3. Wait for `AutoTester` to finish and close the game, then inspect Player.log for:
   - no exceptions
   - PASS markers for the feature being changed
4. Keep iterating until the fix/feature is verified. ONLY THEN let the user know to test.

## MCP tooling

- Unity automation: `Tools/UnityMCP/`
- Blender asset automation (headless): `Tools/BlenderMCP/`

## General Unity C# Conventions

- Use modern C# features where applicable.
- Prefer `SerializeField` over `public` for inspector variables.
- Keep `Update` methods clean; use events or coroutines for complex logic.
- Use `ScriptableObject`s for data-driven design (e.g., items, talents, abilities).

## Networking (Netcode for GameObjects — NGO)

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
- See `.github/instructions/abilities.instructions.md` for detailed ability/talent creation rules.
