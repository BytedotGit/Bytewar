# Docs - Agent Guidance

## Purpose

- `Assets/Scripts/Docs/` stores implementation specs that guide runtime/system work.
- Treat these docs as engineering contracts: update them when behavior or architecture changes.

## Current specs

- `HybridGearVisibilitySpec.md`
- `ServerAuthoritativeDestructionSpec.md`

## Rules

- Keep scope-specific specs concise and implementation-focused.
- When code diverges from a spec, update the spec in the same change.
- Do not put temporary planning notes here; use repository docs or session memory for transient plans.
