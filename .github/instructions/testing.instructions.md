---
description: Enforces 100% test coverage, smoke tests, debugging, and maintainability rules for all C# scripts.
applyTo: "Assets/Scripts/**/*.cs"
---

# Testing, Debugging, and Maintainability Rules

**CRITICAL AI DIRECTIVE**: As an AI agent working on this repository, you MUST adhere to the following rules for EVERY C# script you create or modify. Do not wait for the user to ask for tests or logs; proactively generate them.

## 1. 100% Test Coverage

- Whenever you implement a new feature, class, or method, you MUST simultaneously create or update the corresponding unit tests in `Assets/Scripts/Tests/EditMode` or `Assets/Scripts/Tests/PlayMode`.
- Ensure edge cases and network state (NGO `IsServer` / `IsClient`) are accounted for in PlayMode tests.

## 2. Smoke Tests

- If you are implementing a critical path or core gameplay loop (e.g., combat, resource gathering, building, inventory), you MUST create a PlayMode smoke test to verify the end-to-end flow.

## 3. Extensive Debugging

- You MUST include extensive logging (`Debug.Log`, `Debug.LogWarning`, `Debug.LogError`) in all implemented logic.
- Logs must provide full visibility into the state and flow, especially for networked events (e.g., include `NetworkObjectId`, `ClientId`, and GameObject names).

## 4. File Size Limits (800 LOC)

- No single file should ever exceed 800 lines of code.
- If your implementation pushes a file near or over this limit, you MUST refactor and split the class into smaller, modular components.

## 5. AI Self-Testing Loop

- You MUST NOT ask the user to test a build or verify a fix until you have tested it yourself.
- To test a build, use the terminal to run the Unity batchmode build command.
- Once built, launch the executable in the background using `Start-Process -FilePath "Builds\Windows\SurvivalRPG.exe" -ArgumentList "-autoTest" -NoNewWindow`.
- The `-autoTest` flag will trigger the `AutoTester` script to simulate input and automatically close the game after a few seconds. DO NOT use OS-level input simulation (like `user32.dll` or `keybd_event`) as it steals focus and interrupts the user.
- Read the `Player.log` (located at `$env:USERPROFILE\AppData\LocalLow\DefaultCompany\SurvivalRPG\Player.log` on Windows) to verify that no exceptions were thrown and that the expected logs (e.g., "Server started", "Player spawned", "[AutoTester]") are present.
- **CRITICAL**: Keep iterating on the code and self-testing until you have achieved the task or resolved the issue.
- For example, if movement isn't working, identify the issue, resolve it, test the movement in-game (via logs or automated input) to confirm it's working as intended, and ONLY THEN ask the user to test. Apply this logic to everything.
