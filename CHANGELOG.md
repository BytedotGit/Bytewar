# Changelog

All notable changes to this project are documented here.

## Unreleased

- Added execution protocol + per-folder AGENTS guidance + Git LFS + Unity gitignore.
- Phase 13.1 feel hotfix: camera pivot de-stacking, deterministic grounding, and action-bar animation gating.
- AutoTester expanded to validate camera pivot sanity + grounding sanity in builds.
- AutoTester hardened: deterministic host startup for `-autoTest` (HUD auto-host disabled, random high port + retries) and animation sanity (fails fast on missing controller / T-pose regressions).
