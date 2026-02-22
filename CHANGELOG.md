# Changelog

All notable changes to this project are documented here.

## Unreleased

## v0.1.1 - 2026-02-22

- AutoTester: reduce flaky UTP port bind failures via UDP port probing + small backoff between retries; temporarily suppress known NGO shutdown warning spam during retries (counted and reported).
- Build pipeline: generator/build logs standardized with a greppable `[BuildGen]` prefix (BuildScript + generators).

## v0.1.0 - 2026-02-22

- Added execution protocol + per-folder AGENTS guidance + Git LFS + Unity gitignore.
- Phase 13.1 feel hotfix: camera pivot de-stacking, deterministic grounding, and action-bar animation gating.
- AutoTester expanded to validate camera pivot sanity + grounding sanity in builds.
- AutoTester hardened: deterministic host startup for `-autoTest` (HUD auto-host disabled, random high port + retries) and animation sanity (fails fast on missing controller / T-pose regressions).
