# ByteWar — Global Copilot Instructions

> These instructions apply to every Copilot interaction in this repository.
> This file is the **single source of truth** for project-wide rules. Do not duplicate these rules elsewhere.

## Project Identity

- **Project name**: ByteWar
- **Company**: BytedotGit
- **Engine**: Unity 6 (6000.3.x) with Netcode for GameObjects (NGO)
- **Target**: PC (Windows), multiplayer survival RPG with dedicated server
- **Namespace**: `ByteWar`

## Pre-Flight (Before Writing Code)

1. **Read `.github/ROADMAP.md`** — current project state and active focus.
2. **Read the folder `AGENTS.md`** for each folder you will modify — file inventories, invariants, anti-patterns.
3. **If implementing gameplay features**, also read `.github/GAME_DESIGN.md`.
4. **If your task relates to a specific infrastructure phase**, also read `.github/MASTER_PLAN.md`.

Files in `.github/instructions/` are auto-attached by VS Code based on `applyTo` patterns — you do NOT need to manually read them.

## Instruction Files (auto-attached)

| File                             | Scope                                                               |
| -------------------------------- | ------------------------------------------------------------------- |
| `architecture.instructions.md`   | `Assets/Scripts/**/*.cs` — interfaces, encapsulation, patterns      |
| `testing.instructions.md`        | `Assets/Scripts/**/*.cs` — test coverage, smoke tests, logging      |
| `abilities.instructions.md`      | `Assets/Scripts/Abilities/**/*.cs` — ability/talent/effect creation |
| `agent-workflow.instructions.md` | `**/*` — decision tree, error recovery, escalation                  |

## Definition of Done

Every change MUST pass ALL of the following before being considered complete:

1. **Compiles** — zero errors, zero warnings
2. **EditMode tests pass** — `BatchTestRunner.RunEditModeTests` exits 0 with **zero failures** (including pre-existing). Any failure is blocking — fix it before proceeding.
3. **PlayMode tests pass** — Unity `-runTests -testPlatform PlayMode` exits 0 with **zero failures** (including pre-existing)
4. **Builds** — `BuildScript.BuildWindowsClient` produces `Builds/Windows/ByteWar.exe`
5. **AutoTester** — `ByteWar.exe -autoTest` runs, Player.log contains feature PASS markers, no exceptions (required for user-facing changes; optional for doc-only or test-only changes)
6. **Docs updated** — ROADMAP (if milestone changed), CHANGELOG (if user-facing behavior changed), relevant folder AGENTS.md (if file inventory changed)

## Critical Rules

1. **No public mutable fields** — use `[SerializeField] private` + read-only properties
2. **Interface-first** — implement from `Assets/Scripts/Core/Interfaces/`
3. **800 LOC max** per file — extract responsibilities if exceeded
4. **No magic numbers** — use `[SerializeField]`, `GameConstants`, or ScriptableObjects
5. **Server-authoritative** — damage, spawning, inventory MUST be on server
6. **No per-frame allocations** — cache components, use pools, avoid `Find*` in Update
7. **Test everything** — 100% coverage for changed code (EditMode + PlayMode). **Zero test failures tolerated** — fix pre-existing failures encountered during your session. See `testing.instructions.md` for details.
8. **Warnings are defects** — compiler warnings and missing references are blocking failures
9. **Anti-refactor drift** — only touch files required for the current scope. Log unrelated issues to `TECH_DEBT.md`.
10. **Atomic state sync** — code + docs + tracking updated together

## Agent Escalation

| Situation                        | Action                                                   |
| -------------------------------- | -------------------------------------------------------- |
| Unclear task                     | Re-read instruction files, infer intent, proceed         |
| Aesthetic / visual judgment      | Escalate to user                                         |
| Game design decision             | Delegate to Designer agent                               |
| Architecture question            | Consult `architecture.instructions.md`                   |
| Unrelated issue found (non-test) | Add to `TECH_DEBT.md`, do not fix                        |
| Pre-existing test failure        | Fix it immediately — test failures are never "unrelated" |
| Failing test after 3 attempts    | Log to `ERROR_LOG.md`, escalate to user                  |

## Build & Test Commands

```powershell
# EditMode tests
& "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . `
  -executeMethod ByteWar.Editor.BatchTestRunner.RunEditModeTests `
  -logFile -

# PlayMode tests
& "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" `
  -runTests -projectPath . -testPlatform PlayMode `
  -testResults "PlayModeTestResults.xml" `
  -batchmode -nographics -logFile "Logs/playmode_tests.log"

# Build client
& "C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . `
  -executeMethod ByteWar.Editor.BuildScript.BuildWindowsClient `
  -logFile "Logs/build.log"

# AutoTest
Start-Process -FilePath "Builds\Windows\ByteWar.exe" `
  -ArgumentList "-autoTest" -NoNewWindow

# Check Player.log (wait ~60s for AutoTester to complete)
$logPath = "$env:USERPROFILE\AppData\LocalLow\BytedotGit\ByteWar\Player.log"
Get-Content $logPath | Select-String "(PASS|FAIL|Exception|AutoTester)" | Select-Object -Last 30
```
