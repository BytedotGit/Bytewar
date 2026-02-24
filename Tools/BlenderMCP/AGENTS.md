# BlenderMCP — Agent Guidance

## Invariants

- Bridge must bind to localhost only (no remote access).
- Blender automation must be deterministic and safe to re-run.
- Do not download or embed third-party assets automatically.

## Safety

- Prefer explicit tool functions over `blender_execute_script`.
- Do not allow network access from `blender_execute_script`.
