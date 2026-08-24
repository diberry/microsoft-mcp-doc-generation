# Architecture

The Azure MCP Documentation Generator is a typed .NET pipeline that transforms raw Azure MCP CLI metadata into 800+ publication-ready markdown files across 52 Azure service namespaces.

## Pipeline Authority

The runner is the pipeline definition; the GitHub Actions workflow is a CI host.

## System Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                        start.sh (bash wrapper)                   │
│  Parses: namespace, steps, --skip-deps, extra flags              │
└────────────────────────┬─────────────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────────────┐
│             DocGeneration.PipelineRunner (.NET 9)                 │
│                                                                  │
│  PipelineCli  →  PipelineRequest  →  PipelineRunner.RunAsync()   │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ StepRegistry (typed steps with dependency validation)      │  │
│  │                                                            │  │
│  │  Step 0: BootstrapStep ──────────── Global (runs once)     │  │
│  │  Step 1: AnnotationsParametersRaw ─ Namespace-scoped       │  │
│  │  Step 2: ExamplePrompts ─────────── Namespace (AI)         │  │
│  │  Step 3: ToolGeneration ─────────── Namespace (AI)         │  │
│  │  Step 4: ToolFamilyCleanup ──────── Namespace (AI+Retry)   │  │
│  │  Step 5: SkillsRelevance ────────── Namespace (Warn-only)  │  │
│  │  Step 6: HorizontalArticles ─────── Namespace (AI)         │  │
│  │  Step 7: ArticleHealthValidator ─── Namespace (Warn-only)  │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

## Data Flow

Raw CLI metadata flows through each step, transformed into richer content at each stage:

