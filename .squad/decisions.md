# Squad Decisions

## Active Decisions

### 2026-08-24: AD-044 — Step 2 required-parameter validation failures narrowed to non-blocking (`ArtifactFailure.IsBlocking`)
**By:** Coordinator (Squad), direct TDD change amending Riley's runtime-orchestration domain (`PipelineRunner.IsFatalRoot`, AD-029/#813 item 2)
**What:** A user-reported log showed Step 2 exiting after retries with "Required parameters missing from example prompts: soft-delete" and that single warning suppressing Steps 3-6 for the whole namespace via `IsFatalRoot`'s existing "C2" clause (`artifactFailures.Count > 0`). Added `ArtifactFailure.IsBlocking` (default `true` — every pre-existing call site across every step is unaffected) and narrowed C2 to `artifactFailures.Any(f => f.IsBlocking)`. `ExamplePromptsStep` now sets `IsBlocking=false` **only** when the last validator run for an unresolved command genuinely parsed an explicit `Invalid tools:` report from a validator process that ran successfully (a real content/parameter-validation result) and retries (`MaxValidationRetries`) were exhausted. Every other Step 2 failure shape stays `IsBlocking=true`, unaffected:
  - Generator process crash/failure (before or after tool identification).
  - Missing per-tool output files (initial or after a retry).
  - A retry's generator re-invocation itself failing (process/launch failure).
  - The validator "fallback" case, where the validator process/output could not be parsed at all so we assume ALL matching/targeted tools are invalid (a hard, process-level uncertainty — not a genuine content result).
  - Every other step's `ArtifactFailure.Create(...)` call site (`BootstrapStep`, `ArticleHealthValidatorStep`, `CoverageAuditStep`, `HorizontalArticlesStep`, `SkillsRelevanceStep`, `ToolFamilyCleanupStep`, `ToolGenerationStep`, `ValidationStepBase`) — none pass `isBlocking`, so all keep the default `true`.
  C1 (nonzero mapped exit code) is untouched — a hard `Success=false` failure, forced exit-code override, or fatal envelope-write failure still always roots regardless of any artifact failure's blocking status. Retries, warning output (retry-attempt messages, per-tool validation detail lines, final failure summary), and the recorded `ArtifactFailure` itself are all preserved — only the failure's ability to make Step 2 a fatal root that suppresses dependents is removed.
