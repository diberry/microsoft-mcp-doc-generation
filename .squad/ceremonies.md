# Ceremonies

> Team meetings that happen before or after work. Each squad configures their own.

## Design Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | before |
| **Condition** | multi-agent task involving 2+ agents modifying shared systems |
| **Facilitator** | lead |
| **Participants** | all-relevant |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Review the task and requirements
2. Agree on interfaces and contracts between components
3. Identify risks and edge cases
4. Assign action items

---

## Retrospective

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | build failure, test failure, or reviewer rejection |
| **Facilitator** | lead |
| **Participants** | all-involved |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. What happened? (facts only)
2. Root cause analysis
3. What should change?
4. Action items for next iteration

---

## Blocker Resolution Re-Review

| Field | Value |
|-------|-------|
| **Trigger** | auto |
| **When** | after |
| **Condition** | team review returns blockers and all blockers are fixed |
| **Facilitator** | coordinator |
| **Participants** | all-relevant (same reviewers as initial review) |
| **Time budget** | focused |
| **Enabled** | ✅ yes |

**Agenda:**
1. Verify each blocker is resolved (not just acknowledged)
2. Check that fixes didn't introduce new issues
3. Confirm non-blocking nits from initial review are addressed or explicitly deferred
4. Post consolidated re-review verdict on the PR

**Process:**
- Fix all blockers in a single commit (or minimal commits)
- Push to the same PR branch
- Re-run the full team review with the same reviewers
- If new blockers emerge, repeat the cycle
- Cosmetic nits found in re-review may be fixed in a follow-up commit without another full re-review

**Why this works:** PR #785 demonstrated the value — initial review caught 3 blockers (missing CHANGELOG, missing tests, missing mutual-exclusion guard). All were fixed in one pass, re-review found only 2 cosmetic nits. The cycle prevents both premature merges and infinite review loops.

---

## Pre-Existing Test Baseline Check

| Field | Value |
|-------|-------|
| **Trigger** | manual |
| **When** | during |
| **Condition** | test failures appear during implementation and it's unclear whether they are pre-existing or newly introduced |
| **Facilitator** | implementer |
| **Participants** | implementer + Cameron (test lead) |
| **Time budget** | quick |
| **Enabled** | ✅ yes |

**Agenda:**
1. Stash current changes
2. Run the failing tests against main (or the base branch)
3. If tests fail on main too, they are pre-existing — fix or update test expectations as part of the current work
4. If tests pass on main, the failure is introduced — investigate the change

**Why this works:** PR #785 had 2 pre-existing Pester test failures (namespace sort order) that were initially conflated with new failures caused by a variable collision. Running the baseline check separated the two classes of failures and avoided a false root cause analysis.
