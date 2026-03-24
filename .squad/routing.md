# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Pipeline architecture / cross-stage issues | Riley | Data flow changes, new pipeline stages, quality gates between stages, step contracts, template architecture |
| Pipeline orchestrator / step contracts | Riley | PipelineRunner, step registration, dependencies, workspace isolation, retry logic, merge orchestration |
| C# generator code (`docs-generation/**/*.cs`) | Morgan | Generator bug fixes, new generators, template changes, config file updates |
| Scripts / CI / Docker (`.ps1`, `.sh`, `.yml`, `Dockerfile`) | Quinn | Script fixes, Docker builds, CI pipeline, preflight validation |
| AI prompts / Azure OpenAI (`prompts/`, `GenerativeAI/`) | Sage | Prompt design, AI output validation, fabrication detection, content transformation |
| Test projects (`*.Tests/`, `verify-quantity/`) | Parker | New tests, test data, content validation, regression detection |
| Code review | Avery + domain specialist | Avery coordinates review; domain owner reviews implementation |
| Test strategy & quality gates | Cameron | Test architecture, coverage roadmap, validator requirements, regression frameworks |
| Test strategy + architecture alignment | Cameron + Riley | Co-own quality gate definitions, testability reviews, release readiness |
| Testing | Parker | Write tests, find edge cases, verify fixes (coordinated by Cameron) |
| Scope & priorities | Avery | What to build next, trade-offs, decisions, sprint planning |
| Architecture & design reviews | Riley | New steps, cross-stage changes, template architecture, structural changes |
| Content quality issues in generated output | Sage (if AI) + Morgan (if template/config) | Hallucinated RBAC roles → Sage; wrong parameter count → Morgan |
| Engineering & user documentation (`docs/`, `README.md`) | Reeve | Pipeline guides, config reference, troubleshooting, user how-tos |
| PR documentation review | Reeve | Every PR reviewed for doc completeness — blocks if docs missing |
| Async issue work (bugs, tests, small features) | @copilot 🤖 | Well-defined tasks matching capability profile |
| Session logging | Scribe | Automatic — never needs routing |

## Multi-Agent Routing

Some tasks need multiple agents working in parallel:

| Scenario | Primary | Secondary | Notes |
|----------|---------|-----------|-------|
| New pipeline stage | Avery (design) | Riley (orchestration) + Morgan (implement) + Quinn (script) + Parker (test) + Reeve (docs) | Avery designs, Riley wires into PipelineRunner, then fan out |
| AI content quality fix | Sage (prompt) | Parker (validation test) + Reeve (update troubleshooting docs) | Sage fixes prompt, Parker adds regression test |
| Generator bug fix | Morgan (fix) | Parker (test) + Reeve (docs if behavior changes) | Morgan fixes code, Parker adds test |
| Full pipeline change | Avery (architecture) | All others | Avery designs, routes subtasks |
| Any PR | Domain owner (implement) | Reeve (doc review) + Parker (test review) | Reeve blocks if docs missing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, evaluate @copilot fit, assign `squad:{member}` label | Avery |
| `squad:avery` | Architecture and cross-cutting work | Avery |
| `squad:morgan` | C# generator implementation | Morgan |
| `squad:quinn` | Scripts and DevOps | Quinn |
| `squad:sage` | AI prompts and content quality | Sage |
| `squad:parker` | Testing and validation | Parker |
| `squad:reeve` | Documentation (engineering + user docs) | Reeve |
| `squad:copilot` | Assign to @copilot for autonomous work (if enabled) | @copilot 🤖 |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, **Avery** triages it — analyzing content, evaluating @copilot's capability profile, assigning the right `squad:{member}` label, and commenting with triage notes.
2. **@copilot evaluation:** Avery checks if the issue matches @copilot's capability profile (🟢 good fit / 🟡 needs review / 🔴 not suitable). If it's a good fit, Avery may route to `squad:copilot` instead of a squad member.
3. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
4. When `squad:copilot` is applied and auto-assign is enabled, `@copilot` is assigned on the issue and picks it up autonomously.
5. Members can reassign by removing their label and adding another member's label.
6. The `squad` label is the "inbox" — untriaged issues waiting for Avery's review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. Avery handles all `squad` (base label) triage.
8. **@copilot routing** — when evaluating issues, check @copilot's capability profile in `team.md`. Route 🟢 good-fit tasks to `squad:copilot`. Flag 🟡 needs-review tasks for PR review. Keep 🔴 not-suitable tasks with squad members.
9. **Content correctness is everyone's job** — if any agent notices content quality issues in generated output, they flag it even if it's outside their domain.
10. **All work goes through PRs** — no direct commits to main. Every PR needs domain review + Reeve's doc review.
11. **PRs must have docs** — Reeve reviews every PR for documentation completeness. Missing docs = merge blocked. See AD-004 for exemption rules.
12. **Reeve joins every PR** — Reeve is automatically included in all PR reviews as the documentation gate.
