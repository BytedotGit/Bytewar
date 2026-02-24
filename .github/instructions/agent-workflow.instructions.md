---
description: Defines the decision tree, error recovery, escalation rules, and mandatory pre-flight checks for all AI agents working in this repository.
applyTo: "**/*"
---

# Agent Workflow Protocol

**CRITICAL**: Every AI agent MUST follow this protocol for every task. No exceptions.

## 1. Mandatory Pre-Flight (Do This FIRST)

Before writing ANY code, you MUST:

1. **Read the ROADMAP**: `.github/ROADMAP.md` — understand current project state and active focus.
2. **Read folder AGENTS.md**: For every folder you will modify, read its `AGENTS.md` file. These contain file inventories, invariants, and anti-patterns specific to that folder.
3. **Read relevant instruction files**: Check `.github/instructions/` for any instruction files that apply to the files you're modifying (check the `applyTo` patterns in their frontmatter).

**Conditional reads** (only when relevant):

- **If implementing gameplay features**: Read `.github/GAME_DESIGN.md`.
- **If your task relates to a specific infrastructure phase**: Read `.github/MASTER_PLAN.md`.

**If you skip pre-flight, your work will likely violate established conventions and require rework.**

## 2. Decision Tree

When given a task, follow this decision tree:

```
Is the task clearly defined with specific files/behavior to change?
├── YES → Implement it (with tests)
└── NO → Is it a design/balance/aesthetic question?
    ├── YES → Escalate to user (or Designer agent for RPG balance)
    └── NO → Is there a simpler interpretation?
        ├── YES → Implement the simpler option + log the alternative to TECH_DEBT.md
        └── NO → Ask the user for clarification (do NOT guess)
```

### When to Escalate to the User

Escalate ONLY for:

- **Aesthetic/taste choices**: visual style, color palettes, sound design, "how should this feel?"
- **Game design decisions**: "should we add feature X?" or "which approach is more fun?"
- **Scope changes**: anything that would add new phases, new systems, or change architectural direction
- **Licensing/legal**: third-party asset usage, package additions

Do NOT escalate for:

- Technical implementation details (pick the simpler, more conventional approach)
- Bug fixes (fix it)
- Test failures (fix immediately)
- Naming conventions (follow existing patterns in the codebase)

### Ambiguity Protocol

When a task has multiple valid implementations:

1. Choose the **simpler** option
2. Implement it fully (with tests)
3. Log the alternative approach in `TECH_DEBT.md` with rationale for why you chose the simpler one
4. Move on — do not block waiting for human input on implementation details

## 3. Error Recovery Protocol

When you encounter a failure, follow this protocol:

### Compile Failure

1. Read the error message carefully — identify the exact file and line
2. Fix the specific issue
3. Recompile
4. **Max 3 attempts** on the same approach. If it still fails:
   - Revert your change
   - Try an alternative approach
   - If the alternative also fails after 3 attempts → log to `.github/ERROR_LOG.md` and escalate to user

### Test Failure

1. Read the test output — identify which assertion failed and why
2. Determine: did your code break the test, or is the test wrong?
   - If your code broke it: fix your code
   - If the test expectation is outdated: update the test
   - If it's a pre-existing failure: fix it anyway — **zero test failures are tolerated, ever**
3. Re-run the specific test
4. **Max 3 attempts**. If still failing → `ERROR_LOG.md` + escalate

### AutoTester / Player.log Failure

1. Read Player.log for the exception or FAIL marker
2. Match the error to a specific system
3. Fix and rebuild
4. **Max 3 attempts** → `ERROR_LOG.md` + escalate

### Critical Rules

- **NEVER brute-force retry the same approach.** If attempt 1 failed, attempt 2 must try something different.
- **NEVER disable, skip, or delete a test** to make the build pass. Fix the underlying issue.
- **NEVER use `--no-verify`** or bypass safety checks.
- **After 3 failed attempts on the same issue**, you MUST:
  1. Log the issue in `.github/ERROR_LOG.md` with full context
  2. Stop working on that specific issue
  3. Inform the user with the error details and what you tried

## 4. Tracking & Documentation Updates

### When to update ROADMAP.md

- A phase or sub-phase is completed
- The current focus changes
- A new phase is started

### When to update CHANGELOG.md

- Any user-facing behavior changes
- Bug fixes
- New features
- System infrastructure changes visible to developers

### When to update ERROR_LOG.md

- A defect is found but not fully resolved in the current session
- Error recovery exceeded 3 attempts
- A user reports a bug

### When to update TECH_DEBT.md

- You notice a code smell outside your current scope
- You choose a simpler implementation and want to record the alternative
- You find a performance issue that isn't blocking

### When to update folder AGENTS.md

- You add new files to a folder
- You change class relationships or interfaces
- You introduce new invariants or patterns
- _Note_: Folder AGENTS.md files are auto-generated by `Tools/generate-agents-md.ps1`. Prefer regenerating over manual edits. Use `<!-- MANUAL -->` sections for hand-written invariants.

## 5. Specialized Agents

| Agent         | When to use                                                                                                                           |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| **Designer**  | RPG balance decisions: stat scaling, talent trees, crafting recipes, damage formulas. Provides mathematical justifications.           |
| **Architect** | System design, interface design, ECS vs MonoBehaviour decisions, cross-system dependency analysis.                                    |
| **Debugger**  | Player.log analysis, test failure diagnosis, root-cause analysis. Methodical: read error → identify file:line → propose fix → test.   |
| **Reviewer**  | Convention enforcement. Validates changes against architecture instructions: encapsulation, interfaces, 800 LOC limit, test coverage. |

## 6. Anti-Patterns (NEVER Do These)

- **Don't ask "should I add tests?"** — Always add tests. It's non-negotiable.
- **Don't refactor code outside your scope** — Log it to TECH_DEBT.md instead.
- **Don't add features beyond the task** — Scope containment is mandatory.
- **Don't skip the build/test loop** — Every change must be verified.
- **Don't ignore warnings** — Warnings are defects.
- **Don't guess at conventions** — Read the instruction files first.
- **Don't create files without checking if they already exist** — Search first.
- **Don't use `FindObjectOfType` in Update loops** — Use caching, registries, or events.
- **Don't add per-frame Debug.Log** — Throttle all runtime logging.
- **Don't tolerate pre-existing test failures** — If a test fails during your session, fix it regardless of whether you caused it. Test failures are never "unrelated".
