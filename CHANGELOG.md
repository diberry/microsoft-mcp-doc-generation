# Changelog

All notable changes to the Azure MCP Documentation Generator are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed

- **`start.sh` now copies the repo-root `.env` into `mcp-tools/.env` before running the pipeline** — the script now fails immediately with a clear message when `.env` is missing at the repo root, instead of letting credential validation fail later in bootstrap.

- **BootstrapStep no longer fails when a newer prerelease of `azure.mcp` is already installed** — `mcp-tool-version.txt` at repo root is now the single source of truth for the azure.mcp version. `BootstrapStep` reads this file and passes `--version` to `dotnet tool update`, preventing a downgrade from `3.0.0-beta.15` to the latest stable `2.0.2`. "Already at this version" exit codes are treated as success. `run-focus.sh` also reads from the same file. When the file is absent, the previous behaviour (update to latest) is preserved with a warning.

- **Cosmos regeneration no longer downgrades `azure.mcp` from prerelease to stable** — `run-focus.sh` now pre-installs `azure.mcp@3.0.0-beta.15` before dispatching any namespace and always forwards `--skip-npm-update` to `start.sh`, preventing `BootstrapStep` from attempting a conflicting stable-tool downgrade during focus runs.

- **#673: MCP Server tab now appears before CLI tab** — `CliTabWrapper.BuildTabBlock()` previously emitted the Azure MCP CLI tab first, then the MCP Server tab. The order is now corrected: MCP Server tab is emitted first, followed by the Azure MCP CLI tab. All existing `CliTabWrapperTests` updated to reflect the new ordering; new unit test `WrapWithTabs_McpServerTabAppearsBeforeCliTab` added. Resolves #673.

- **#664A: `prompt-preview.txt` never written for AI/Hybrid pipeline steps** — `ObservabilityWriter.WritePromptPreview()` added and called for all non-deterministic steps (Steps 2, 3, 4, 6) in `PipelineRunner.WriteObservabilityOutputs()`. Previously only deterministic steps wrote `prompt-preview-na.txt`, leaving AI/Hybrid steps without the file `StageOutputContract` expected, causing a spurious "observability contract missing file" warning on every AI step run. The new method writes a pipeline-level placeholder (`"AI step — prompt preview not captured at pipeline level."`) that satisfies the contract. Test `RunAsync_AiStep_MissingPromptPreviewLogsWarning` renamed to `RunAsync_AiStep_WritesPromptPreviewAndNoContractWarning` and updated to assert the file is present and no contract warning is emitted; `WritePromptPreview_CreatesFileWithExpectedContent` unit test added. Resolves #664 sub-issue A.

### Added

- **#666: Readable output folder timestamp format** — `GetDefaultOutputPath()` in `PipelineRequest.cs` now uses `yyyy-MM-dd-HHmmss` instead of `yyyyMMddTHHmmssfffZ`, producing folders like `generated-appconfig-2026-05-31-062940` that are easier to read and sort. CLI help text for `--output` updated to show the new format. Test regex updated to match new pattern. Resolves #666.

- **PRD-QUALITY Item A: Step 3 style guide** — `system-prompt.txt` for `ToolGenerationStep` now includes a "Style Guide for Tool Descriptions" section covering contraction rules (Microsoft Learn style), backtick conventions (CLI flags, example values, tool names), MCP acronym expansion on first body mention, and prohibited patterns (second-person phrases, marketing superlatives, deprecated product names). `user-prompt-template.txt` adds "Tone Consistency Heuristics" for active voice, lead-with-action, and avoiding service-level context in per-tool descriptions. `cli-prose-system-prompt.txt` updated with matching backtick conventions and prohibited patterns.
- **PRD-QUALITY Item A: TDD contract tests** — `StitcherStep3PromptHandlingTests.cs` added with 6 tests defining the new contract: `FamilyFileStitcher.Stitch()` does NOT apply contractions, MCP acronym expansion, VM acronym expansion, or bare example-value backtick wrapping (these are now Step 3 AI responsibilities).
- **PRD-QUALITY Item C: Step 4 post-assembly validation extended** — `ToolFamilyPostAssemblyValidator` now runs 6 additional inline checks after tool-family article assembly:
  - `GetRelatedToolsCompletenessIssues` — blocking; verifies every backtick-quoted term (4+ chars) in the `## Related tools` / `## See also` section resolves to an H2 section heading in the same article.
  - `GetToneMarkerWarnings` — warning; flags second-person phrases, marketing superlatives, and deprecated service names in article body text.
  - `GetBoilerplateRedundancyWarningsAsync` — warning; detects near-duplicate section bodies using `{namespace}.context.json` as the reference corpus (skipped when context file is absent).
  - `GetRelatedSectionHeaderWarnings` — warning; alerts when neither `## Related tools` nor `## See also` is present in the assembled article.
  - `GetMissingExampleIssues` — blocking; requires each tool section to contain an `Example prompts include:` header or recognized alternate.
  - `GetLowParameterCountWarnings` — warning; flags tool sections with fewer than 2 documented parameters.
  All six checks are inline methods on `ToolFamilyPostAssemblyValidator` with no new registered types. Results appear in the per-namespace validation report under `reports/tool-family-validation-{namespace}.txt` and in the step-envelope `validation.json`. Regression tests added for advisor, compute, and monitor namespaces; E2E test verifies `overallStatus: passed` in the step envelope for a valid advisor assembly. Implements PRD-QUALITY-2026-05-30 Item C.