```
dotnet run --project mcp-tools/McpCliMetadata (Azure MCP package)
  │
  ▼
Step 0: Bootstrap ─────────────────────────────────────────────────
  │  • dotnet run McpCliMetadata → cli-output.json, cli-namespace.json, cli-version.json
  │  • Build .NET solution
  │  • Brand mapping validation → reports/
  │  • E2E test prompt parsing → e2e-test-prompts/
  │  • Deterministic H2 headings → h2-headings/
  │  • Namespace mapping emission → namespace-mapping.json
  │
  ▼
Step 1: Annotations + Parameters + Raw Tools ──────────────────────
  │  • cli-output.json → annotations/*.md (tool metadata flags)
  │  • cli-output.json → parameters/*.md (parameter tables)
  │  • cli-output.json → tools-raw/*.md (raw tool markdown)
  │  • Parameter tables now keep ALL CLI parameters, including common
  │    infrastructure/scoping flags such as tenant, auth-method,
  │    retry-*, and subscription
  │  • Parameter tables and CLI example command flags use the same
  │    stable required-first order: required parameters first, then
  │    optional parameters, preserving source order within each group
  │  • The "Required or optional" column is derived only from the
  │    cli-output.json required boolean; description/default wording is
  │    preserved as descriptive text and never changes requiredness
  │  Uses: Handlebars templates, static-text-replacement.json
  │
  ▼
Step 2: Example Prompts (AI + Deterministic Repair) ───────────────
  │  • tools-raw/ + cli-output.json → example-prompts/*.md
  │  • Azure OpenAI generates 5 natural language prompts per tool
  │  • DeterministicPromptRepairer replaces placeholder/fabricated
  │    parameter values with realistic deterministic values (runs
  │    AFTER AI parse, BEFORE CredentialSanitizer)
  │  • Retry feedback now adds actionable repair guidance: missing
  │    parameter names, prompt-slot suggestions, and a concrete rewrite
  │    example for the next AI attempt
  │  • If canonical-name injection still misses a required parameter
  │    after sanitization, DeterministicPromptRepairer injects a
  │    display-name-based last-resort fallback prompt
  │  • Per-tool repair telemetry → repair-telemetry/*.json
  │  • Validation checks parameter coverage in generated prompts
  │
  ▼
Step 3: Tool Composition + AI Improvements ────────────────────────
  │  • Merges: tools-raw/ + example-prompts/ + parameters/ + annotations/
  │  • → tools-composed/*.md (mechanically merged)
  │  • → tools/*.md (AI-improved descriptions, clarity, style)
  │
  ▼
Step 4: Tool Family Assembly (AI + Retry + Validation) ────────────
  │  • tools/*.md → tool-family/{namespace}.md (one article per service)
  │  • `FamilyStructureBuilder` deterministically emits
  │    `FamilyStructureContext` (family name, section order, headings,
  │    source content, schema version) before AI metadata generation
  │  • Pre-assembly `ParameterCrossCheckService` compares each tool's
  │    parameter table to the Step 1 parameter manifest and strips any
  │    hallucinated parameter rows before the family article is stitched
  │  • H2 headings come from bootstrap `h2-headings/*.json`
  │  • AI generates: frontmatter, intro, related content
  │  • Post-processing: MCP acronym expansion, frontmatter enrichment,
  │    duplicate example stripping, annotation table normalization
  │    (`AnnotationTableFixer` converts any inline annotation lines to
  │    the 3-row markdown table format deterministically)
  │  • Post-assembly validator checks: tool count, cross-references,
  │    parameter coverage, branding
  │  • Retries up to 2x on validation failure
  │  • CLI-variant emission (`CliVariantWriter`): always writes TWO
  │    per-namespace files — canonical `tool-family/{namespace}.md`
  │    (plain MCP, no CLI tabs) and `tool-family/{namespace}-cli.md`
  │    (CLI tabs when available, else an exact copy of the canonical)
  │  Runs in isolated temp workspace for parallel safety
  │
  ▼
Step 5: Skills Relevance (non-blocking) ───────────────────────────
  │  • tools/ → skills-relevance/*.md (GitHub Copilot skills mapping)
  │  • Output lookup uses the same sanitized filename strategy as the
  │    writer, so multi-token/extension namespaces resolve correctly
  │  • Zero relevant skills is treated as warning-only success rather
  │    than a missing-artifact failure
  │  • Warning-only — failures don't stop the pipeline
  │
  ▼
Step 6: Horizontal Articles (AI) ──────────────────────────────────
  │  • tools/ + cli-output.json → horizontal-articles/*.md
  │  • One overview article per namespace: capabilities, scenarios,
  │    prerequisites, RBAC roles, best practices
  │  • AI generation = one bounded per-tool call per tool, plus four
  │    small, focused namespace-fragment calls (overview / access
  │    [prerequisites+RBAC] / best practices / links), deterministically
  │    stitched into the same NamespaceSummaryAIData shape — replaces
  │    the former single broad namespace-summary call, which requested
  │    all seven namespace fields in one response and was prone to
  │    truncation on reasoning models like gpt-5-mini
  │  • Every per-tool and namespace-fragment prompt artifact appends the
  │    corresponding AI response. Truncated calls retain their partial
  │    response and failure details; calls with no response record that
  │    explicitly, so failed calls remain diagnosable from the same file.
  │  • Step 6 requests low reasoning effort and JSON response format so
  │    bounded fragment budgets are spent on visible structured output
  │    instead of being exhausted by hidden reasoning.
  │  • All four namespace fragments start with a 1,500-output-token
  │    ceiling. An empty/whitespace token-truncated fragment gets exactly
  │    one adaptive retry at 3,000 tokens; partial truncations don't retry,
  │    and both attempts remain in the same component prompt artifact.
  │  • Per-tool prompts bind command, description, parameter count, and
  │    metadata from the nested tool context used by the generator.
  │  • Only transient network/time-out, HTTP 429, and HTTP 5xx failures
  │    retry; truncation, malformed JSON, cancellation, and client errors
  │    fail immediately instead of repeating the same-budget request
  │  • Missing focused prompt pairs fail before any AI request; Step 6
  │    never falls back to the obsolete monolithic generation path
  │  • ArticleContentProcessor validates and transforms AI output
  │  • Prompt/template paths are resolved via HorizontalArticleGenerator(
  │    mcpToolsRoot: context.McpToolsRoot) — always anchored to mcp-tools/
  │    regardless of the process working directory
  │
  ▼
Step 7: Article Health Validation (non-blocking) ──────────────────
  │  • Invokes Test-ArticleHealth.ps1 on tool-family/*.md
  │  • Checks: placeholder tokens, required frontmatter, broken links
  │  • Gate mode: "warn" (advisory) or "block" (fail pipeline)
  │  • Configured via mcp-tools/data/validation-gate-config.json
  │  • Depends on Step 4; warn-only — failures don't stop the pipeline
  │  • PR validation-gate smoke fallback uses
  │    Get-ArticleHealthSmokeFixtures.ps1, an explicit healthy-fixture
  │    allowlist, so negative health fixtures and coverage fixtures stay
  │    in their dedicated tests instead of becoming baseline gate failures
  │
  ▼
Final Output ──────────────────────────────────────────────────────
  generated-{namespace}/
  ├── tool-family/{namespace}.md         ← Primary deliverable (plain, no CLI tabs)
  ├── tool-family/{namespace}-cli.md     ← CLI-tab variant (always emitted)
  ├── horizontal-articles/{namespace}.md ← Overview article
  ├── annotations/*.md                   ← Include files
  ├── parameters/*.md                    ← Include files
  ├── example-prompts/*.md               ← Include files
  ├── observability/{stepId}-{slug}/     ← 5-file step observability contract
  └── reports/                           ← Validation reports

Post-Assembly: Multi-Namespace Merge (AD-011) ────────────────────
  • Runs AFTER all namespaces complete (called by start.sh)
  • Reads mergeGroup config from brand-to-server-mapping.json
  • Primary namespace: keeps frontmatter + overview + related content
  • Secondary namespaces: contribute tool H2 sections only
  • Updates tool_count in merged article frontmatter
  • Example: monitor (15 tools) + workbooks (5 tools) → monitor.md (20 tools)
```

## Step Contract

Every pipeline step implements the `IPipelineStep` interface:

```csharp
public interface IPipelineStep {
    int Id { get; }
    string Name { get; }
    StepScope Scope { get; }            // Global or Namespace
    FailurePolicy FailurePolicy { get; } // Fatal or Warn
    IReadOnlyList<int> DependsOn { get; }
    int MaxRetries { get; }
    ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken ct);
}
```

Steps declare their dependencies, failure policy, and whether they need AI configuration. The runner validates dependencies before execution and handles retries automatically.

## Step Details

| Step | Class | AI? | Failure | Retries | Key Outputs |
|------|-------|-----|---------|---------|-------------|
| 0 | `BootstrapStep` | No | Fatal | 3 | `cli/`, `h2-headings/`, `e2e-test-prompts/`, `namespace-mapping.json` |
| 1 | `AnnotationsParametersRawStep` | No | Fatal | 0 | `annotations/`, `parameters/`, `tools-raw/` |
| 2 | `ExamplePromptsStep` | Yes | Fatal | 0 | `example-prompts/` |
| 3 | `ToolGenerationStep` | Yes | Fatal | 0 | `tools-composed/`, `tools/` |
| 4 | `ToolFamilyCleanupStep` | Yes | Fatal | **2** | `tool-family/`, `reports/` |
| 5 | `SkillsRelevanceStep` | No | **Warn** | 0 | `skills-relevance/` |
| 6 | `HorizontalArticlesStep` | Yes | Fatal | 0 | `horizontal-articles/` |
| 7 | `ArticleHealthValidatorStep` | No | **Warn** | 0 | `article-health.json`, `validation-summary.md` |
| 8 | `CoverageAuditStep` | No | **Warn** | 0 | `coverage-audit.json`, `validation-summary.md` |

### Dependencies

```
Step 1 → (no deps, uses CLI metadata from Step 0)
Step 2 → depends on Step 1
Step 3 → depends on Step 2
Step 4 → depends on Step 3
Step 5 → (no deps, reads tools/ directly)
Step 6 → (no deps, reads tools/ + cli-output.json)
Step 7 → depends on Step 4 (validates tool-family/ output)
Step 8 → depends on Steps 4 and 7 (tool coverage audit)
```

## Key Design Decisions

### Typed .NET Orchestrator (PipelineRunner)

The pipeline migrated from PowerShell scripts to a typed C# orchestrator. This provides:
- **Compile-time safety** for step registration and dependency declarations
- **Integrated retry logic** for AI-dependent steps
- **Post-validation framework** (`IPostValidator`) attached to specific steps
- **Isolated workspaces** via `WorkspaceManager` for parallel execution
- **Per-step execution envelopes** written to `{output}/step-<id>-<slug>/step-result.json` after each wrapper completes so downstream automation can inspect normalized status, outputs, validation state, and timing without reading step-specific logs
- **Per-step observability bundles** written to `{output}/observability/{stepId}-{slug}/` with `summary.md`, `step-result.json`, `validation.json`, `prompt-preview.txt` (or `prompt-preview-na.txt`), and `metrics.json`; missing files log warnings so instrumentation gaps are visible without breaking the pipeline

Legacy PowerShell scripts remain in `mcp-tools/scripts/` as fallback.

### Behavioral Equivalence CI Gate

`DocGeneration.Tools.Fingerprint` also maintains advisor golden manifests for behavioral-equivalence checks:

- Deterministic outputs (`annotations/`, `parameters/`, `h2-headings/`, `cli/`, `reports/`, `logs/`, `common-general/`, and root files) are compared by SHA-256.
- AI outputs (`tools/`, `tool-family/`, `horizontal-articles/`, `example-prompts/`, `e2e-test-prompts/`) are compared structurally by required top-level keys and H2/section-count tolerance (±1).

The `golden-diff` workflow job regenerates advisor output and verifies it against `mcp-tools/DocGeneration.PipelineRunner.Tests/Fixtures/GoldenSnapshot/advisor/golden-manifest.json`.

### Isolated Workspaces (Step 4)

Step 4 runs in a temporary directory (`pipeline-runner-step4-{guid}`) to enable parallel namespace execution. Files are copied in, generation runs in isolation, and outputs are copied back. This prevents file conflicts when multiple namespaces run simultaneously.

### Post-Assembly Validation (Step 4)

After Step 4 generates a tool-family article, `ToolFamilyPostAssemblyValidator` checks:
- **Tool count integrity** — frontmatter `tool_count` matches H2 sections and tool files
- **Cross-reference check** — every tool file has a matching article section
- **Source JSON consistency** — article `@mcpcli` markers, frontmatter `mcp-cli.version`, `tool_count`, documented parameter names, and required source parameters match the loaded CLI metadata for the namespace
- **Required parameter coverage** — example prompts mention all required parameters
- **Branding consistency** — no "CosmosDB", "this command", etc.

If validation fails, Step 4 retries (up to 2 attempts) since AI output is non-deterministic.

### CLI-Tab Variant Emission (Step 4)

After a tool-family article is assembled and validated, Step 4 always emits **two**
per-namespace files via `CliVariantWriter`:

- **`tool-family/{namespace}.md`** — the canonical article, plain MCP content with **no**
  CLI tabs. This file is never modified by the CLI-tab step.
- **`tool-family/{namespace}-cli.md`** — the CLI-tab variant. When CLI tabs are enabled for
  the namespace (`cli-tab-config.json`) and CLI content is available, `Shared.CliTabWrapper`
  injects `#### [Azure MCP CLI]`/`#### [MCP Server]` tabs keyed off the
  `<!-- @mcpcli {command} -->` markers. When CLI tabs are disabled or no CLI data exists, the
  variant is written as an **exact copy** of the canonical article — guaranteeing exactly two
  files per namespace.

Both the in-process (reducer) and subprocess-fallback generation paths route through the same
`ApplyCliTabWrappingAsync` → `CliVariantWriter.WriteVariantsAsync` logic, so the two-file
guarantee holds regardless of path. CLI-variant write failures are non-fatal (added as
warnings), so they never fail the pipeline.

> **Multi-namespace merge covers both variants:** the multi-namespace merge
> (`merge-namespaces.sh`) merges the `-cli.md` variant under the **same rules** as the
> canonical article — for each merge group it produces `{primary}-cli.md` from the members'
> `{member}-cli.md` files (primary frontmatter/overview/related + all members' tool sections
> in order + updated `tool_count`), preserving the `#### [Azure MCP CLI]`/`#### [MCP Server]`
> tab markers. The canonical merge is required (a missing member article skips the whole
> group); the `-cli.md` merge is best-effort and never blocks the canonical merge. The typed
> `NamespaceMerger.Merge` contract is variant-agnostic and is regression-locked by a
> `NamespaceMergerTests` CLI-tab test.

### Deterministic Post-Processing

The `FamilyFileStitcher.Stitch()` method chains 9 deterministic fixes after AI assembly:
1. H2 stripping from metadata (remove AI-generated H2 lines from frontmatter section)
2. Tool section assembly (merge individual tool H2 blocks)
3. Related content assembly (append related content section)
4. `PostProcessor.ExpandMcpAcronym()` — expand "MCP" on first body mention
5. `FrontmatterEnricher.Enrich()` — inject required Microsoft Learn fields
6. `DuplicateExampleStripper.Strip()` — remove duplicate non-canonical example blocks, or canonicalize a section's only example-prompt block back to `Example prompts include:`
7. `AnnotationSpaceFixer.Fix()` — blank line between annotation link and values
8. `ContractionFixer.Fix()` — "does not" → "doesn't", etc. (backtick-aware)
9. `ExampleValueBackticker.Fix()` — wrap bare values in `(for example, VALUE)` with backticks

These are reliable, testable fixes that compensate for AI inconsistency.

### Multi-Namespace Merge (AD-011)

Some Azure services span multiple MCP namespaces but publish as a single article (e.g., `monitor` + `workbooks` → `monitor.md`). Rather than threading multi-namespace awareness through all 6 pipeline steps, a **post-assembly merge** runs after all namespaces complete:

Merge member articles are resolved by each mapping's `fileName` value, not by the raw MCP namespace. For example, the `monitor` namespace writes `azure-monitor.md` and the `workbooks` namespace writes `azure-workbooks.md`; the merge writes the combined article back to the primary mapped filename, `azure-monitor.md`.

1. Each namespace generates independently through Steps 1-6
2. `merge-namespaces.sh` reads merge group config from `brand-to-server-mapping.json`
3. Grouped namespaces are combined using three optional fields:
   - `mergeGroup`: group identifier (e.g., `"azure-monitor"`)
   - `mergeOrder`: position within group (1 = primary)
   - `mergeRole`: `"primary"` (owns frontmatter/overview/related) or `"secondary"` (tool H2 sections only)
4. Namespaces WITHOUT `mergeGroup` are standalone — fully backward compatible
5. `MergeGroupValidator` enforces: exactly one primary per group, unique order values (no duplicates), complete field sets

**C# implementation**: `NamespaceMerger.cs` provides typed merge logic with `ParseArticle()` / `Merge()` / `UpdateToolCount()` methods, mirrored by the Node.js-based `merge-namespaces.sh` for shell-level execution.

### Fingerprint Baseline Gate (`--run-fingerprint-gate`)

After all namespace-scoped steps complete, `PipelineRunner.RunAsync()` can run an optional post-pipeline fingerprint comparison gate.

**Gate logic:**

1. Runs `DocGeneration.Tools.Fingerprint snapshot` to capture a candidate snapshot of all `generated-*` directories.
2. Runs `DocGeneration.Tools.Fingerprint diff` comparing the candidate against `fingerprint-baseline.json` at repo root.
3. If `diff` exits with code 1 (quality regressions detected) → pipeline exits with `FatalExitCode`.
4. If no `fingerprint-baseline.json` exists → gate is **skipped** (safe first-run behaviour).
5. Candidate file (`fingerprint-candidate.json`) is cleaned up in a `finally` block regardless of outcome.

**CLI flags:**

| Flag | Effect |
|------|--------|
| `--run-fingerprint-gate` | Enable fingerprint baseline comparison after all namespaces are processed. |

**Key components:**

- `IFingerprintGate` / `FingerprintGate` — service interface and concrete implementation; invokes fingerprint tool as subprocess via `IProcessRunner`.
- `FingerprintGateResult` — result record with `Pass` / `Fail` factory methods and a `Reason` string.

---

### Prompt Regression Gate (`--run-prompt-regression-gate`)

After all namespace-scoped steps complete, `PipelineRunner.RunAsync()` can run an optional post-pipeline prompt regression gate.

**Gate logic:**

1. Runs `dotnet test DocGeneration.PromptRegression.Tests --no-build --configuration Release --verbosity quiet` via `IProcessRunner`.
2. If the test runner exits non-zero → pipeline exits with `FatalExitCode`.
3. Stdout is scanned for the xUnit summary line (e.g., `Passed! – Failed: 0, Passed: 54`) and included in the gate result reason.

**CLI flags:**

| Flag | Effect |
|------|--------|
| `--run-prompt-regression-gate` | Run the full prompt regression test suite after all namespaces are processed. |

**Key components:**

- `IPromptRegressionGate` / `PromptRegressionGate` — service interface and concrete implementation; invokes `dotnet test` as subprocess via `IProcessRunner`.
- `PromptRegressionGateResult` — result record with `Pass` / `Fail` factory methods and a `Reason` string.

---

### Pipeline Output Regression Workflow

GitHub Actions enforces the regression gates through `.github/workflows/pipeline-output-regression.yml` on pull requests targeting `main`.

**Workflow jobs:**

1. `classify-change` — maps changed files to deterministic and AI-involved gate requirements, expands merge-group peers from `brand-to-server-mapping.json`, and publishes job outputs for downstream jobs.
2. `deterministic-regression` — restores/builds/tests the solution, runs representative dry-runs for `applens`, `cloudarchitect`, `deploy`, `compute`, and `fileshares`, then runs the fingerprint gate and uploads a fingerprint diff artifact bundle.
3. `ai-regression` — runs only when the classifier marks the PR as AI-involved; fork PRs fail with a trusted-run-required message, while trusted PRs run fingerprint + prompt regression gates and upload prompt regression artifacts.

This workflow complements `build-and-test.yml`: the standard CI workflow proves the code builds and tests, while the regression workflow proves pipeline output changes are understood before merge.

---

### Source Version Verification Gate

Before processing namespace-scoped steps, `PipelineRunner.RunAsync()` runs `SourceVersionVerificationGate` unless `--skip-validation` is set. When `mcp-tool-version.txt` pins a target version, the gate resolves the versioned source snapshot under `mcp-cli-metadata/<version+hash>/` and compares that source folder version with `cli-version.json`, the `version` fields in generated and source CLI JSON, and the configured target. A missing or mismatched source snapshot fails the run before AI generation can use metadata from the wrong Azure MCP version.

---

### CHANGELOG Gate (AD-571)

Before processing namespace-scoped steps, `PipelineRunner.RunAsync()` applies an optional pre-processing gate that evaluates whether the namespace has changes in the upstream `servers/Azure.Mcp.Server/CHANGELOG.md`.

**Gate logic (evaluated per namespace):**

1. **New namespaces** (no existing article in `tool-family/`) — always processed regardless of CHANGELOG.
2. **Fetch CHANGELOG** from `https://raw.githubusercontent.com/microsoft/mcp/{branch}/servers/Azure.Mcp.Server/CHANGELOG.md`.
3. **Find relevant sections** — version sections where the version is >= `cliVersion` (includes `[Unreleased]`).
4. If **no relevant sections found** → process (conservative fallback).
5. If the **namespace name appears** (case-insensitive) in any relevant section's content → process.
6. Otherwise → **skip** with an informational message (avoids generating an empty-diff PR).
7. **Fetch failures** (network, timeout) → process (conservative fallback).

**CLI flags:**

| Flag | Effect |
|------|--------|
| `--skip-changelog-gate` | Bypass the gate entirely; process all namespaces. |

**Key components:**

- `IChangelogGate` / `ChangelogGate` — service interface and production implementation with injected `HttpClient`.
- `ChangelogParser` — internal static class that parses `## [Version]` sections and implements `HasMentionOf()` / `IsVersionRelevantFor()`.
- `ChangelogGateResult` — record carrying `ShouldSkip` + `Reason` for logging.

### Branch-Aware Upstream Fetching (`--mcp-branch`)

`BootstrapStep` and `ChangelogGate` both fetch files from `microsoft/mcp` using a configurable branch. Resolution order:

1. `--mcp-branch` CLI flag
2. `MCP_BRANCH` environment variable
3. Default: `main`

### CLI Metadata Retry Logic

BootstrapStep retries CLI metadata extraction with exponential backoff to handle cold-start timeouts (e.g., first invocation after `dotnet tool install`):

- **Attempts**: Initial + 3 retries (4 total)
- **Backoff**: 2s → 4s → 8s between retries
- **Scope**: All CLI metadata extractions (not just cold-start)
- **On exhaustion**: Pipeline fails with diagnostic message listing all attempt errors
- **Testability**: Delay function is injected (overridable in tests)

### Namespace Drift Detection

After loading CLI metadata, BootstrapStep checks whether every `mcpServerName` in `brand-to-server-mapping.json` exists in the live CLI namespace list. Mismatches produce a **warning** (non-fatal) logged to the console:

```
WARNING: Namespace 'get' exists in brand-to-server-mapping.json but was not found in CLI output.
```

This alerts operators to namespace lifecycle changes but does **not** stop the pipeline — Steps 1–3 do not require brand-mapping data and can proceed for any namespace present in the CLI output.

Configuration files that track planned changes:
- `config/namespace-mapping.json` — namespace lifecycle tracking
- `merge-namespaces.sh` — namespace merge/join configuration

### Brand-to-Server Mapping: When It Matters

`brand-to-server-mapping.json` is consumed only at **Step 4** (tool-family assembly) and **Step 5** (merge-namespaces). Its purpose is to:

1. Resolve the user-facing brand name and output filename for the tool-family article
2. Determine whether multiple namespaces merge into a single tool-family file (`mergeGroup`)

**If a namespace is NOT in brand-mapping**, Steps 4 and 5 still complete successfully — they use the raw namespace name as the fallback filename and skip multi-namespace merge logic. The only features that require a brand-mapping entry are:

- Custom brand display names (e.g., "Azure Container Registry" instead of "acr")
- Multi-namespace merge groups (combining related namespaces into one article)

Namespaces missing from brand-mapping produce identical tool-family output to those with an entry whose `fileName` matches the namespace — i.e., no alterations are needed for single-namespace families.

### Output Archival (generated-old)

When running a **clean run** (full pipeline), previous output is moved to `generated-old/{timestamp}/` instead of being deleted. This:
- Preserves prior generation results for debugging cross-version issues
- Prevents stale content from contaminating fresh runs
- Provides clear logging: "Clean run: archiving previous output" vs "Incremental run: preserving existing output"

### beta.34 Critical-Failure Baseline Freeze (AD-028)

Issue #813 remediation is anchored to a **frozen evidence baseline**: 34 sanitized,
hash-pinned critical-failure records from Azure MCP build `3.0.0-beta.34+eec7accc`, stored as
immutable fixtures in `DocGeneration.Baseline.Beta34.Tests` with a provenance manifest. It adds
**no pipeline behavior** — it exists so later fixes can be measured against a stable reference
that cannot silently drift. The freeze script (`scripts/baseline/New-Beta34Baseline.ps1`)
regenerates it deterministically from a read-only source run and its `-VerifyOnly` mode proves
byte-for-byte reproducibility; 32 guard tests (31 run + 1 opt-in deep-verify skip) run in CI via
`dotnet test mcp-doc-generation.sln`. A committed `Fixtures/source-inventory.json` (68 physical
copies) and a `.gitattributes` EOL lock let the suite pass on a clean checkout.
See [`beta34-baseline-freeze.md`](beta34-baseline-freeze.md).

### Runtime Dependency Suppression (AD-029)

A fatal step no longer aborts the whole catalog. Before Step 2 of issue #813, the first
namespace-scoped step to exit nonzero returned immediately and every remaining namespace was
skipped. `PipelineRunner` now lets a namespace **survive** a fatal step by suppressing only the
work that actually depended on it, then continues to the next namespace and finally exits nonzero.

**What makes a step a fatal root — `IsFatalRoot` (read this before touching the predicate).** A
selected step becomes a fatal root when its policy is `FailurePolicy.Fatal` **and** it did not
*cleanly* succeed. "Did not cleanly succeed" is signalled by **either** condition — a nonzero exit
is **not** required:

| Signal | Condition | Typical source |
|--------|-----------|----------------|
| **C1** | mapped exit code ≠ `SuccessExitCode` | a hard `Success=false` Fatal step, a forced `ExitCodeOverride`, or a fatal envelope-write failure |
| **C2** | at least one **blocking** entry in `ArtifactFailures` (`ArtifactFailure.IsBlocking`) **even when the step reports `Success=true` and maps to exit 0** | the real Step-2 shape: `ExamplePromptsStep` appends per-tool failures to `ArtifactFailures` after retries but still returns `success: true` |

```csharp
internal static bool IsFatalRoot(FailurePolicy policy, int mappedExitCode, IReadOnlyList<ArtifactFailure> artifactFailures)
    => policy == FailurePolicy.Fatal
        && (mappedExitCode != SuccessExitCode || artifactFailures.Any(f => f.IsBlocking));
```

C2 exists because a step's *result* state can say `success` while its *validation* state says
`failed`. When `ValidateWithRetriesAsync` exhausts its retries, `ExamplePromptsStep` records the
failures in `ArtifactFailures` (and a failed `ValidatorResult`) but falls through to
`return BuildResult(..., success: true, ...)` (`ExamplePromptsStep.cs:141`). An exit-code-only
trigger never fired on that shape: in the frozen beta.34 baseline **16 of 17** Step-2 failures are
exactly this `Success=true` shape, and those 16 are the upstream links behind **all 10** historical
cascades — so the exit-code-only trigger would have suppressed **0 of 10**. Keying on
`ArtifactFailures` catches **17 of 17** and eliminates all 10 cascades.

> ⚠️ **Maintainer trap — C2 keys on `ArtifactFailures`, deliberately NOT on a failed
> `ValidatorResult` (nor on persisted critical-failure counts).** The pre-AI validation gate
> (`TryRunPreAiGateAsync`) intentionally returns `Success=true` **+ a failed `pre-ai-validation`
> `ValidatorResult` + an _empty_ `ArtifactFailures`**, and `ExecuteStepAsync` maps that skip to
> `SuccessExitCode` so the skipped step stays **non-fatal** and its independent dependents keep
> running (`PreAiValidationGateTests` pins this). The real Step-2 failure and the pre-AI skip **both**
> carry `Success=true` and a failed validator; the **only** thing that distinguishes "fatal root" from
> "intentional non-fatal skip" is whether `ArtifactFailures` contains a **blocking** entry
> (validation-after-retries = non-empty and, as of the amendment below, blocking only when it is NOT
> a genuine exhausted-retries content/parameter-validation failure; pre-AI skip = empty). A predicate
> that fired on any failed validator — or on critical-JSON counts — would reclassify the pre-AI skip
> as a fatal root and break the pre-AI-gate contract. **Do not "simplify" `IsFatalRoot` to look at
> validator results.** The `FailurePolicy != Fatal` guard short-circuits first, so a Warn step with
> artifact failures is never a root.

**Amendment — `ArtifactFailure.IsBlocking` narrows C2 for required-parameter validation warnings
(post-#813 follow-up).** A user-reported log showed Step 2 exiting with "Required parameters missing
from example prompts: soft-delete" after automatic retries — a genuine content/parameter-validation
result, not a process or infrastructure failure — and that single warning was suppressing Steps 3-6
for the entire namespace. `ArtifactFailure` now carries `IsBlocking` (default `true`, so every
pre-existing call site across every step is unaffected). `IsFatalRoot`'s C2 clause was narrowed from
"any `ArtifactFailures`" to "any **blocking** `ArtifactFailures`". `ExamplePromptsStep` sets
`IsBlocking=false` **only** when the *last* validator run for an unresolved command genuinely parsed
an explicit `Invalid tools:` report (the validator process ran fine and told us exactly which tool(s)
are missing a required parameter) — never when the validator's own output/process could not be
parsed (the pre-existing "fallback: assume all matching tools invalid" branch, which remains a hard,
`IsBlocking=true` failure), and never when a retry's generator process failed or a regenerated output
was missing (also hard, process/artifact failures forced to `IsBlocking=true` regardless of the
command's prior validation status). The failure is still recorded (visible in `ArtifactFailures`) and
still surfaced through the existing retry/warning messages — only its ability to suppress Steps 3-6
is removed. C1 is untouched by this amendment: a nonzero mapped exit code still always roots,
regardless of any artifact failure's blocking status.

**Forcing a nonzero exit for a C2-only root.** A C2 root maps to exit 0, so `Worse(worstRootExit, 0)`
would leave the catalog exiting 0 despite a recorded root. Before recording the root the runner
recomputes `rootExit = MapStepFailureExitCode(Fatal, stepSucceeded: false, override)` (preserving a
human-review override of `2`), so the invocation still exits nonzero. This recompute is load-bearing:
without it, a namespace that failed only via artifact failures would silently exit 0. `MapStepFailureExitCode`
itself is unchanged — the recompute lives entirely in the namespace loop. This recompute still fires
for a C2-only root under the amendment above (a root requires at least one *blocking* artifact
failure, and that check happens before the recompute); a namespace whose only recorded failures are
all non-blocking (the required-parameter-validation-warning shape) is, by definition, no longer a
root at all, so no recompute is needed or performed for it.

**What happens once a step is a fatal root:**

1. The runner records exactly **one root failure** for that step, with a run-independent id
   `{namespaceSlug}.{stepId:D2}.root` (for example `storage.02.root`). A step runs once per
   namespace, so a namespace with several failing tools still yields **one** Step-2 root that owns
   all of that namespace's artifact-level critical records.
2. It computes the step's **selected transitive dependents** (`SelectedTransitiveDependents`): a
   breadth-first walk over the reverse-adjacency (dependents) map built once from the real
   `StepRegistry` (`BuildDependentsOf`). The walk traverses the **full** reverse graph with its own
   visited set — every reachable dependent is enqueued **regardless of whether it is selected** — and
   the selection filter is applied **only when collecting** the result. Suppression therefore
   propagates *through* an unselected intermediate to a selected dependent beyond it (see the
   `--skip-deps` worked example below). An earlier build filtered at enqueue time and stopped at the
   first unselected step, under-suppressing; the current walk is a pure `∩ selectedIds` at
   collection time, so an unselected step never blocks propagation to selected steps past it.
3. Each collected dependent is **suppressed**: it does not execute, produces no outputs, gets no
   retries, and emits **no critical-failure JSON**. The runner writes a step-result envelope marked
   `suppressed: true` with a `blockedByDependency` block naming the root, to **both** the canonical
   step workspace and the observability directory (see *Where the suppressed envelope is written*
   below).
4. **Independent** selected steps in the same namespace still run, and **later namespaces** still
   run. Per-namespace runtime state is a fresh instance each iteration, so nothing leaks across
   namespaces.

**Warn vs. Fatal.** Only `FailurePolicy.Fatal` steps can become roots. A `FailurePolicy.Warn`
step that fails maps to exit 0, records a warning-only outcome for accounting, and **never**
suppresses dependents (its downstream steps stay eligible to run).

**Global vs. namespace scope.** Only the per-namespace step loop changed. Global-scope failures —
Step 0 Bootstrap, the source-version gate, and namespace/argument planning errors — still abort
the catalog immediately.

**Exit code.** After every namespace has been attempted, the catalog exits nonzero if any fatal
root occurred. A hard fatal (`1`) dominates human-review (`2`); an explicit validation-gate
failure still wins over both because the gates run and return first. A root detected only via **C2**
(artifact failures, mapped exit 0) is forced to a nonzero effective exit by the `rootExit` recompute
above, so a namespace that failed only through artifact failures never leaves the catalog at exit 0.

**Worked examples** (matching the dependency chain in [Step Details](#dependencies)):

- Select `[1,2,3,4,6]`; Step 4 becomes a fatal root. Its dependents (Steps 7/8) are not selected, so
  the collected suppression closure is empty. Step 6 (depends on Step 0, not Step 4) still runs. The
  namespace exits nonzero.
- Select `[1,2,3,4,7]`; Step 2 becomes a fatal root — including the common **C2** case where Step 2
  returns `success: true` but recorded a **blocking** `ArtifactFailure` (for example, the validator
  process itself could not be parsed after retries, so we fell back to assuming every matching tool
  is invalid). The closure is `{3, 4, 7}` — all suppressed — and the catalog is forced to exit nonzero
  even though Step 2's own mapped exit was `0`. Later namespaces still run.
- Select `[1,2,3,4,7]`; Step 2 returns `success: true` and recorded ONLY **non-blocking**
  `ArtifactFailures` (a required-parameter/example-prompt content-validation failure that genuinely
  parsed the validator's `Invalid tools:` report and survived automatic retries — for example,
  "Required parameters missing from example prompts: soft-delete"). Step 2 is **not** a fatal root
  (post-#813 follow-up amendment above): the failure stays visible as a warning and an
  `ArtifactFailure` record, but Steps 3, 4, and 7 all run normally.
- Run `start.sh <ns> 2,4 --skip-deps` (select `{2,4}`; Steps 1/3 unselected) and Step 2 becomes a
  fatal root. The full reverse-graph walk reaches Step 4 **through** the unselected Step 3
  (`2 → 3 → 4`), then intersects with the selection, so **Step 4 is suppressed**. This upholds the
  `--skip-deps` invariant — a selected failed dependency is never silently turned into a success —
  which an enqueue-time-filtered walk would violate by letting Step 4 run.

**Step-result envelope extension.** `StepResultFile` (in `DocGeneration.Core.Shared`) gains two
**additive, optional** fields, plus a top-level `BlockedByDependency` type:

| Field | Type | Meaning |
|-------|------|---------|
| `suppressed` | `bool?` | `true` when the step was blocked by a fatal dependency; null/absent for steps that executed normally. |
| `blockedByDependency` | object | Present only when `suppressed` is true. Fields: `namespace`, `failedRootStepId`, `failedRootStepName`, `rootFailureId`. |

`schemaVersion` deliberately stays `"1.0"` — the reader only rejects an *unrecognized non-empty*
`schemaVersion`, and both new fields are nullable, so every pre-existing envelope and every current
reader deserializes byte-unchanged. The informational integer `version` moves `3` → `4` to document
the new content shape. A suppressed step writes `status: "failure"` with
`validationStatus: "skipped"`; the **authoritative** signal is `suppressed == true`, and the
non-success status is a conservative fallback for tooling that ignores the new field.

**Where the suppressed envelope is written (canonical + observability).** A suppressed step writes
its envelope to **two** locations, each overwritten in place via `StepResultWriter.Write`:

| Location | Path | Read by |
|----------|------|---------|
| **Canonical (authoritative)** | `{output-dir}/step-<id>-<slug>/step-result.json` | `StepResultReader`, `UpstreamArtifactResolver` (downstream Steps 3/4/6), replay/inspect |
| **Observability (dashboard copy)** | `{output-dir}/observability/<id>-<slug>/step-result.json` | dashboards / observability tooling |

Writing the **canonical** copy is what makes suppression correct on a **same-workspace rerun**: it
overwrites any stale *success* envelope a previous run left for that step, so replay and any partial
downstream selection read `suppressed: true` instead of a stale success. The envelope carries
`outputFileCount: 0` and no output artifacts, so `UpstreamArtifactResolver` will not resolve a
suppressed step's stale prior `.md` outputs. Because a suppressed step bypasses `ExecuteStepAsync`,
`WriteSuppressedEnvelope` is the sole writer for that step in that run (no double-write, no clobber).
An earlier build wrote **only** the observability copy — leaving the authoritative canonical envelope
stale — which is the defect this dual write corrects.

**Run accounting (`run-accounting.json`).** Each completed run writes `run-accounting.json` at the
output-directory root and prints a matching six-category console summary. The same partition backs
both surfaces, so they cannot diverge:

| Category | Source | Contents |
|----------|--------|----------|
| 1. Successful namespaces | live | Namespaces whose selected steps all succeeded or warn-failed with zero fatal roots. |
| 2. Root-failed namespaces | live | Each `(namespace, rootStepId, rootStepName, rootFailureId, exitCode)`. |
| 3. Warning-only failures | live | Each selected `Warn` step that did not succeed, as `(namespace, stepId, stepName)`. |
| 4. Suppressed steps | live | Each suppressed dependent, as `(namespace, stepId, rootFailureId)`. |
| 5. Cascades imported from historical fixtures | baseline | `chainRoleCounts.cascade` from the frozen beta.34 manifest. |
| 6. Unclassified records | baseline | `classificationCounts.diagnostic` from the frozen beta.34 manifest. |

Categories 1–4 are computed live from the per-namespace reports. Categories 5–6 live under the
`reconciliation` object and are a **pure function of the frozen AD-028 baseline manifest** — they
reflect the historical baseline, not the current run, and are read once (never summed). The
`reconciliation` object is `null` when the baseline manifest cannot be located (graceful
degradation), and the top-level `successfulNamespaces` / `rootFailedNamespaces` /
`warningOnlyFailures` / `suppressedSteps` arrays always reflect the live run. The catalog wrapper
`start-with-logs.ps1` aggregates every namespace's `run-accounting.json` into one catalog summary
(live categories summed; baseline categories taken once); missing or malformed files are
skipped/warned and can never mask a failure. Because the root predicate now fires on the real Step-2
`Success=true` + `ArtifactFailures` shape (C2 above), category 4 (live suppressed steps) is non-zero
on the real corpus and corresponds to the historical cascade count in category 5 — the two are kept
as distinct *live* vs *baseline* columns so the historical figure is never misread as live evidence.

### Live AI Endpoint Probe & `partial_explicit` Offline Continuation (AD-042)

Before AD-042, the pipeline discovered a misconfigured or unreachable Azure OpenAI endpoint only
when the first AI-dependent step actually called it — potentially deep into a long, expensive
full-namespace run (Step 2, 3, 4, or 6). `BootstrapStep` now makes one **very early live call** to
the configured endpoint, immediately after configuration presence is confirmed, to prove the
endpoint actually works before any generation step runs.

Every call through the shared `GenerativeAIClient` writes a concise transport-status line to the
console and generation log. A successful chat completion reports `status=200`; HTTP failures
report the SDK status such as `400`, `401`, or `429` before the original exception is rethrown.
The line also identifies the operation, target tool or namespace, outcome, and duration. Failures
that never receive an HTTP response, such as DNS or socket errors, report `status=unavailable`.
When Step 2 intentionally uses verbatim source prompts or deterministic generation instead of the
LLM, it writes `status=skipped outcome=not-called` with the tool command, selected mode, and reason.
Its raw-response artifact remains `{}` by design.

**Non-interactive / redirected-input runs fail immediately.** If the probe fails and stdin is
redirected or otherwise non-interactive (CI, scripted runs), the pipeline fails fast with a nonzero
exit — there is no prompt and no silent continuation.

**Interactive runs prompt the operator.** If the probe fails and the session is interactive,
`IPipelineUserPrompt` asks whether to continue:
- **Decline** → the pipeline fails with the same nonzero exit as the non-interactive case; no
  critical-failure record is created (the operator aborted before any offline work happened).
- **Continue (`partial_explicit`)** → the pipeline:
  1. Persists one loud, explicit critical-failure JSON record via the existing
     `CriticalFailureRecorder.Persist` facility — no new JSON shape is invented — stating that
     Azure OpenAI endpoint access failed and a partial deterministic fallback was explicitly
     selected.
  2. Sets `PipelineContext.AiOffline = true` and exports `PIPELINE_AI_ENDPOINT_OFFLINE=true` so
     child subprocesses (which inherit the parent's environment) also see the offline state.
  3. Disables **all further AI endpoint calls**, universally and for the rest of the run, via a
     call-time guard inside `GenerativeAIClient.GetChatCompletionAsync` (throws
     `AiEndpointOfflineException` — a subclass of `InvalidOperationException` whose message never
     contains "truncated", so it always falls through each step's specific retry/truncation
     handling into that step's existing generic-failure fallback).
  4. Continues with deterministic/verbatim work only. Every AI-required artifact or step is
     explicitly marked **incomplete** — never presented as fully successful, per requirement 3.

**Why call-time gating, not construction-time.** The guard lives inside
`GetChatCompletionAsync` rather than in `GenerativeAIClient`'s constructor or
`CreateChatClient` factory, because a direct `(IChatClient, tracer, modelName)` constructor exists
and bypasses the options-based factory entirely (see `GenerativeAIClientTracingTests`). Call-time
gating is the only placement that catches every construction path uniformly.

#### Observed AI behavior (pre-AD-042 baseline run)

| Step | Uses Azure OpenAI? | Observed behavior on AI failure |
|------|--------------------|----------------------------------|
| 1 — Bootstrap/setup | No | Fully deterministic; no OpenAI call at all. |
| 2 — Example prompts | Verbatim tier: no. AI tier: yes. | Verbatim/deterministic tier uses source prompts unchanged; raw output `{}` is produced **by design** for that tier. The AI-required tier has no endpoint-failure-specific handling before AD-042. |
| 3 — Tool improvements | Yes | Caught the AI failure and saved the original **composed** file **byte-identically** as a fallback. |
| 4 — Tool-family cleanup | Yes | Caught the AI failure and inserted the literal placeholder `<TBD_Content>` in place of the AI-generated service description. |
| 5 — Skills relevance | No | Does not use Azure OpenAI at all; relevance is computed from skills data only. |
| 6 — Horizontal articles | Yes | Required complete AI output with no failure handling for this case, and **failed fatally** when AI was unavailable. |

#### Designed AI behavior (AD-042)

| Step | Behavior when `AiOffline` is true (post-AD-042) |
|------|--------------------------------------------------|
| 1 — Bootstrap/setup | Unchanged — runs the live probe itself; not gated by its own result. |
| 2 — Example prompts | Verbatim/deterministic tier unaffected (still produces `{}` by design). The AI-required tier's call now throws `AiEndpointOfflineException` before any network attempt; that tool's example prompts are marked incomplete rather than silently absent. |
| 3 — Tool improvements | `GenerativeAIClient` throws before any network attempt; `ImprovedToolGeneratorService` falls through to its existing composed-content fallback and saves the original content byte-identically — the same observed fallback, now proven to trigger with zero network calls and still reported as an incomplete/fallback result, never as success. |
| 4 — Tool-family cleanup | Same call-time throw; `FamilyMetadataGenerator` falls through to `BuildFallbackServiceDescription()`, inserting `<TBD_Content>` — the same observed placeholder, now proven to trigger with zero network calls and never reported as a fully successful description. |
| 5 — Skills relevance | Unaffected — still does not use Azure OpenAI. |
| 6 — Horizontal articles | `HorizontalArticlesStep` short-circuits **before** constructing the reducer/generator at all: it returns an explicit incomplete failure with zero AI endpoint calls (no subprocess retry, no repeated failing call). When online, the reducer path now uses the current per-tool + namespace-fragment AI generation path (`GeneratePerToolAiDataAsync`, shared with the standalone generator) instead of the obsolete monolithic `GenerateAIContent`. The former single broad namespace-summary call is replaced by four small, focused fragment calls (overview/access/best-practices/links), deterministically stitched via `StitchNamespaceSummary` — see the Step 6 bullet above. |

See [AD-042 in `.squad/decisions.md`](../.squad/decisions.md) for the full design record.

## Parameter Taxonomy (3-Tier Model)

Parameters in Azure MCP tools fall into three categories:

| Tier | Parameters | Behavior in Generated Output |
|------|-----------|------------------------------|
| **Global** | `--tenant`, `--auth-method`, `--retry-delay`, `--retry-max-delay`, `--retry-max-retries`, `--retry-mode`, `--retry-network-timeout` | Included in all tool parameter tables (Dina strips during content PR) |
| **Resource-group** | `--resource-group` | Included in all tool parameter tables (Dina strips during content PR when not required) |
| **Tool-specific** | All other parameters | Always included; must never be lost or dropped |

**Historical note**: Prior to beta.31 fixes, common/global parameters were filtered from generated output automatically. This was changed to include all parameters for consistency between CLI and NLP tabs, with manual stripping during content PR creation.

The `common-parameters.json` file is retained for documentation purposes but is no longer used for filtering.

### Parallel Execution

After Step 0 (bootstrap) runs once, namespace-scoped steps can execute in parallel:

```bash
# After preflight completes:
./start.sh advisor &
./start.sh compute &
./start.sh storage &
wait
```

Each namespace writes to its own `generated-{namespace}/` directory with no shared mutable state.

## Output Directory Convention

| Mode | Output Path |
|------|-------------|
| All namespaces | `./generated/` |
| Single namespace (start.sh wrapper) | `./generated-{namespace}/` |
| Single namespace (PipelineRunner default) | `./generated-{namespace}-{yyyyMMddTHHmmssfffZ}/` |
| Validated output | `./generated-validated-{namespace}/` |

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Fatal error (step failure) |
| 2 | Human review required (brand mapping suggestions) |
| 64 | Invalid CLI arguments |

Since #813 Step 2, a namespace-scoped fatal root no longer stops the run: the runner suppresses that
step's selected dependents, continues with independent steps and later namespaces, and surfaces the
**worst** namespace exit code at the end (a hard fatal `1` dominates human-review `2`). A step
becomes a fatal root when it is `Fatal` **and** did not cleanly succeed — either a nonzero exit **or**
a recorded **blocking** artifact failure even when its own mapped exit was `0` (see
[Runtime Dependency Suppression](#runtime-dependency-suppression-ad-029) for the `IsFatalRoot`
predicate and its AD-044 amendment); such an artifact-failure-only root is still forced to a nonzero
catalog exit. An exhausted-retries Step 2 required-parameter/content-validation failure is recorded
as a **non-blocking** artifact failure (AD-044): it stays visible as a warning but does not, by
itself, make Step 2 a fatal root.
Global-scope failures still abort immediately.

A failed live Azure OpenAI probe (AD-042) is one such global-scope failure: a non-interactive or
redirected-input run fails immediately with a nonzero exit, and an interactive run that declines to
continue exits the same way. Only an interactive **explicit** Continue keeps the run going, and only
in the reduced `AiOffline` mode described above.

## AI Configuration

Steps 2, 3, 4, and 6 require Azure OpenAI. Configure in `mcp-tools/.env`:

| Variable | Purpose |
|----------|---------|
| `FOUNDRY_API_KEY` | Azure OpenAI API key |
| `FOUNDRY_ENDPOINT` | Azure OpenAI endpoint URL |
| `FOUNDRY_MODEL_NAME` | Model deployment (e.g., `gpt-4.1-mini`) |
| `TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME` | Step 4 model (e.g., `gpt-4o`) — higher quality for article assembly |

Step 0 validates these variables before any AI steps run (unless `--skip-env-validation`).

**Live endpoint probe (AD-042).** Configuration presence alone does not prove the endpoint is
reachable. Immediately after Step 0/Bootstrap confirms configuration is present, the pipeline makes
one live Azure OpenAI call to prove it actually works, **before** Steps 2–6 run. See
[Live AI Endpoint Probe & `partial_explicit` Offline Continuation](#live-ai-endpoint-probe--partial_explicit-offline-continuation-ad-042)
for full behavior, the observed-vs-designed table, and the `PIPELINE_AI_ENDPOINT_OFFLINE`
environment variable that a confirmed interactive "Continue" sets for the rest of the run
(including inherited child subprocesses).

## Project Layout

```
microsoft-mcp-doc-generation/
├── start.sh                          # Entry point (bash wrapper)
├── merge-namespaces.sh               # Post-assembly merge (AD-011)
├── mcp-doc-generation.sln               # .NET solution
├── mcp-tools/
│   ├── DocGeneration.PipelineRunner/  # Typed orchestrator
│   │   ├── Program.cs                # CLI entry (System.CommandLine)
│   │   ├── PipelineRunner.cs         # Core runner loop
│   │   ├── Registry/StepRegistry.cs  # Step 0-6 registration
│   │   ├── Contracts/                # IPipelineStep, StepDefinition
│   │   ├── Steps/                    # Typed step implementations
│   │   │   ├── Bootstrap/            # Step 0
│   │   │   └── Namespace/            # Steps 1-6
│   │   └── Validation/               # Post-validators
│   ├── DocGeneration.Steps.*/        # Generator projects (one per step)
│   ├── DocGeneration.Core.*/         # Shared libraries
│   ├── data/                         # Configuration JSON files
│   ├── templates/                    # Handlebars templates
│   └── scripts/                      # Legacy PowerShell (fallback only)
├── shared/
│   ├── DocGeneration.Core.Tracing/   # Pipeline observability (trace AI + steps)
│   ├── DocGeneration.Core.GenerativeAI/ # Shared AI client
│   ├── DocGeneration.Core.Shared/    # Shared utilities (incl. JsonControlCharacterSanitizer — the single sanitizer all CLI-JSON parse sites use to strip stray control chars like 0x1A before parsing)
│   └── shared.slnx                   # Shared libraries solution
├── skills-generation/                # Skills documentation pipeline
├── docs/                             # Documentation
├── generated-validated-*/            # Validated pipeline output
└── mcp-cli-metadata/               # CLI metadata version snapshots
```

## Pipeline Observability

Both pipelines emit structured trace files after every run to `{output-dir}/trace/`:

| File | Content |
|------|---------|
| `pipeline-trace.json` | Full execution graph with step timing, classification, and status |
| `ai-interactions.json` | Every LLM call with system prompt, user prompt, response, tokens, model |
| `summary.md` | Human-readable run summary with step table and AI statistics |

Tracing is always-on (no opt-in flag), uses in-memory collection during execution, and flushes once at the end of each run. The `NullTracer` pattern ensures zero overhead when the tracer is not wired (e.g., in unit tests).

PipelineRunner also writes a shared `step-result.json` envelope for every selected step under `{output-dir}/step-<id>-<slug>/`. Dry runs emit the same envelope with placeholder values. A missing envelope is a fatal outcome for that step: a Global-scope step aborts the catalog, while a namespace step records a root failure and suppresses its selected dependents before the run continues (see [Runtime Dependency Suppression](#runtime-dependency-suppression-ad-029)); warn-only steps continue.

In addition, every executed step now gets an observability directory at `{output-dir}/observability/{stepId}-{slug}/`. The runner writes `summary.md`, `validation.json`, and `metrics.json`, writes `prompt-preview-na.txt` for deterministic steps, and checks for the full 5-file contract (`prompt-preview.txt` for AI/hybrid steps). Missing contract files are surfaced as warnings so partial instrumentation is visible during rollout.

### Trace Architecture

```
shared/DocGeneration.Core.Tracing/
├── IPipelineTracer.cs         # Interface + IStepHandle + StepClassification enum
├── PipelineTracer.cs          # ConcurrentBag-based in-memory collector
├── NullTracer.cs              # No-op for tests and disabled paths
├── AiInteractionRecord.cs     # Input record for RecordAiCall()
├── Models/                    # Serialization models
│   ├── TraceEvent.cs
│   ├── AiInteraction.cs
│   └── PipelineTrace.cs
└── TraceWriter.cs             # Atomic JSON + markdown emission
```

- **Skills pipeline:** Fresh tracer created per `ProcessBatchAsync()` run, flushed in `finally` block
- **MCP pipeline:** Fresh tracer created per namespace iteration, flushed to `generated-{namespace}/trace/`

---

## Key Concepts

These terms are introduced by the pipeline manageability work (Points 1–17) and used throughout new code and comments.

| Term | Definition |
|------|------------|
| **LLM** | Large Language Model — the Azure OpenAI model invoked by AI stages (Steps 2, 3, 4, and 6). Configured via `FOUNDRY_MODEL_NAME` and related environment variables. |
| **step envelope** | The `StepResultFile` JSON artifact written by every step to its workspace directory after execution. Contains schema version, input/output artifacts, validation status, token usage, and timing. |
| **frozen artifact** | A `step-result.json` from a prior pipeline run stored in a versioned run directory. Used by `--replay` to re-run a single step against fixed upstream outputs without re-running predecessors. |
| **reducer** | A deterministic class that extracts only the inputs one AI stage needs from the upstream envelopes, producing a compact typed context object. No LLM call; runs before the pre-AI gate. See `ToolGenerationReducer`. |
| **builder** | Synonym for reducer in the `ToolFamilyCleanup` and `HorizontalArticles` contexts; additionally generates structural scaffolding (headings, section order, skeleton) so the AI stage handles prose only. See `FamilyStructureBuilder`, `ArticleOutlineBuilder`. |
| **seam validator** | An `IPreAiValidator<TContext>` implementation that gates an AI stage. Runs after the reducer but before the LLM call; can block the call by returning `isValid: false`. See `ToolGenerationBudgetValidator`, `ArticleOutlineBudgetValidator`. |
| **pre-AI gate** | The point in `PipelineRunner` where all registered seam validators for a stage are invoked before any LLM call is dispatched. When a seam validator fails, the stage is skipped and `validationStatus: failed` is written to the step envelope. |
| **workspace directory** | The per-run, per-step scratch directory managed by `WorkspaceManager`. Path: `{outputPath}/step-{stepId}-{stepSlug}/`. Step wrappers read upstream inputs from and write the step envelope to this directory. |
| **step wrapper** | A class in `DocGeneration.PipelineRunner/Steps/Namespace/` that implements `IPipelineStep` and orchestrates one pipeline stage — invoking the reducer, running the pre-AI gate, dispatching the LLM call, and writing the step envelope. |
| **replay mode** | CLI mode (`--replay`) that loads frozen step envelopes from a past run directory and re-executes only the target step against those fixed inputs, without re-running predecessors. Entry point: `RunReplayAsync`. |
| **inspect mode** | CLI mode (`--inspect`) that runs the reducer for a named step against the current workspace inputs and prints a prompt budget summary — without invoking the LLM. A pre-flight check, not a debugging tool. Entry point: `RunInspectAsync`. |

---

## Developer Loop

Three common workflows for working with the pipeline locally.

### 1. Fresh full run

Run all steps for all namespaces:

```bash
./start.sh
# Equivalent:
dotnet run --project mcp-tools/DocGeneration.PipelineRunner -- --output ./generated
```

Run specific steps for a single namespace (e.g., only `advisor`):

```bash
./start.sh advisor 1,2,3
# Equivalent:
dotnet run --project mcp-tools/DocGeneration.PipelineRunner -- \
  --namespace advisor --steps 1,2,3 --output ./generated-advisor
