# Core — Agent Guidance

Core includes input, camera, and automated test harnesses.

## Invariants
- Input must use the new Input System; input regressions are treated as P0.
- Camera must not allocate per frame.
- No per-frame Debug.Log in camera or input loops (throttle logs).

## Required validations when changing Core
- Extend `AutoTester` when the playable build flow changes.
- Add a regression test if you change camera pivot math or grounding behavior.