- **Inspect mode (`--inspect`) for budget analysis** — New CLI mode that runs a pipeline step's input reducer without invoking the LLM, emitting structured JSON budget output including `estimatedTokens`, `budget`, `headroom`, and `topItems[]` (top-5 consuming sections/tools). When `--output` is provided, writes `inspect-budget.json` for programmatic consumption by CI gates and developer tooling. `start-only.sh` now passes `--inspect` through to `PipelineRunner`. Implemented via `RunInspectAsync()` in PipelineRunner. Enables fast token headroom verification before prompt changes. Documented in ARCHITECTURE.md with 4 annotated `--inspect` CLI examples. References PRD-QUALITY-2026-05-30 Item D. Resolves #[PR]
  - `ArticleOutlineBudgetValidator.InputTokenBudget` corrected from 100k to 150k (horizontal articles require higher budget)
  - Developer Loop documentation added to ARCHITECTURE.md Section "Developer Loop" with inspect mode walkthrough and 4 complete examples
  - CI gate pattern documented (inspect as preflight before full LLM run)

### Changed

- **PRD-QUALITY Item A: FamilyFileStitcher reduced from 17 to 14 steps** — Three redundant post-processing steps removed from `FamilyFileStitcher.Stitch()` because Step 3 AI prompts now handle these upstream:
  - Removed `AcronymExpander.ExpandAll` (former step 4) — AI now expands MCP/VM on first body use
  - Removed `ContractionFixer.Fix` (former step 9) — AI now produces contractions per Microsoft style
  - Removed `ExampleValueBackticker.Fix` (former step 11) — AI now wraps example values in backticks
  - Token delta: 0% (prompt changes affect AI quality, not tool-file character counts measured by `--inspect`)
- **P10: Pre-AI seam validators** — Registered `IPreAiValidator` implementations for pipeline steps 3, 4, and 6 using the `PreAiValidatorRegistry` from P9. Five validators added:
  - `ToolGenerationContextValidator` — validates `ToolName`, `ComposedContent` non-empty, `SchemaVersion == "1.0"` before step 3 AI calls.
  - `ToolGenerationBudgetValidator` — enforces 100,000-token input budget (via `ComposedContent.Length / 4`) before step 3 AI calls.
  - `FamilyStructureContextValidator` — validates `FamilyName`, `Sections.Count >= 1`, all section headings non-empty, `SchemaVersion == "1.0"` before step 4 AI calls.
  - `ArticleOutlineContextValidator` — validates `ArticleTitle`, `Sections.Count >= 2`, each section has `EvidenceItems.Count >= 1`, `SchemaVersion == "1.0"` before step 6 AI calls.
  - `ArticleOutlineBudgetValidator` — enforces 100,000-token input budget (via total evidence item length / 4) before step 6 AI calls.
  - 27 new unit tests covering valid and invalid inputs for all five validators. All 492 `DocGeneration.PipelineRunner.Tests` pass.

