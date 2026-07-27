# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Release detection & tracking | Echo | Scan Azure MCP CHANGELOG, open one ADO User Story per version |
| Metadata generation | Echo | Capture `{version}+{sha}` CLI snapshot, land 4 files, open metadata PR |
| Content impact & article edits | Echo | Map namespaces → articles, classify rewrite vs. fix, write/edit docs |
| Content checks (accessibility, markdown, metadata, links, style, SEO) | Spawned worker invoking a `mosaic:` skill | Echo scopes the check; a worker runs it in an isolated context — see Mosaic Content Skills |
| CODEOWNERS / reviewer resolution | Echo | `echo-finn-approved-pr-codeowners`, `ms.reviewer` alias/OSPO lookup |
| Scope & priorities | Echo | Release scope, pipeline gates, what to work on next |
| Session logging | Scribe | Automatic — never needs routing |
| RAI review | Rai | Content safety, bias checks, credential detection, ethical review |
| Verification / Devil's Advocate | Fact Checker | Verify claims, challenge assumptions, pre-mortem |

## Mosaic Content Skills (context-isolated)

The `mosaic` plugin (marketplace `agent-skills-playground-pr`, repo `azure-core/spark-content-agent-skills-playground`) is already installed and enabled. Its skills surface as `mosaic:{skill}` (accessibility, markdown, metadata, links, writing-style, SEO, and more).

**Hard rule: never invoke a `mosaic:` skill in the coordinator or Echo main session.** A skill's full `SKILL.md` plus its `references/` library (writing-style guide, contributor guide) is large and would flood context. Invoke it **inside a spawned worker** so the heavy content lives and dies in the worker's separate context window.

**Spawn pattern (per check):**
- `task` tool, `agent_type: general-purpose`, minimal prompt:
  > Invoke skill `mosaic:{skill}` on `{file}`. Return findings only — one line per issue: `{line}: {problem} → {fix}`. Do NOT paste the skill text or reference files back.
- One skill per worker, invoked by **name** (lazy-load only what the task needs).
- For a full authoring pass, fan out one worker per check **in parallel** (`mode: "background"`), then Echo synthesizes the compact returns.

**Do NOT** route through the mosaic `*.agent.md` agents in the CLI — they are `execution-environments: [visual-studio-code]` / `user-invocable: false` (VS Code orchestration). Go straight to the `mosaic:` skill by name.

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Echo (founding SME acts as Lead) |
| `squad:echo` | Pick up issue and complete the work | Echo |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
8. **Mosaic skills run in spawned workers only** — invoke one `mosaic:` skill per worker with a compact-return contract; never load mosaic skill/reference content into the main session.
