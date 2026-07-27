---
name: echo-finn-approved-pr-codeowners
description: >-
  Thin orchestrator: runs committed scripts (collect-approved-prs, analyze-approved,
  resolve-aliases, render-outputs) and fills committed templates to find open
  MicrosoftDocs/azure-dev-docs-pr pull requests Dina approved, compute path-specific
  CODEOWNERS for changed files, resolve owners to Microsoft aliases via OSPO-backed
  cache, and render a Teams-pasteable plain-text list. The skill never sends Teams
  messages and never free-generates paste content.
domain: 'PR workflow, CODEOWNERS, alias resolution, teams-paste'
confidence: low
source: 'Dina Berry directive, 2026-07-10'
status: active
category: workflow
tools:
  - name: gh
    description: GitHub CLI / REST for PR, comment, review, file, and CODEOWNERS reads
    when: collect-approved-prs (all GitHub reads)
  - name: github-ms-alias-bidirectional-lookup
    description: GitHub handle -> Microsoft alias via OSPO (forward direction only)
    when: resolve-aliases, on cache miss
# NOTE: NO Teams MCP tool. This skill only renders a paste file; it never sends.
---

# echo-finn-approved-pr-codeowners

A **thin orchestrator**. All deterministic logic lives in committed scripts under
`scripts/`; all output shapes live in committed templates under `templates/`; config
lives in `config/`. The LLM only runs scripts in order, invokes the OSPO-backed
`github-ms-alias-bidirectional-lookup` sub-skill for unresolved individual handles,
warms the shared offline alias cache, re-runs alias resolution, and reports produced
file paths.

> **Invocation:** Echo or Finn can invoke this with: "Echo/Finn, list my approved PRs
> for codeowner pings."

## USE FOR

- Finding **open** `MicrosoftDocs/azure-dev-docs-pr` PRs Dina approved but did not author.
- Computing path-specific CODEOWNERS for each PR's changed files with last-match-wins precedence.
- Rendering a plain-text Teams paste list with PR URL, title, and owner mentions.

## DO NOT USE FOR

- Sending Teams messages or calling Teams MCP tools.
- Guessing Microsoft aliases from GitHub handles.
- Producing customer-facing content or modifying PRs.
- PRs outside `MicrosoftDocs/azure-dev-docs-pr`.

## How this skill works (run order)

```
collect-approved-prs -> raw-prs.json      (pure gh: candidates + reviews/comments + files + CODEOWNERS)
analyze-approved     -> findings.json     (approval filter + CODEOWNERS path matching)
resolve-aliases      -> alias cache update (offline gh-handle -> ms-alias; teams pass through)
render-outputs       -> teams-paste.txt + report.md (pure template substitution)
```

## Output

This skill emits structured output per `structured-output@1.0.0`.

- Envelope schema: `.github/skills/structured-output/schemas/spark-structured-output-envelope.schema.json`
- Domain result schema: `approved-pr-codeowners@1.0.0`
- Producer: `echo-finn-approved-pr-codeowners`
- JSON artifacts: `projects/project-dina/data/approved-pr-runs/{run_id}/raw-prs.json`, `findings.json`, and `render.json`
- Per-stage producers:
  - `echo-finn-approved-pr-codeowners.collect-approved-prs` emits raw collection results (`stage`, `version`, `run_id`, `generated_at`, `me`, `repo`, `codeowners`, `prs`).
  - `echo-finn-approved-pr-codeowners.analyze-approved` emits findings (`stage`, `version`, `run_id`, `generated_at`, `repo`, `dina_login`, `codeowners_path`, `approved_prs`, `skipped`).
  - `echo-finn-approved-pr-codeowners.resolve-aliases` rewrites `findings.json` as an alias-resolution envelope and adds `unresolved_aliases`; unresolved aliases are warning-severity `errors[]`.
  - `echo-finn-approved-pr-codeowners.render-outputs` emits `render.json` with `stage`, `run_id`, `generated_at`, `approved_prs`, and `artifactPaths`.
- Correlation ID: `approved-pr-codeowners-{runId}` for every stage in the run.
- Supplements: `teams-paste.txt` and `report.md` are human-facing supplements only; the skill never sends Teams messages.

Per-run outputs are written outside the skill directory under
`projects/project-dina/data/approved-pr-runs/{run_id}/`. The shared offline alias cache is
`projects/project-dina/data/alias-cache.json`, the same cache used by sibling skills.
Config knobs: `approval_comment_regex` defines LGTM-style approval comments; `unresolved_retry_days`
controls when unresolved cache entries are retried through OSPO.

