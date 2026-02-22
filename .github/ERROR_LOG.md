# Error Log

This file tracks known defects that are reproducible and not yet fully resolved.

Guidelines

- Keep entries short, actionable, and reproducible.
- Include: repro steps, expected vs actual, last known good, and the most relevant log lines.
- Update entries as the investigation progresses (don’t create duplicates).

---

## ERR-0001 — Player visuals float above ground

- Status: **Resolved** (2026-02-22)
- First observed: 2026-02-22
- Area: Player visuals / grounding
- Severity: Medium (visual feel / immersion)

**Root cause**

`VisualGroundingUtility.TrySampleVisualBottomY` preferred `SkinnedMeshRenderer.bounds.min.y` ("RendererBounds") over humanoid bone positions. Skinned mesh bounds include rigging/skinning padding and vary per animation pose, causing the grounding correction to under-shoot and leave a visible air gap.

**Fix applied**

- Inverted priority in `TrySampleVisualBottomY`: humanoid toe/foot bone positions are now the primary reference; renderer bounds are used only as fallback for non-humanoid rigs.
- Increased `desiredDelta` (sink bias) from `-0.015` to `-0.035` for more aggressive ground contact.
- Result: `totalOffset=0.219` (was ~0.025), `method=HumanoidBones` (was RendererBounds).

**Verification**

- EditMode tests: PASS (exit 0)
- PlayMode tests: 15/15 PASS
- Build: success
- AutoTest: all 7 checks PASS, `method=HumanoidBones`, no exceptions
