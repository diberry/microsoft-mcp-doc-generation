---
last_updated: 2026-03-20T23:31:07.439Z
---

# Team Wisdom

Reusable patterns and heuristics learned through work. NOT transcripts — each entry is a distilled, actionable insight.

## Patterns

<!-- Append entries below. Format: **Pattern:** description. **Context:** when it applies. -->

**Pattern:** Derive parameter Required/Optional strictly from the CLI metadata `required` boolean, never from parsing the option's description text. **Context:** Doc-generation parameter tables and any step that reinjects/filters params (issues #732/#733, PR #745). Description-derived requiredness caused PostgreSQL `--auth-type` mismatches.

**Pattern:** Step 3 must re-inject *optional* parameters, not only required ones; Step 4 assembly must constrain commands to the *current* CLI metadata (no stale-command inheritance); namespace-wide AI prose regeneration needs previous-version delta gating. **Context:** Four durable pipeline defects catalogued in issue #740 during beta.22; watch for them whenever a new MCP beta changelog is thin or empty.

**Pattern:** Do not gate a `pwsh` CI/workflow step on `$LASTEXITCODE` for a helper script that never calls `exit`; validate the expected output contract instead (e.g., a non-empty list of existing files). **Context:** Article Health / validation-gate workflow steps (PR #747). A null/empty exit code silently passed or failed.

**Pattern:** A generated-content "phantom parameter" is usually a config mapping bug, not a generator-logic bug — check `nl-parameter-identifiers.json` for erroneous suffixes (e.g., `health-model` → "Health model name") before touching generator code. **Context:** Parameter-identifier defects surfaced in published content (issue #742, PR #744). Distinguish generator bugs from manual authoring errors in the symptom PR.

**Pattern:** When adding a standalone .NET project with top-level statements, ensure only one file (`Program.cs`) declares them; a second top-level-statement file (e.g., `GenerateMapping.cs`) causes CS8802 and blocks the build. **Context:** McpCliMetadata generator (PR #750). Exclude or wrap the extra file's top-level statements.

**Pattern:** After a build/generator fix, the *next* failure is often a legitimate Azure infra exception (Foundry endpoint DNS, `.env` credentials, quota) — classify it as an operational blocker for the human, not as a regression to keep patching. **Context:** beta.27/beta.28 generation runs. Separates engine bugs from environment issues so the team stops chasing non-code failures.

**Pattern:** Split unrelated changes into separate PRs before review; retitle and rewrite both PR bodies when you carve one out. **Context:** Multi-service PRs (e.g., Redis carved out of a PostgreSQL PR, #9413 → #9455). Keeps scope reviewable and merge-safe.

**Pattern:** Under reviewer-lockout, the original author is barred from revising their own rejected work — a different agent (or an at-will guest reviewer/fixer) performs the fix. **Context:** Any PR that fails review. The coordinator may cast a guest fixer/adversarial reviewer at-will to satisfy this.

**Pattern:** Ownership split — build/maintain the generation *engine* vs. *operate* it to produce content are different jobs. **Context:** This repo is the engine (generators, steps, templates, validators). Content-production runs (release detection, generation execution, docs PRs) are downstream operations; keep engine changes decoupled from content-run mechanics.

**Pattern:** For repository-root PowerShell orchestrators that consume AZD configuration, resolve `.azure/<environment>/.env` through `defaultEnvironment`, accept only one unambiguous nested candidate when no default resolves, and use `.azure/.env` only as a final fallback. Invoke child scripts directly with argument arrays so output streams in real time and paths containing spaces remain safe in PowerShell and Git Bash. **Context:** Versioned all-namespace generation from `mcp-cli-metadata/`.