## Orchestration (auto-invoke the alias sub-skill)

Run the pipeline in this exact order. Step 4 is the ONE fuzzy step and must happen
automatically when needed:

1. **collect-approved-prs** -> `raw-prs.json`.
2. **analyze-approved** -> `findings.json`.
3. **resolve-aliases** first pass -> enriches owners from `alias-cache.json`; cache misses are
   recorded as `unresolved` placeholders and surfaced in `unresolved_aliases`. It deliberately
   does **not** call OSPO.
4. **IF unresolved individual handles exist:** invoke `github-ms-alias-bidirectional-lookup`
   once per handle, write every successful result to `projects/project-dina/data/alias-cache.json`
   (`status: resolved`, `source: ospo`), then **re-run `resolve-aliases`**. Misses stay
   unresolved. Never guess.
5. **render-outputs** -> `teams-paste.txt` and `report.md`.

## Subfiles this skill depends on

| File | Purpose |
|---|---|
| `scripts/collect-approved-prs.ps1` / `.sh` | Resolve Dina's login, union open PRs from `reviewed-by:@me` and `commenter:@me`, pull details, files, and CODEOWNERS. |
| `scripts/analyze-approved.ps1` / `.sh` | Exclude Dina-authored PRs; detect formal/comment approval; compute path-specific CODEOWNERS owners. |
| `scripts/resolve-aliases.ps1` / `.sh` | Resolve individual owners from shared alias cache; pass teams through; mark unresolved handles. |
| `scripts/render-outputs.ps1` / `.sh` | Fill templates to produce Teams paste text and a readable markdown report. |
| `templates/teams-paste.txt.tmpl` | Plain text Teams-paste list shape. |
| `templates/report.md.tmpl` | Human-readable report shape. |
| `config/approved-pr-codeowners.config.json` | Target repo, approval-comment regex, CODEOWNERS search paths, data locations. |
| `references/placeholder-convention.md` | Documents `{{PLACEHOLDER}}` substitution. |

## Pipeline

All scripts have a `.ps1` and `.sh` twin. Every script accepts the same shared params:
`-DataDir`/`--data-dir`, `-Config`/`--config`, and `-RunId`/`--run-id`. `collect-approved-prs`
mints the `RunId` (`yyyy-MM-dd-HHmm`); later scripts reuse it or default to the newest run
under `<DataDir>/approved-pr-runs/`. Each script prints the files it wrote.

### Step 1 — collect-approved-prs

Resolves Dina's login dynamically and lists open PR candidates with both:

- `gh search prs --repo MicrosoftDocs/azure-dev-docs-pr --state open reviewed-by:@me`
- `gh search prs --repo MicrosoftDocs/azure-dev-docs-pr --state open commenter:@me`

For each candidate it pulls PR metadata, Dina's reviews/comments, changed files, and the
first available CODEOWNERS file. No analysis happens here.

### Step 2 — analyze-approved

Keeps only open PRs not authored by Dina where a formal `APPROVED` review or stripped comment
matches `approval_comment_regex`. It applies CODEOWNERS patterns to changed paths; the **last
matching pattern wins per file**, owners are de-duplicated, and Dina's handle is excluded.

### Step 3 — resolve-aliases

Individual owners are resolved from `projects/project-dina/data/alias-cache.json` only. On miss,
write an unresolved placeholder and report it for the LLM to resolve through OSPO. Team owners
like `@org/team` are never sent to OSPO and remain as-is in output.

### Step 4 — render-outputs

Pure `{{PLACEHOLDER}}` string substitution. Produces:

- `teams-paste.txt` — plain text one-line-per-PR paste list:
  `PR #12345 — <title> — <url> — @alias1 @alias2 @org/team`
- `report.md` — readable table of PRs, owners, resolved aliases, and unresolved handles.

## Anti-patterns

- ❌ Guessing or fabricating aliases; OSPO miss means `@handle (unresolved)`.
- ❌ Sending to Teams or invoking Teams MCP tools.
- ❌ Free-generating paste content or report rows instead of filling templates.
- ❌ Treating all CODEOWNERS as global owners; matching is path-specific and last-match-wins.
- ❌ Resolving `@org/team` through OSPO; teams pass through and are flagged.