```

Skip dependency validation (useful when re-running a single step that you know has all inputs):

```bash
./start.sh advisor 4 --skip-deps
```

---

### 2. Replay a single step

After a full run, re-run only `tool-generation` using frozen inputs from a previous run:

```bash
./start.sh --replay --step tool-generation --from 20240501T120000Z --namespace advisor
# Equivalent:
dotnet run --project mcp-tools/DocGeneration.PipelineRunner -- \
  --replay --step tool-generation --from 20240501T120000Z \
  --namespace advisor --output ./generated-advisor
```

Replay loads the frozen `step-result.json` envelopes from `--from` run directory and passes
them directly to the step executor, skipping all predecessors. No LLM calls are made for
deterministic steps; AI stages still invoke the LLM but against fixed upstream context.

---

### 3. Inspect prompt budget before running

Use `--inspect` before making prompt changes to verify you have enough headroom. No LLM call is made; the reducer runs deterministically against the current workspace.

**Example 1 — Check horizontal-articles budget before editing the system prompt:**

```bash
# Check how many tokens the advisor namespace will consume at Step 6.
# Use this BEFORE editing horizontal-article-system-prompt.txt to confirm headroom.
./start.sh --inspect --step horizontal-articles --namespace advisor \
  --show prompt-budget --output ./generated-advisor
