# Changelog

All notable changes to the Azure MCP Documentation Generator are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- **Prompt regression CI workflow** — New `.github/workflows/prompt-regression-ci.yml` runs prompt content validation and regression infrastructure tests on PRs touching `docs-generation/**/prompts/**` or `prompt-regression.sh`. Includes baseline management documentation. (#350)
- Azure Skills documentation generation pipeline (`skills-generation/`) — generates customer-facing docs for 24 Azure Skills (#365)
- `start-azure-skills.sh` entry point script
- 77 xUnit tests with 81.6% line coverage
- Skills Generation CI workflow (`.github/workflows/skills-generation-ci.yml`)

### Removed

- Deleted unused `test_coverage.cs` and `test_coverage.csx` from repository root (#349)

### Changed

- **Consolidated FindProjectRoot() into shared test utility** — Created `DocGeneration.TestInfrastructure` project with canonical `ProjectRootFinder` class (`FindSolutionRoot()`, `FindDocsGenerationRoot()`). Replaced 7 duplicate implementations across 5 test projects. (Issue #334)

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
