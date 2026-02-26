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

## Practical Agent Routing Matrix

Use this quick matrix to choose the right custom agent before starting work.

| Task Type | Agent to Invoke | Why |
| --- | --- | --- |
| CI/build/test artifact triage and release gate checks | `BuildOps` | Enforces evidence-based go/no-go decisions from logs and test artifacts |
| Large architecture changes, interface decisions, dependency flow | `Architect` | Best for deep cross-file design and guardrail checks |
| Build/test/runtime failures, stack traces, Player.log triage | `Debugger` | Focused root-cause workflow for iterative error recovery |
| UI/UX decisions, gameplay balance, 3D/design trade-offs | `Designer` | Best fit for design reasoning and value tuning |
| Fast discovery of files/symbols/usages (narrow lookup) | `Search` | Lowest-cost, fastest read/search pass |
| Broad read-only exploration and context mapping (quick/medium/thorough) | `Explore` | Structured deep discovery when Search results need synthesis and dependency mapping |
| PR audits, convention checks, docs/test coverage verification | `Reviewer` | Final quality gate against repo standards |

If a preferred model is temporarily unavailable, use the same agent role and rely on the documented fallback policy in `.github/copilot-instructions.md`.

For repeatable post-change validation of agent models/routing, run `.github/AGENT_REGRESSION_CHECKLIST.md`.

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