# Prints: step | namespace | estimatedTokens | budget | headroom | topItems (top-5 sections)
# Exits 0 if within budget (≤ 150,000 tokens); exits 1 if over budget.
```

**Example 2 — Check tool-generation budget and export results to JSON (CI pre-flight):**

```bash
# Export the budget table as JSON so CI can parse the results.
# --output is required to enable JSON file writing; without it only stdout is printed.
./start.sh --inspect --step tool-generation --namespace advisor \
  --show prompt-budget --output ./generated-advisor
# Creates ./generated-advisor/inspect-budget.json:
#   { "model": "gpt-4.1-mini", "rows": [{ "step", "namespace", "estimatedTokens",
#     "budget", "headroom", "topItems" }, ...] }
# Exits 0 if all tools within 100k budget; exits 1 if any tool exceeds budget.
```

**Example 3 — Verify headroom before and after a prompt change (tool-family-cleanup):**

```bash
# Before editing the tool-family cleanup prompt, capture the baseline budget:
./start.sh --inspect --step tool-family-cleanup --namespace compute \
  --show prompt-budget --output ./generated-compute
# Note the headroom value in the output (budget = 150,000 tokens).

# Make your prompt change, then re-run inspect to confirm headroom is still positive:
./start.sh --inspect --step tool-family-cleanup --namespace compute \
  --show prompt-budget --output ./generated-compute
