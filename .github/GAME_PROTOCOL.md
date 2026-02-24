# GAME_PROTOCOL.md — Quality Checklist (Bytewar)

> For the full Definition of Done, build commands, and critical rules, see `.github/copilot-instructions.md`.
> For gameplay systems, economy, combat, and design invariants, see `.github/GAME_DESIGN.md`.

## Quick Checklist

A change is Done only if:

- [ ] Code compiles with ZERO warnings
- [ ] All EditMode tests pass
- [ ] All PlayMode tests pass
- [ ] Client builds successfully
- [ ] AutoTester passes with feature-specific PASS markers (if user-facing change)
- [ ] Player.log has no exceptions
- [ ] ROADMAP.md updated (if milestone changed)
- [ ] CHANGELOG.md updated (if user-facing behavior changed)
- [ ] Relevant folder AGENTS.md invariants still hold