- **Item B: Pre-AI seam validator observability and E2E coverage** — Adds integration and smoke tests confirming validator outcomes are durably recorded in the per-step observability bundle. References PRD-QUALITY-2026-05-30 Item B.
  - `TryRunPreAiGateAsync_ValidatorFailure_IsRecordedInStepEnvelope` — integration test that runs a pipeline step whose pre-AI gate fires, then asserts `validationStatus: "failed"` is present in both `metrics.json` and the `step-result.json` envelope.
  - `RunAsync_AdvisorNamespace_ValidatorsFireForAllThreeSteps` — E2E smoke test that runs the advisor namespace with all three pre-AI gated steps (3, 4, 6), confirms each validator fired, and confirms `validationStatus` is recorded for every stage.

### Changed

- **Completed npm-to-dotnet migration** — Removed all Node.js scripts from `mcp-cli-metadata/`. CLI metadata extraction now uses `mcp-tools/McpCliMetadata/` exclusively. Updated CI workflows, preflight.ps1, and documentation. Closes #627.
- **PipelineRunner stage observability contract** — Each step now emits a standard observability bundle under `{output}/observability/{stepId}-{slug}/`: `summary.md`, `step-result.json`, `validation.json`, `prompt-preview.txt` (or `prompt-preview-na.txt` for deterministic steps), and `metrics.json`. Missing files are enforced at warning level so incomplete step instrumentation surfaces immediately without breaking existing runs.
- **Advisor golden behavioral-equivalence gate** — Added `fingerprint golden capture` / `fingerprint golden verify`, an advisor golden manifest fixture, a local capture helper script, and a `golden-diff` workflow job that enforces SHA-256 parity for deterministic outputs plus structural parity for AI-authored outputs.
- **Step 4 deterministic family structure builder** — Tool-family cleanup now constructs a typed `FamilyStructureContext` via `FamilyStructureBuilder` before AI cleanup, using precomputed `h2-headings/*.json` data for canonical section headings and order. The legacy subprocess path remains available as a fallback.

### Fixed

- **#664: Protect @mcpcli comment from AI movement** — `ImprovedToolGeneratorService.RemoveMcpCliComment()` removes the `<!-- @mcpcli ... -->` marker from content before it is sent to the AI, preventing the AI from relocating it. `RestoreMcpCliComment` already reinjected the marker after H1 when missing; with the pre-removal step, it now always reinjjects at the correct canonical position. Fixes misplaced "Example prompts include:" headers for ~9 tools (cosmos list, storage account-create, blob-upload, etc.). Two regression tests added: `RemoveMcpCliComment_RemovesFromContent` and `RemoveMcpCliComment_NoComment_ReturnsUnchanged`. Resolves #664.
- **PipelineRunner step-result enforcement** — Every PipelineRunner step wrapper now writes a shared `step-result.json` envelope to `{output}/step-<id>-<slug>/` after `ExecuteAsync` and post-validation complete, including dry-run placeholders. Missing envelopes are now treated as fatal for non-warn steps, while warn-only steps continue with a warning.
- **`AzmcpRunner` PATH resolution on Windows** — `Process.Start` with `UseShellExecute=false` does not reliably resolve `.cmd` batch wrappers from `PATH`. `AzmcpRunner` now walks each `PATH` directory to locate `azmcp.cmd` (Windows) or `azmcp` (non-Windows) and passes the full absolute path to `Process.Start`, falling back to the bare name if not found. This fixes `azmcp` invocation failures when the `azure.mcp` dotnet global tool is installed at `~/.dotnet/tools/azmcp.cmd`. Extracted to `internal static ResolveBinaryPath()` for testability.
- **`BootstrapStep` migrated from npm to dotnet tool** — The bootstrap step previously ran `npm install -g @azure/mcp@latest` to update the MCP CLI. This has been replaced with `dotnet tool update azure.mcp --global`, matching the current installation method. Removed the dead `GetNpmExecutable()` helper and unused `CaptureCommandOutputAsync` method. The `--skip-npm-update` / `SkipNpmUpdate` flag is preserved for backwards compatibility with existing CLI scripts.


### Changed
- Added .NET CLI metadata extractor (`mcp-tools/McpCliMetadata/`) alongside existing Node.js scripts. The `azmcp` binary is now invoked via `Process.Start` in a typed C# console app; `preflight.ps1` calls the .NET project instead of npm. The folder `test-npm-azure-mcp/` was renamed to `mcp-cli-metadata/` (all Node.js scripts and version snapshot directories preserved). Closes #627, PR #628.

### Added