# If headroom < 0, the prompt is too large — trim before running the full pipeline.
```

**Example 4 — Run `--inspect` in CI as a gate before dispatching a full LLM run:**

```bash
# Use in a CI job to block the LLM step if the prompt would exceed budget.
# FOUNDRY_MODEL_NAME is shown in inspect output for traceability.
FOUNDRY_MODEL_NAME=gpt-4.1-mini \
  dotnet run --project mcp-tools/DocGeneration.PipelineRunner -- \
  --inspect --step horizontal-articles --namespace advisor \
  --show prompt-budget --output ./generated-advisor
# Exit code 0 = within budget → proceed to full run
# Exit code 1 = over budget → fail CI, notify author to trim the prompt
```

Exit code 0 = all items within budget; exit code 1 = at least one item exceeds budget.
JSON is written to `{output}/inspect-budget.json` only when `--output` is explicitly provided.

---

## Enforcement Model

All enforcement decisions across the pipeline follow a four-tier model.

| Level | Condition | Response |
|-------|-----------|----------|
| **Fatal** | `step-result.json` absent after a non-warn-only step completes | Runner logs FATAL. A Global-scope step aborts the catalog; a namespace step records a root failure and suppresses its selected dependents, then the run continues and exits nonzero (see [Runtime Dependency Suppression](#runtime-dependency-suppression-ad-029)). |
| **Validation skip** | Pre-AI seam validator returns `isValid: false` | Stage is skipped; `validationStatus: failed` written to step envelope; pipeline continues to next independent step. |
| **Warning** | Observability files (`summary.md`, `metrics.json`, etc.) missing after a step | Logged as WARNING; pipeline continues. |
| **Phase-gated** | `StepRegistry` in-memory registry diverges from `pipeline.config.json` | Phase 1: WARNING; Phase 2 and beyond: throws `StepRegistryConfigMismatchException`. |

`SkillsRelevanceStep` is **warn-only by design**: its `step-result.json` is required, but a `validationStatus: failed` in that file does not abort the pipeline.
