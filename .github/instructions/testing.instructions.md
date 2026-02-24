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

## 4. Zero-Failure Enforcement

- **Any failing test (EditMode or PlayMode) is a blocking failure.** You MUST NOT proceed to the next task, report completion, or ask the user to test until ALL tests pass — zero failures, zero unhandled log errors.
- If a test starts failing because of your changes, fix it immediately before moving on.
- If a pre-existing test is failing, fix it as part of your current session — do not ignore it.

## 5. Feature-Specific AutoTester Scenarios

- **Every user-facing feature change MUST have a corresponding AutoTester scenario** that emits a PASS marker. Core sanity (movement, camera, grounding) is not sufficient.
- When implementing a new feature, add or extend an AutoTester scenario that validates the feature at runtime (e.g., building prefab counts, ability effects, UI presence).
- AutoTester uses `-autoTestScenario <name>` arguments. If `-autoTest` is present but no scenarios are specified, AutoTester runs ALL registered scenarios.
- When verifying your work, always run the autoTest with the scenario relevant to your feature and confirm its PASS marker in Player.log.

## 6. AI Self-Testing Loop

- You MUST NOT ask the user to test a build or verify a fix until you have tested it yourself.
- To test a build, use the terminal to run the Unity batchmode build command.
- Once built, launch the executable in the background using `Start-Process -FilePath "Builds\Windows\ByteWar.exe" -ArgumentList "-autoTest" -NoNewWindow`.
- The `-autoTest` flag will trigger the `AutoTester` script to simulate input and automatically close the game after a few seconds. DO NOT use OS-level input simulation (like `user32.dll` or `keybd_event`) as it steals focus and interrupts the user.
- Read the `Player.log` (located at `$env:USERPROFILE\AppData\LocalLow\BytedotGit\ByteWar\Player.log` on Windows) to verify that no exceptions were thrown and that the expected logs (e.g., "Server started", "Player spawned", "[AutoTester]") are present.
- **CRITICAL**: Keep iterating on the code and self-testing until you have achieved the task or resolved the issue.
- For example, if movement isn't working, identify the issue, resolve it, test the movement in-game (via logs or automated input) to confirm it's working as intended, and ONLY THEN ask the user to test. Apply this logic to everything.


