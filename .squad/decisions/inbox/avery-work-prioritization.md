# Decision: Work Prioritization Framework — Post-Review Issue Set

**Date:** 2026-03-25  
**Author:** Avery (Lead / Architect)  
**Status:** Proposed  
**Requested by:** Dina Berry  

## Context

After merging PRs #200 and #201, creating the requirements doc (#202), completing the test strategy, and receiving 6 team reviews, the backlog needed consolidation and prioritization. This decision documents the prioritization framework and resulting issue set.

## Decision

14 GitHub issues created across 4 priority tiers, synthesized from:
- Requirements review (#202) — team consensus
- Test strategy reviews — 6 reviewers
- AD-020 (pipeline architecture assessment by Riley)
- AD-021 (Step 3/6 validator requirement by Parker)
- Existing backlog (#142-#146, #154)

### Priority Framework

| Tier | Criteria | Issues |
|------|----------|--------|
| **P0** | Data loss or silent bad content shipping | #203, #204, #205 |
| **P1** | Catches bugs before production | #206, #207, #208, #209, #210 |
| **P2** | Improves developer experience / observability | #211, #212, #213, #214 |
| **P3** | Improves quality over time | #215, #216 |

### Issue Summary

| # | Title | Priority | Owner |
|---|-------|----------|-------|
| #203 | Fix ResetOutputDirectory — destroys partial progress | P0 | Riley |
| #204 | Add Step 3 post-validator — template token leakage | P0 | Morgan |
| #205 | Add Step 6 post-validator — incomplete horizontal articles | P0 | Sage |
| #206 | Add TextCleanup unit tests — high-risk regex chain | P1 | Parker |
| #207 | Add failure path tests for all pipeline steps | P1 | Parker |
| #208 | Add Bootstrap contract tests — step I/O validation | P1 | Avery |
| #209 | Implement baseline fingerprinting for generated output | P1 | Avery + Quinn |
| #210 | Replace regex error detection with structured step-result.json | P1 | Riley |
| #211 | Add prompt versioning system | P2 | Sage |
| #212 | Add token usage tracking and observability | P2 | Sage |
| #213 | Create CI integration documentation | P2 | Quinn |
| #214 | Build prompt regression testing framework | P2 | Sage |
| #215 | Acrolinx compliance — automated style fixes | P3 | Sage |
| #216 | Consolidate StripFrontmatter implementations | P3 | Morgan |

### Work Distribution

| Member | Issues | Load |
|--------|--------|------|
| Riley | #203, #210 | 2 (pipeline infra) |
| Morgan | #204, #216 | 2 (validators + refactor) |
| Sage | #205, #211, #212, #214, #215 | 5 (AI/prompt domain) |
| Parker | #206, #207 | 2 (test authoring) |
| Avery | #208, #209 | 2 (architecture + contracts) |
| Quinn | #213 | 1 (CI docs) |
| Reeve | Reviewer on #213 | Supporting |

### Execution Order

1. **Immediate:** P0 issues (#203, #204, #205) — all three can be worked in parallel
2. **Next sprint:** P1 issues — #206 and #210 first (highest test/infra leverage), then #207, #208, #209
3. **Following sprint:** P2 issues — #211 and #214 first (prompt quality loop), then #212, #213
4. **Backlog:** P3 issues — pick up opportunistically

## Rationale

The prioritization follows a single principle: **prevent harm before adding value.** P0 prevents data loss and silent failures. P1 builds the safety net. P2 makes the team faster. P3 polishes quality. This order ensures each tier's value compounds — fingerprinting (P1) enables prompt regression testing (P2), which enables prompt versioning (P2) to be meaningful.

## Impact

All team members have assigned work. The `squad` label on all issues enables triage routing. Individual `squad:{member}` labels enable filtered views per team member.