- **TDD:** RED proven by reverting only the 4 production files (`git stash` of `ArtifactFailure.cs`, `NamespaceStepBase.cs`, `ExamplePromptsStep.cs`, `PipelineRunner.cs`) with the new/modified test assertions in place — 10 compile errors (`CS1061`/`CS1739` — `IsBlocking`/`isBlocking` do not exist yet). Restored the 4 files (`git stash pop`) for GREEN: targeted tests 54/54, full `DocGeneration.PipelineRunner.Tests` project 684/684, full solution `dotnet test` clean on rerun (one `AiCapabilityProbeTests` failure on a single full-suite run was isolated and reproduced as pre-existing cross-assembly env-var flakiness unrelated to this change — passed both in isolation and on a full-suite rerun).
- **Pinned tests unaffected:** `DependencySuppressionTests.cs` T32/T33 (and the Beta34 corpus replay) construct `ArtifactFailure.Create(...)` without `isBlocking` and remain green automatically; they were **not modified**. A new, separate test (`IsFatalRoot_NonBlockingArtifactFailures_AreNotARoot`) proves the new non-blocking path and a mixed blocking/non-blocking case (still roots).
- **Squad review follow-up (test-only, no production change):** Riley (architecture) and Cameron (test lead) independently flagged that the "latest validator observation wins" semantic (a command's blocking status can legitimately flip across retries as the underlying content is regenerated) and the validator "fallback" case (never parses an explicit list, across all attempts) were correct by construction but not directly pinned by a test. Added `Step2_ExamplePrompts_ValidatorFallbackAfterRetriesStaysBlocking` and `Step2_ExamplePrompts_LatestValidatorObservationDeterminesFinalBlockingStatus` (both in `NamespaceStepTests.cs`, using a new `FallbackThenGenuineParseRunner` test double) to close both gaps; targeted suite now 56/56. See Addendum C-2 in the evidence file.
**Why:** Preserves the AD-029/#813 item 2 contract for every failure that is genuinely a hard/process/missing-artifact failure (0 change in behavior, 0 regressions), while fixing the one shape the user's report showed was too aggressive: a content-only, exhausted-retries required-parameter warning that should be visible but non-fatal.
**Docs:** `docs/ARCHITECTURE.md` (`IsFatalRoot`/C2 table + amendment + two new worked examples + the "fatal root" summary paragraph in the exit-code appendix), `docs/START-SCRIPTS.md` (Run Accounting Summary section — "Root-failed namespaces" now qualified as **blocking** artifact failures), `README.md` (fatal-step-suppression callout reworded to "blocking" + a Step 2 non-blocking-warning note), `CHANGELOG.md` under `## [Unreleased]` (new bullet, plus the pre-existing AD-029 bullet amended in place so the two don't contradict each other), `mcp-tools/DocGeneration.PipelineRunner.Tests/evidence/813-step2-warn-not-block-addendum-c.txt` (RED/GREEN evidence, following the Addendum B evidence-file convention).

### 2026-08-20: AD-043 — Step 6 namespace-summary call split into four small, focused fragment calls
**By:** Coordinator (Squad), surgical change to Morgan/Riley domain (`HorizontalArticleGenerator`)
**What:** Replaced the single broad namespace-summary AI call (33 KB `horizontal-article-system-prompt.txt` + 6 KB `horizontal-article-namespace-user-prompt.txt`, seven fields, prone to truncation on `gpt-5-mini`, then blindly retried 3× by `WithRetry`) with four small, focused AI calls, each with its own compact, service-agnostic prompt pair and small output-token budget:
  - **overview** — `ServiceShortDescription` + `ServiceOverview`, ~500 tokens (required, fatal-if-absent — matches prior behavior).
  - **access** — `ServiceSpecificPrerequisites` + `RequiredRoles`, ~1,500 tokens (grounded to the compact tool list for minimum-privilege RBAC).
  - **best practices** — `BestPractices`, ~1,500 tokens (also tool-grounded).
  - **links** — `ServiceDocLink` + `AdditionalLinks`, ~750 tokens.
  - Each fragment deserializes into a new typed model (`Models/NamespaceFragmentAIData.cs`: `NamespaceOverviewFragment`/`NamespaceAccessFragment`/`NamespaceBestPracticesFragment`/`NamespaceLinksFragment`) and is deterministically stitched — pure, no AI, no I/O — via `StitchNamespaceSummary` back into the existing `NamespaceSummaryAIData` shape, so `AggregateAIData`, `ArticleContentProcessor`, and template rendering needed zero changes.
  - The three static-include directives (`mcp-introduction.md`, `mcp-prerequisites.md`, `mcp-usage-contexts.md`) remain deterministic template content, unaffected and never sent to/generated by AI.
  - **Retry classification (`IsRetryableAiFailure`):** `WithRetry` gained an optional `shouldRetry` predicate. Only positive transient cases retry: network failures without an HTTP response, timeouts, HTTP 429, and HTTP 5xx. Token truncation, malformed JSON, cancellation, unknown exceptions, and all other HTTP client errors fail immediately.
  - **Operation/target context:** the per-tool call and all four fragment calls now pass real `operation`/`toolOrNamespace` values into `GenerativeAIClient.GetChatCompletionAsync` (e.g. `operation=per-tool target={tool command}`, `operation=namespace-overview target={serviceIdentifier}`) — status logs no longer show `target=unknown` for these calls.
  - Each component prompt + response is saved separately under `horizontal-article-prompts/` with a clear per-component filename (e.g. `horizontal-article-{service}-namespace-overview-prompt.md`).
  - **Collateral-avoidance:** the legacy 33 KB system prompt and namespace-user-prompt files remain untouched because unrelated regression tests assert their existence/content, but no supported Step 6 entry path reads them. Missing focused prompt pairs fail before any AI request instead of falling back to monolithic generation.
**Why:** Eliminates guaranteed-truncation-then-wasted-retry AI calls on reasoning models like `gpt-5-mini` for the namespace-summary step, without touching deterministic template/include-file content or downstream data shapes. Behavioral tests cover stitching, budgets, transient-only retries, prompt-pair gating, compact-prompt-only wiring, operation/target context, the five-call sequence, and required-overview failure.
**Docs:** `mcp-tools/DocGeneration.Steps.HorizontalArticles/README.md` (Token limits, Generation Process, Customization sections), `docs/ARCHITECTURE.md` (Step 6 diagram bullet + `AiOffline` behavior table row), `CHANGELOG.md` under `## [Unreleased]`.

### 2026-08-20: AD-042 — Live Azure OpenAI bootstrap probe + `partial_explicit` offline continuation
**By:** Coordinator (Squad), cross-cutting change spanning Riley/Morgan/Cameron domains
**What:** Added a very-early live Azure OpenAI call in `BootstrapStep` (right after config presence is confirmed) to prove the configured endpoint actually works, before any generation step runs.
- **Seam:** Added `LiveCheckAsync` directly to the existing `IAiCapabilityProbe` interface/implementation (alongside its existing `ProbeAsync` configuration-presence check) — no parallel probe abstraction was introduced. Added one small `IPipelineUserPrompt` (console prompt, non-blocking on redirected/non-interactive input) since no existing seam covers interactive confirmation. Both live in `PipelineRunner/Services/`.
- **Non-interactive fail-fast:** Redirected/non-interactive input always fails immediately with a nonzero exit on probe failure — never silently continues.
- **Interactive continuation = `partial_explicit`:** On interactive Continue, the pipeline (a) persists one loud, explicit critical-failure JSON record via the existing `CriticalFailureRecorder.Persist` facility (no new JSON shape invented) stating the endpoint failed and partial deterministic fallback was selected, (b) sets `PipelineContext.AiOffline = true` and exports `PIPELINE_AI_ENDPOINT_OFFLINE=true` so child subprocesses inherit it, (c) disables all further AI endpoint calls universally via a call-time guard in `GenerativeAIClient.GetChatCompletionAsync` (throws `AiEndpointOfflineException`), and (d) never reports AI-required artifacts as fully successful — each step's existing fallback (Step 2 incomplete AI tier, Step 3 byte-identical composed fallback, Step 4 `<TBD_Content>`, Step 6 explicit incomplete failure) is preserved and now proven to trigger without any network attempt.
- **Gate placement — call-time, not construction-time:** The offline guard lives inside `GenerativeAIClient.GetChatCompletionAsync` rather than at client construction, because `GenerativeAIClientTracingTests` proved a direct `(IChatClient, tracer, modelName)` constructor bypasses the options-based factory entirely. Call-time gating is the only placement that catches every construction path uniformly.
- **Step 6 rewrite:** `HorizontalArticleGenerator.GenerateArticleMarkdownAsync` (PipelineRunner/reducer path) now uses the same current per-tool + namespace-summary AI generation path as the standalone `GenerateSingleArticleAsync` (extracted into a shared `GeneratePerToolAiDataAsync` helper), instead of the obsolete monolithic `GenerateAIContent`. The meaningless subprocess fallback (which silently repeated the same failing AI call when AI was already known unavailable) was removed from `HorizontalArticlesStep`; a reducer-path failure now fails directly with an incomplete result. Behavior stays universal/service-agnostic — no per-namespace special-casing.
**Why:** Prevents wasted, expensive full-namespace runs against a misconfigured Azure OpenAI endpoint (previously only failed loudly deep into Step 2/3/4/6), while still allowing an operator to deliberately choose a partial deterministic-only run without ever mistaking placeholder/fallback content for a fully successful AI result. Reuses all existing failure-accounting/critical-record facilities rather than inventing a parallel one.
**Docs:** `docs/ARCHITECTURE.md` (new "AI Endpoint Probe & Offline Continuation" section + observed-vs-designed behavior table), `CHANGELOG.md` under `## [Unreleased]`.

### 2026-08-14: L-006 — `gh api -f body=@file` does NOT expand file references
**By:** Scribe — learned from #813 Step 1 (Quinn/Avery)
**What:** `gh api -f body=@file` writes the literal string `@file`, not the file's contents. It clobbered a live tracker comment. Use `gh api --input payload.json` with a JSON body file instead.
**Why:** Tooling gotcha worth recording to prevent repeat.

### 2026-08-14: L-005 — Reviewer-lockout pattern is worth the overhead
**By:** Scribe — learned from #813 Step 1 (Cameron + Ellis)
**What:** Cameron authored only strategy (not tests); Ellis (guest) authored nothing — between them they caught 7 blocking defects that self-review would have missed.
**Why:** Independent review gates justify the cost of spawning a guest reviewer even for single tasks. If #813 Steps 2–10 also need independent nondeterministic eval, consider promoting the Evaluation Reviewer role to standing.

### 2026-08-14: L-004 — Don't collapse independent dimensions into one taxonomy field
**By:** Scribe — learned from #813 Step 1 (Riley/Parker)
**What:** `classification: mixed` was conflating error-type overlap with chain position. Fix: keep the mandated single taxonomy but add `chainRole`, `errorClasses`, and `upstreamStableIds`. Correct accounting is **10 dependent Step-4 records / 16 upstream Step-2 links** (one Step-4 record can have multiple roots), not "10 pairs."
**Why:** Collapsing dimensions loses auditability and produces wrong downstream counts.

### 2026-08-14: L-003 — Derive provenance from run evidence, not sample config
**By:** Scribe — learned from #813 Step 1 (Quinn)
**What:** The manifest initially recorded `sample.env` model values (`gpt-4.1-mini`/`gpt-4o`, api `2025-01-01-preview`) while run logs showed `gpt-5-mini` / `2025-03-01-preview`. Always derive provenance from the run's own sanitized logs; if undiscoverable, record `null` with a note — never fabricate.
**Why:** Fabricated provenance makes the baseline untrustworthy for regression comparison.

### 2026-08-14: L-002 — Hash-pinned fixtures need a `-text` `.gitattributes` entry
**By:** Scribe — learned from #813 Step 1 (Quinn)
**What:** Committed fixtures under `* text=auto` + `core.autocrlf=true` caused Windows checkout to rewrite LF→CRLF, breaking every pinned SHA-256. Any future hash-pinned artifact must have `-text` in a local `.gitattributes` in the same directory.
**Why:** EOL rewriting silently invalidates all hash assertions, making fixtures appear corrupted on every Windows clone.

### 2026-08-14: L-001 — Green in author's tree ≠ green in CI or a clean clone
**By:** Scribe — learned from #813 Step 1 (Cameron/Ellis round-1 rejection)
**What:** Tests depended on a gitignored `generated-*` directory that existed in the author's working tree but not in CI or a fresh clone — both review gates rejected round 1. Fix pattern: commit an inventory artifact (raw hashes + logical identity) so accounting/hash gates verify from committed data; make live-source verification an explicitly-skipped opt-in test.
**Why:** Any test that silently passes when a gitignored directory is absent is not a reliable regression guard.

### 2026-08-14: ROSTER — Ellis guest engagement complete; recurring need flagged
**By:** Scribe — #813 Step 1
**What:** Ellis (guest Evaluation Reviewer, nondeterministic) was hired 2026-08-14 for #813 Step 1 only. Engagement is complete; guest is dismissed. If #813 Steps 2–10 also require an independent eval reviewer, the recurring need should be assessed for promotion to a standing roster position before the next step begins.
**Why:** Guest reviewers must be explicitly re-engaged per step unless promoted to standing.

### 2026-08-14: AD-028 — beta.34 baseline fixture freeze architecture
**By:** Riley (Architect) — issue #813 Step 1
**What:** Freeze the 34 logical catalog-level critical-failure records from run `generated-20260813T162453` as immutable test fixtures.
- **Layout:** new xUnit project `mcp-tools/DocGeneration.Baseline.Beta34.Tests` (net10.0, CPM, added to `mcp-doc-generation.sln`) holding `Fixtures/critical-failures/*.json` (34 sanitized copies) + `Fixtures/beta34-baseline-manifest.json` + `README.md`. Fixtures are copied output, never edits to the source run — complies with "never edit generated files."
- **Stable ID:** `{namespace}.{stepId:D2}.{artifactSlug}.{ordinal:D2}` (e.g., `storage.02.account-create.01`) — derived only from record content (namespace, stepId, kebab artifactName, per-tool ordinal), path/timestamp-independent, proven collision-free across all 34.
- **Sanitization contract (deterministic + idempotent — 2nd pass byte-identical):** redact/normalize absolute repo paths → `<REPO>/…`, temp dirs `…/AppData/Local/Temp/…` and pipeline GUID dirs → `<TEMP>/pipeline-runner-stepN-<GUID>/…`, username `diberry`/machine name → `<USER>`/`<HOST>`, per-run output dir suffix `generated-<ns>-YYYY-MM-DD-HH-MM-SS` → `generated-<ns>-<RUNSTAMP>`. **Retained (semantically meaningful):** `recordedAtUtc`, azureMcpBuild version+SHA, stepId, namespace, artifactName, validatorResults. No secrets exist in records; secret-scan test enforces it.
- **Manifest:** versioned (`schemaVersion`), `provenance` block (repo commit SHA, source run dir, `azureMcpBuild=3.0.0-beta.34+eec7acccddab1e16be852a3c3b9503cc9adf7538`, model/deployment/apiVersion/temperature/seed where discoverable, config/prompt hashes, capture timestamp, tool versions) + 34 `records[]` each with `stableId, namespace, stepId, artifactName, sourceRelativePath, sourceSha256, sanitizedSha256, classification(root|mixed|cascade|diagnostic), errorClass(A|B|C|D), physicalCopies[](catalog+namespace paths), rationale`.
- **Immutability:** tests assert `sanitizedSha256` of each fixture and `sourceSha256` against the manifest; a `--regen` script recomputes but fails if any existing hash would change (no silent drift). Duplicate accounting test proves 34 logical → 68 physical (catalog + one namespace copy each).
**Why:** Establishes a provable, secret-free, deterministic beta.34 regression baseline; classification captured exactly once per logical record. **No blocking concerns**; seed/temperature may be undiscoverable — record as `null` with a provenance note rather than fabricate.

### 2026-08-01: AD-027 — PowerShell parameter-variable collision check
**By:** Coordinator (learned from PR #785)
**What:** When reviewing PowerShell scripts (`.ps1`), Quinn and reviewers must check that `param()` parameter names do not collide (case-insensitive) with local variables used in the script body. PowerShell's type-constrained parameters silently coerce reassigned values (e.g., `[string]$Namespaces` converts an array assignment to a space-joined string), causing subtle bugs that only surface at runtime.
**Why:** PR #785 had `[string]$Namespaces` as a parameter and `$namespaces` as a local variable. PowerShell treated them as the same variable, coercing array→string and breaking namespace iteration. Fix was to rename the parameter to `$NamespaceList`. This class of bug is invisible to static analysis and easy to introduce.

### 2026-05-31: AD-026 — PR CHANGELOG requirement (summary reference)
**By:** Dina Berry (via Copilot)
**What:** Every PR must update `CHANGELOG.md` under `## [Unreleased]` with a user-facing description before team review. Full definition in `.squad/decisions-archive.md` under AD-026.
**Exemptions** (must be stated in PR comment): test-only changes, internal refactors with no behavior change.
**Why:** Surfaced as required but unresolvable reference in parameter-filtering PRD review (Rounds 1–2). Active decisions.md is the lookup point agents use during dispatch; archive entry alone was insufficient for routing.

### 2026-05-30: Per-tool AI call refactor
**By:** Morgan
**What:** GenerateAIContent now calls AI once per tool + once for namespace summary, instead of once per namespace with all tools. Eliminates token overflow on large namespaces like storage.
**Why:** Storage namespace had 18k token input, exceeding limits. Per-tool calls are bounded to ~1 tool's data.

### 2026-05-30: Per-tool AI prompts created
**By:** Sage
**What:** Created horizontal-article-tool-system-prompt.txt, horizontal-article-tool-user-prompt.txt, horizontal-article-namespace-user-prompt.txt for per-tool AI calls.
**Why:** Namespace-level calls caused 18k token overflow on storage. Per-tool calls bound input to one tool.

### 2026-05-30: Empty namespace summary causes article failure
**By:** Morgan
**What:** Added validation check after AggregateAIData — if ServiceShortDescription or ServiceOverview is empty, fail the article with a clear error message rather than silently generating broken output.
**Why:** Rubber-duck review caught that the empty-fallback path produced valid-looking but content-corrupted articles that passed all validation gates.

### 2026-05-29: npm-to-dotnet CLI metadata migration completed (#627)
**By:** Quinn (DevOps) & Reeve (Documentation)
**What:** Removed all Node.js npm scripts from `mcp-cli-metadata/` (package.json, generate-report.js, validate-cli-output.js, etc.). The .NET `McpCliMetadata` tool is now the sole CLI metadata extractor. Updated CI workflows (update-azure-mcp.yml, test-azure-mcp-update.yml) to use Python for JSON validation instead of Node.js. Updated all documentation (README, ARCHITECTURE, CHANGELOG, copilot-instructions).
**Why:** The .NET replacement (PR #628, #631) is complete and integrated. Keeping npm scripts alongside created ambiguity and false security surface (npm audit on unused code). CI still installs `npm install -g @azure/mcp` to get the binary on PATH for the .NET tool to invoke.

### 2026-05-26: PRD #574 formalized and approved
**By:** Avery
**What:** Formalized issue #574 into a 17-dimension PRD artifact at `projects/azure-ai-tools/prds/2026-05-26T11-57-prd-574-validation-pipeline-integration.md` and completed a 6-round, 8-reviewer approval cycle to 8/8 approval.
**Why:** The integrated validation pipeline is a high-blast-radius feature. The final PRD now pins gate ownership to the pipeline, defines versioned validation contracts, rollout criteria, waiver rules, and test expectations tightly enough to guide implementation without reopening architecture questions.

### 2026-05-26: PRD #574 Phase 1 boundary and exit criteria
**By:** Avery
**What:** Phase 1 implements repo-local relocation of the existing validation scripts, fixtures, and Pester suites into `mcp-tools/validation/`, plus the minimum support surface needed to keep them usable and enforced (README, runbook, changelog, and CI execution of the relocated Pester suite). Phase 1 does **not** add pipeline wrapper code, PRD JSON-contract fields, placeholder-token detection, waiver logic, or gate verdict computation; those stay in Phases 2-4.
**Why:** The first week needs a clean, low-blast-radius landing of the already-tested deterministic validators before we start changing their runtime contracts. Moving the assets and making the suite runnable in CI gives Morgan, Quinn, and Cameron a stable base for wrapper work without mixing relocation risk with new validation semantics.

### 2026-05-26: Normalize PipelineRunner output paths before resolution
**By:** Avery
**What:** PipelineRunner now normalizes both `\\` and `/` in `PipelineRequest.OutputPath` before resolving it against the repo root.
**Why:** CI runs on Linux while many scripts and tests still pass Windows-style relative paths like `.\\generated`. Normalizing separators makes output and trace artifacts land in the intended directories on every platform instead of creating literal backslash path segments on Unix.

### 2026-05-26: PRD #574 Phase 1 review — BLOCKED (files not committed)
**By:** Morgan
**What:** BLOCKED. The branch `diberry/validation-pipeline-integration` is not mergeable. The entire Phase 1 deliverable — `mcp-tools/validation/` (scripts, tests, fixtures, README) and `docs/VALIDATION-RUNBOOK.md` — is untracked in the working directory and never committed. Only a Scribe housekeeping commit is on the branch. If merged, the `pester-tests` CI job would fail (path does not exist).
**Why:** `git status` confirms both paths are untracked. `git diff origin/main --name-only` shows only workflow/doc changes and `.squad/` files — no validation tree. Fix: `git add mcp-tools/validation/ docs/VALIDATION-RUNBOOK.md && git commit` before PR review.

### 2026-05-26: PRD #574 Phase 1 Doc Review — CONDITIONAL PASS
**By:** Reeve
**What:** Reviewed Phase 1 docs (diberry/validation-pipeline-integration). Verdict: CONDITIONAL PASS. Docs are substantively accurate; no wrong commands or overpromised behavior. Two non-blocking content issues must be fixed before commit; one process issue (nothing committed) blocks PR submission.
**Why:** Docs review passed. Content issues correctable; process blocker (untracked deliverables) is the same one Morgan identified.
**Notes:** Process blocker: nothing committed except Scribe housekeeping. Content issues: (1) RUNBOOK pre-creation instruction orphaned from command blocks — move mkdir note into each command example; (2) validation README uses Windows-only backslashes in Invoke-Pester path — change to forward slashes for cross-platform.

### 2026-05-27: Phase 1 #574 test review — REJECT (2 blocking findings)
**By:** Cameron
**What:** Reviewed validation test suite (Test-ArticleHealth.Tests.ps1, Scan-McpToolCoverage.Tests.ps1, fixtures/) on diberry/validation-pipeline-integration. Verdict: REJECT. Two AD-010 violations (vacuous test, zero regression coverage).
**Why:** Test review identified vacuous assertion and missing fixture. Per reviewer protocol, original author is locked out; fixes owned by another agent (Parker).
**Blocking findings:**
- BLOCKING-1 (ms.reviewer test): `$r | Should -BeIn @("warn", "fail")` is vacuous — test passes regardless of outcome. Fix: Pin to `$r | Should -Be "warn"`.
- BLOCKING-2 (markers.well-formed): Zero regression coverage — no bad-markers.md fixture, no test ever triggers the warn path. Fix: Add bad-markers.md fixture with malformed HTML comment and test that asserts `markers.well-formed` returns `"warn"`.

---

## Bug Fixes & Merges

**Author:** Morgan (C# Generator Developer)  
**Date:** 2026-05-19  
**Branch:** `squad/603-604-602-namespace-resolution-fixes`

---

## Summary

Fixed three interconnected bugs that caused the pipeline to fail for decomposed namespaces (e.g., `extension_azqr`, `extension_ghissues`) and when Step 3 is skipped.

---

## Bug #603 — ResolveFamilyName uses CLI prefix instead of raw namespace key

**Root cause:** `ResolveFamilyName()` in `ToolFamilyCleanupStep.cs` always took `tokens[0]` from the first CLI command (e.g., `"extension"` for `extension azqr scan`). The brand mapping keys use underscores (`extension_azqr`), so the lookup always missed.

**Files changed:**
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/ToolFamilyCleanupStep.cs`  
  `ResolveFamilyName()` now checks `currentNamespace` against brand mappings first; falls back to CLI prefix only if no direct match.
- `shared/DocGeneration.Core.Shared/ToolFileNameBuilder.cs`  
  `ResolveFamilyFileName()` now tries `familyName.Replace(' ', '_')` as a secondary key when direct lookup fails.

**Tests added:** `ToolFamilyCleanupStepTests.Step4_UsesDecomposedNamespace_AsFamilyName_Bug603`,  
`ToolFileNameBuilderTests.ResolveFamilyFileName_SpaceInFamilyName_TriesUnderscoreKey_Bug603`,  
`ToolFileNameBuilderTests.ResolveFamilyFileName_SpaceKey_NoUnderscoreMapping_FallsBackToFamilyName_Bug603`

---

## Bug #604 — BrandMappingValidator rejects prefix-covered namespaces

**Root cause:** `Program.cs` in `DocGeneration.Steps.Bootstrap.BrandMappings` extracted the first token of each CLI command as the namespace (e.g., `"extension"`) and required an exact match in brand mappings. Decomposed entries like `extension_azqr` were never checked.

**Files changed:**
- `mcp-tools/DocGeneration.Steps.Bootstrap.BrandMappings/Program.cs`  
  After exact-match fails, checks if any brand mapping key starts with `ns + "_"`. If yes, the namespace is considered covered and excluded from unmapped list.

**Tests added:** `BrandMapperValidatorTests.Validator_ConsidersNamespaceCovered_WhenDecomposedEntriesExist_Bug604`,  
`BrandMapperValidatorTests.Validator_ReportsUnmapped_WhenNamespaceHasNoExactOrPrefixMatch_Bug604`

---

## Bug #602 — Step 4 fails when Step 3 is skipped (tools/ empty)

**Root cause:** Step 4 (`ToolFamilyCleanupStep`) hard-failed if `tools/` directory didn't exist or was empty, with no fallback. When Step 3 is skipped (no AI steps), `tools/` is never populated.

**Files changed:**
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/ToolFamilyCleanupStep.cs`  
  Before failing, checks if `tools-raw/` exists and is non-empty; if so, uses it as the input directory. Logs `"INFO: Using tools-raw/ as fallback (tools/ not available)."`.

**Tests added:** `ToolFamilyCleanupStepTests.Step4_FallsBackToToolsRaw_WhenToolsDirectoryAbsent_Bug602`,  
`ToolFamilyCleanupStepTests.Step4_FallsBackToToolsRaw_WhenToolsDirectoryEmpty_Bug602`,  
`ToolFamilyCleanupStepTests.Step4_Fails_WhenBothToolsAndToolsRawAbsent_Bug602`

---

## Test Results

- `DocGeneration.PipelineRunner.Tests` — 12/12 ToolFamilyCleanup tests pass ✅
- `DocGeneration.Core.Shared.Tests` — 6/6 ResolveFamilyFileName tests pass ✅  
- `DocGeneration.Steps.Bootstrap.BrandMappings.Tests` — 17/17 pass ✅  
- `DocGeneration.Steps.ToolFamilyCleanup.Tests` — 880/881 pass (1 pre-existing `R_CG2` failure unrelated to these changes) ✅