- **Validation Gate CI workflow — Phase 4** (`.github/workflows/validation-gate.yml`) — New GitHub Actions workflow that runs on every PR to `main`. Builds `DocGeneration.PipelineRunner` to verify Steps 7 and 8 compile, then runs `Test-ArticleHealth.ps1` and `Scan-McpToolCoverage.ps1` against any changed tool-family articles (falling back to committed test fixtures as a smoke test when no articles are changed). Results are posted as a PR comment and the gate starts in **warn-only mode** (non-blocking). To promote to blocking after 2 clean weeks, change `GATE_MODE: warn` → `GATE_MODE: block` in the workflow file. Implements Phase 4 of #574.

- **Integrated Validation Pipeline — Phase 2** (`ArticleHealthValidatorStep`) — New pipeline step (ID 7) wrapping `Test-ArticleHealth.ps1` to validate generated tool-family articles after Step 4 completes. Uses a `runId` echo contract to detect stale artifacts, normalizes script verdicts (`Pass`/`Warn`/`Fail`/`ScriptError`/`ArtifactError`) through `ValidationResultNormalizer`, and reads `mcp-tools/data/validation-gate-config.json` to decide whether warn-verdict findings are blocking (`"block"`) or advisory (`"warn"`). Initial rollout is warn-only (`FailurePolicy.Warn`) so no existing pipeline runs are broken. Added `ValidationScriptRunner`/`IValidationScriptRunner` for typed subprocess invocation, `validation-gate-config.json` (gateMode: `"warn"`), `validation-waivers.json` (empty initial set), and 12 new unit tests. All 400 `DocGeneration.PipelineRunner.Tests` pass. Implements Phase 2 of #574.

- **Repo-local validation scripts and tests** — Moved `Test-ArticleHealth.ps1`, `Scan-McpToolCoverage.ps1`, and their 72 Pester tests into `mcp-tools/validation/` so deterministic article-health and coverage checks now live beside the generation source instead of only in the project-dina ops hub. Added `mcp-tools/validation/README.md`, `docs/VALIDATION-RUNBOOK.md`, and a dedicated `pester-tests` CI job for the relocated suite. Implements Phase 1 of #574.
- **Pipeline Observability — Trace AI Requests/Responses** — New shared tracing infrastructure (`shared/DocGeneration.Core.Tracing/`) that captures full pipeline execution traces including step timing, AI prompt/response content, token usage, and model metadata. Both the Skills pipeline (`SkillPipelineOrchestrator`, `AzureOpenAiRewriter`) and MCP pipeline (`PipelineRunner`, `GenerativeAIClient`) now emit structured trace files after every run: `pipeline-trace.json` (full execution graph), `ai-interactions.json` (all LLM calls with prompts/responses), and `summary.md` (human-readable report). Tracing is always-on with zero configuration, uses in-memory collection with single end-of-run flush (≤2% overhead), and the `NullTracer` pattern ensures no breaking changes to existing APIs. Implements #590.

### Changed

