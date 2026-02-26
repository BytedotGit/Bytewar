# Agent Regression Checklist

Use this checklist after any edit to agent files or routing policy so custom-agent behavior stays stable.

## Run Trigger

Run this checklist when any of these change:

- `.github/agents/*.agent.md`
- `.github/copilot-instructions.md`
- `.github/instructions/agent-workflow.instructions.md`
- `AGENTS.md` routing matrix

## Expected Agent Inventory & Model Pins

| Agent | Expected Primary Model | Fallback | Notes |
| --- | --- | --- | --- |
| `BuildOps` | `GPT-5.3-Codex` | `Claude Sonnet 4.6` | CI/build/test artifact triage and release gates |
| `Architect` | `GPT-5.3-Codex` | `Claude Sonnet 4.6` | Architecture and dependency validation |
| `Debugger` | `Claude Opus 4.6` | `Claude Sonnet 4.6` | Must not route to `GPT-5.3-Codex` |
| `Designer` | `Gemini 3.1 Pro (Preview)` | `Claude Sonnet 4.6` | Includes gameplay + UI/UX + 3D trade-offs |
| `Search` | `Gemini 3 Flash (Preview)` | `Claude Sonnet 4.6` | First-pass symbol/file discovery |
| `Explore` | `Gemini 3 Flash (Preview)` | `Claude Sonnet 4.6` | Broad context/dependency synthesis |
| `Reviewer` | `GPT-5.3-Codex` | `Claude Sonnet 4.6` | Convention and release-quality audit |

## One-Pass Validation

### 1) Static Policy Sync Checks

- [ ] Confirm all expected `.agent.md` files exist in `.github/agents/`.
- [ ] Confirm model-routing statements are consistent across:
  - `.github/copilot-instructions.md`
  - `.github/instructions/agent-workflow.instructions.md`
  - `AGENTS.md`
- [ ] Confirm no contradictory rule exists for Debugger model routing.
- [ ] Confirm Search/Explore boundary guidance is present and consistent.

### 2) Parallel Smoke Invocation (All Agents)

Run all custom agents in parallel with short read-only smoke prompts.

- [ ] `BuildOps`: returns GO/NO-GO format with Blocking Issues + Benign Warnings.
- [ ] `Architect`: returns architecture-focused guardrail output.
- [ ] `Debugger`: reports pinned model + fallback + disallowed Codex route.
- [ ] `Designer`: returns output including Values + UX Analysis + Visual/3D Analysis.
- [ ] `Search`: returns fast lookup behavior and escalation condition to Explore.
- [ ] `Explore`: returns deep-context usage criteria distinct from Search.
- [ ] `Reviewer`: returns convention/test/documentation audit output.

### 3) Pass Criteria

- [ ] All agents invoke successfully (no invocation errors).
- [ ] Output behavior matches each role definition.
- [ ] Model pins/fallback behavior are reflected in policy and agent outputs.
- [ ] No changed file reports markdown/config errors.

## Failure Handling

If any item fails:

1. Fix the smallest policy or agent-file mismatch.
2. Re-run only affected checks.
3. Re-run full parallel smoke pass once all targeted fixes are complete.

## Evidence Template

Record results in PR/session notes:

- Date:
- Files changed:
- Static checks: pass/fail
- Parallel smoke checks: pass/fail per agent
- Remaining gaps:
- Final status: `GO` / `NO-GO`