- **`update-azure-mcp.yml` workflow commits directly to main** — The nightly version check workflow now commits package updates directly to `main` instead of creating pull requests. Breaking changes (major version bumps) are blocked and require manual review. Added inline tests, error handling for git operations, concurrency control (`cancel-in-progress: true`), and directory validation before staging. (#621)
- **`cli-examples.md` moved to version-specific folders** — `generate-cli-examples.js` now outputs `cli-examples.md` inside version folders (e.g., `3.0.0-beta.11+hash/cli-examples.md`) instead of the `test-npm-azure-mcp` root. Each version snapshot now includes both `tools-list.json` and `cli-examples.md`. (#621)

### Added

- **Namespace mapping emission** (`BootstrapStep`) — After brand validation succeeds, `BootstrapStep` now emits `generated/namespace-mapping.json` at the root of the global output directory. The file is a machine-readable snapshot of every brand mapping entry joined with the tools that belong to it, keyed by `McpServerName`. Fields include `generated_at`, `source_version`, `namespace_count`, `tool_count`, and a `namespaces` dictionary with per-namespace `display_name`, `file_name`, `short_name`, `merge_group`, and sorted `tools` list. Downstream validation agents (coverage audit, drift detection, agents marketplace) can now consume this stable artifact instead of re-deriving the mapping each run. Implements `INamespaceMappingEmitter` / `NamespaceMappingEmitter` in `DocGeneration.PipelineRunner/Services/`. Closes #618.

- **Timestamped default output directories for PipelineRunner** — When `DocGeneration.PipelineRunner` is invoked without `--output`, it now appends a UTC timestamp suffix to the default generated directory (`generated-<timestamp>` or `generated-<namespace>-<timestamp>`). This prevents stale `generated-*` folders from colliding across overlapping regression runs and local iterations while preserving explicit `--output` paths unchanged. Closes #611.
- **Step 4 tool-family generation for extension sub-namespaces** (`extension_azqr`, `extension_cli_generate`) — `ToolReader.ExtractFamilyNameFromContent` previously took only the first whitespace-delimited token of the `@mcpcli` command as the family name. For multi-token namespaces like `"extension azqr"` and `"extension cli generate"`, this returned `"extension"` instead of the correct brand-mapping keys `"extension_azqr"` and `"extension_cli_generate"`. The subprocess processed the wrong family, wrote output to the wrong filename, and the pipeline's expected output check failed with "Subprocess output: Failed: 0". Fix: greedy longest-prefix brand-mapping lookup (up to 4 tokens, underscore and space variants) before falling back to first token for single-token namespaces. 4 regression tests added.

- **Consistent parameter descriptions for global variables** — `BuildParameterManifest` now accepts an optional `canonicalDescriptions` dictionary from `common-parameters.json`. When a tool-specific parameter matches a common/global parameter name (e.g., `--subscription`, `--resource-group`, `--tenant`), the canonical description is used instead of the inconsistent per-tool CLI source description. This ensures identical language across all generated tool articles for shared parameters. Closes #592.

- **Strip "Defaults to..." from Required parameter descriptions** — `RequiredParameterDescriptionSanitizer` now removes contradictory default-value language (`Defaults to X.`, `Default is X.`, `Default: X.`, `If not specified, defaults to X.`, etc.) from parameter descriptions when the parameter is marked Required. Required parameters have no default — users must always provide a value — so including default language is confusing. Closes #593.

### Added

- **Pipeline Output Regression workflow** — Added `.github/workflows/pipeline-output-regression.yml` to classify PR changes, run deterministic regression checks for representative namespaces, and run AI regression gates on trusted PRs when prompts or AI-involved generation steps change. The workflow publishes fingerprint and prompt-regression artifacts and fails safely for fork PRs that require AI credentials. Closes #599.
- **PR template with regression evidence block** — Added `.github/PULL_REQUEST_TEMPLATE.md` with structured YAML evidence block for pipeline regression results, change classification checkboxes, and validation checklist.
- **Pipeline regression contributor runbook** — Added `docs/pipeline-regression-runbook.md` documenting when the gate runs, how to interpret results, handle failures, update baselines, and run local verification.
- **Source Outline Cataloging step** (`skills-generation`) — New pipeline step inserted between fetch and parse in `SkillPipelineOrchestrator`. Extracts all `##` and `###` headings from each source `SKILL.md`, compares them against known mapping rules (`HeadingMappingRules`), and emits warnings for unmapped headings. Results are persisted to `data/source-outlines.json` after batch processing. New types: `SkillOutline`, `HeadingEntry`, `HeadingMappingRules`, `SourceOutlineCataloger`, `SourceOutlineWriter`. Implements PRD-587. (#587)
- **Install latest @azure/mcp before tools-list generation** — Bootstrap step now runs `npm install @azure/mcp@latest --save` before the pinned `npm install`, ensuring the tools-list is always generated from the latest published package. Failure is fatal (the whole point is to use the latest). New `--skip-npm-update` CLI flag opts out for offline or reproducible builds. (#)

- **Fingerprint baseline comparison gate** — `PipelineRunner.RunAsync()` now supports an optional post-pipeline fingerprint gate (`--run-fingerprint-gate`) that snapshots `generated-*` output directories and diffs them against `fingerprint-baseline.json`. Detects unintended output drift (file count/size changes) across namespaces. Skips safely when no baseline exists. Implemented via `IFingerprintGate` / `FingerprintGate`, which invokes `DocGeneration.Tools.Fingerprint` as a subprocess.

- **Prompt regression gate** — `PipelineRunner.RunAsync()` now supports an optional post-pipeline prompt regression gate (`--run-prompt-regression-gate`) that runs `DocGeneration.PromptRegression.Tests` as a subprocess. Catches prompt template regressions by running the full regression suite after generation. Implemented via `IPromptRegressionGate` / `PromptRegressionGate`.

- **CHANGELOG decision gate**— `PipelineRunner.RunAsync()` now checks the upstream `servers/Azure.Mcp.Server/CHANGELOG.md` before processing each namespace. Namespaces with no entries in CHANGELOG versions >= the current CLI version are skipped with an informational message, preventing wasted PRs on azure-dev-docs-pr. New namespaces (no existing article) always process. Gate can be disabled via `--skip-changelog-gate`. Implements `IChangelogGate` / `ChangelogGate` / `ChangelogParser`. Closes #571.

### Changed

- **`DefaultMcpBranch` updated to `main`** — `PipelineRequest.DefaultMcpBranch` changed from `release/azure/2.x` to `main` to track the current default upstream branch. Override via `--mcp-branch` or `MCP_BRANCH` env var. (#571)

### Added

- **Pipeline smoke tests** — New `DocGeneration.PipelineRunner.SmokeTests` project provides end-to-end smoke tests that run the full pipeline on small test namespaces (quota, redis) and verify output matches committed baseline fixtures. Tests serve as a safety net for refactoring, ensuring code changes don't alter pipeline behavior or output structure. Includes `BaselineManager` for capturing/comparing golden files and comprehensive documentation on updating baselines. Closes #534. PR #[PR_NUMBER]

### Changed

- **Infrastructure migrated from Azure OpenAI to Azure AI Services** — Updated Bicep templates to use `kind: 'AIServices'` instead of `kind: 'OpenAI'` for Microsoft Foundry compatibility. Upgraded API version to `2025-06-01`. Resource names preserved as `oai-*` for deployment continuity (avoids destroy/recreate of existing Azure resources). The SDK (Azure.AI.OpenAI) and environment variables (FOUNDRY_*) remain unchanged and are compatible with both Azure OpenAI and Foundry endpoints. (#506)

### Fixed

- **Per-tool AI timeout and fallback in Step 3** — The AI improvement phase now applies a configurable per-tool timeout (default: 5 minutes) via `CancellationToken`. When a tool's AI call hangs or fails, the pipeline saves the original composed content as fallback and continues processing remaining tools. Previously, a single hanging Azure OpenAI call would freeze the entire pipeline indefinitely, preventing Step 4 (cleanup/stitching) from ever running. External (caller) cancellation is properly distinguished from per-tool timeout and propagated immediately. (#507)

### Added

- **CLI tab generation for tool family articles** — Azure MCP CLI tab now appears alongside the existing MCP Server tab in tool family pages, enabling users to view CLI equivalents for server-based tools. Includes CLI tool descriptions, parameter tables, and example commands. Uses `console` devlang slug for syntax highlighting. Features `CliTabConfig` namespace allowlist for progressive rollout, `CliTabValidator` for content verification, `GlobalSwitchFilter` to exclude common Azure CLI switches from parameter tables, and `CliProseImprover` for sentence-boundary-aware description formatting. (#545)
- **Alphabetical tool ordering in stitcher** — Tools within tool family pages are now sorted alphabetically by display heading (case-insensitive, with FileName tie-breaker) during stitching. Multi-resource families sort tools within each resource group while preserving group arrival order. This normalizes output for varying input permutations. (#501)
- **Branch-aware content generation (`--mcp-branch`)** — The pipeline now fetches upstream files (`azmcp-commands.md`, `e2eTestPrompts.md`) from a configurable branch of `microsoft/mcp`. Default: `release/azure/2.x`. Override via `--mcp-branch <branch>` CLI flag or `MCP_BRANCH` environment variable. Local fallback preserved for offline use. (#387)
- **Vale CLI prose linter for docs-generation** — Integrated Vale with Microsoft style rules into the docs-generation pipeline. Includes `.vale.ini` config, `lint-vale.sh`/`lint-vale.ps1` scripts, Pester tests, and a non-blocking CI job in `build-and-test.yml`. Suppresses same false positives as skills-generation (FirstPerson, Dashes, Quotes, HeadingAcronyms). (#368)
- Azure Skills documentation generation pipeline (`skills-generation/`) — generates customer-facing docs for 24 Azure Skills (#365)
- `start-azure-skills.sh` entry point script
- 77 xUnit tests with 81.6% line coverage
- Skills Generation CI workflow (`.github/workflows/skills-generation-ci.yml`)
- JSON schemas for core configuration files in `mcp-tools/data/schemas/` (#355)
- Configuration registry document (`docs/configuration-registry.md`) (#355)
- Project consolidation evaluation document (`docs/project-consolidation-evaluation.md`) (#353)
- Core.Shared extraction plan document (`docs/core-shared-extraction-plan.md`) (#354)
- Text transformation migration plan document (`docs/text-transformation-migration-plan.md`) (#351)
- Parallel AI evaluation document (`docs/parallel-ai-evaluation.md`) (#356)

### Removed

- Deleted unused `test_coverage.cs` and `test_coverage.csx` from repository root (#349)
- Removed unused NUnit and NUnit3TestAdapter from central package management (#349)

### Changed

- Migrated 3 NUnit test projects (SkillsRelevance, TextTransformation, HorizontalArticles) to xUnit for framework consistency (#349)
- **Consolidated FindProjectRoot() into shared test utility**— Created `DocGeneration.TestInfrastructure` project with canonical `ProjectRootFinder` class (`FindSolutionRoot()`, `FindMcpToolsRoot()`). Replaced 7 duplicate implementations across 5 test projects. (Issue #334)

### Fixed

- **TOCTOU race condition in PromptHasher** — `HashFileAsync` now captures file metadata before reading content and verifies the file wasn't modified during read, throwing `IOException` on mismatch. Prevents inconsistent snapshots where hash comes from old content but size/timestamp from new file. 3 new tests. (Issue #332)
- **Copyright headers** — Added missing MIT license headers to `PromptHasher.cs` and `PromptHasherTests.cs` to match project convention. (Issue #335)

### Added

- **AI response archival infrastructure** — `AiResponseArchiveEntry` model and `AiResponseArchiveWriter` in `DocGeneration.Core.Shared`. Writes raw AI responses to `{outputDir}/ai-responses/{step}-{toolName}.json` for audit trail. Enabled by default; set `ARCHIVE_AI_RESPONSES=false` to disable. 15 new tests. (Issue #340)
- **Post-validator for Step 3 (ToolGeneration)** — `ToolGenerationValidator` detects leaked pipeline template tokens, empty/truncated tool files (<50 chars), and content loss (>50% shorter than raw input). Registered alongside existing `ToolGenerationOutputValidator`. 18 new tests. (Issue #341)
- **Token usage tracking models** — `TokenUsageRecord` and `TokenUsageSummary` types in `DocGeneration.Core.Shared` for tracking Azure OpenAI token consumption per AI call. `StepResultFile` v3 schema adds nullable `tokenUsage` field. Backward-compatible with v1/v2 results. 12 new tests. (Issue #212)

- **Prompt versioning documentation** — `docs/prompt-versioning.md` covering `PromptHasher`, `PromptSnapshot`, `PromptTokenResolver`, `StepResultFile` v2 schema, usage examples, and future pipeline integration notes. Added to README navigation. (Issue #333)
- **Prompt versioning system** — `PromptHasher` utility (SHA256) + `PromptSnapshot` record + `StepResultFile` v2 schema with `promptSnapshots` field. Backward-compatible: v1 results still deserialize. 16 new tests. (Issue #211)
- **Prompt regression testing framework** — Runner script (`prompt-regression.sh`) + 5 regression comparison tests + baselines for 5 representative namespaces. Detects quality regressions when prompts change. (PR #329, Issue #214)
- **CI integration documentation** — Local development commands, CI pipeline structure, test project inventory, and debugging guide. (PR #328, Issue #213)
- **Baseline fingerprinting tool**(`DocGeneration.Tools.Fingerprint`) — Snapshot and diff generated output for regression detection. Supports `--snapshot` and `--diff` modes with CI-gatable exit codes. 58 tests. (PR #324, Issue #209)
- **Comprehensive README documentation navigation** — 22 documents organized across 8 categories replacing flat 6-link list. (PR #325)
- **Prompt review P0/P1 fixes** — Removed redundancy, dead code, and fixed bugs across pipeline prompts. (PR #323, Issue #294)
- **Shared Acrolinx rules** — Standardized compliance rules across all AI system prompts. (PR #323)
- **Prompt hygiene tests** — 14 cross-cutting tests enforcing no legacy duplicate prompts, shared Acrolinx rules in all AI steps, and Step 3 scope boundaries. (Issue #294)

### Changed

- **Step 3 prompt scope narrowed** — Removed 35 lines of formatting/style rules (backtick formatting, CLI switch conversion, LLM guidance removal) that duplicated Step 4. Step 3 now focuses on content quality improvement only. (Issue #294)
