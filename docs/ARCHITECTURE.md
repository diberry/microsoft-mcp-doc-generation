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
  │  Uses: Handlebars templates, static-text-replacement.json
  │
  ▼
Step 2: Example Prompts (AI) ──────────────────────────────────────
  │  • tools-raw/ + cli-output.json → example-prompts/*.md
  │  • Azure OpenAI generates 5 natural language prompts per tool
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
  │  • Warning-only — failures don't stop the pipeline
  │
  ▼
Step 6: Horizontal Articles (AI) ──────────────────────────────────
  │  • tools/ + cli-output.json → horizontal-articles/*.md
  │  • One overview article per namespace: capabilities, scenarios,
  │    prerequisites, RBAC roles, best practices
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
| 0 | `BootstrapStep` | No | Fatal | 0 | `cli/`, `h2-headings/`, `e2e-test-prompts/`, `namespace-mapping.json` |
| 1 | `AnnotationsParametersRawStep` | No | Fatal | 0 | `annotations/`, `parameters/`, `tools-raw/` |
| 2 | `ExamplePromptsStep` | Yes | Fatal | 0 | `example-prompts/` |
| 3 | `ToolGenerationStep` | Yes | Fatal | 0 | `tools-composed/`, `tools/` |
| 4 | `ToolFamilyCleanupStep` | Yes | Fatal | **2** | `tool-family/`, `reports/` |
| 5 | `SkillsRelevanceStep` | No | **Warn** | 0 | `skills-relevance/` |
| 6 | `HorizontalArticlesStep` | Yes | Fatal | 0 | `horizontal-articles/` |
| 7 | `ArticleHealthValidatorStep` | No | **Warn** | 0 | `article-health.json`, `validation-summary.md` |

### Dependencies

```
Step 1 → (no deps, uses CLI metadata from Step 0)
Step 2 → depends on Step 1
Step 3 → depends on Step 2
Step 4 → depends on Step 3
Step 5 → (no deps, reads tools/ directly)
Step 6 → (no deps, reads tools/ + cli-output.json)
Step 7 → depends on Step 4 (validates tool-family/ output)
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

## AI Configuration

Steps 2, 3, 4, and 6 require Azure OpenAI. Configure in `mcp-tools/.env`:

| Variable | Purpose |
|----------|---------|
| `FOUNDRY_API_KEY` | Azure OpenAI API key |
| `FOUNDRY_ENDPOINT` | Azure OpenAI endpoint URL |
| `FOUNDRY_MODEL_NAME` | Model deployment (e.g., `gpt-4.1-mini`) |
| `TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME` | Step 4 model (e.g., `gpt-4o`) — higher quality for article assembly |

Step 0 validates these variables before any AI steps run (unless `--skip-env-validation`).

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

PipelineRunner also writes a shared `step-result.json` envelope for every selected step under `{output-dir}/step-<id>-<slug>/`. Dry runs emit the same envelope with placeholder values, and missing envelopes abort the run for fatal steps while warn-only steps continue.

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
| **Fatal** | `step-result.json` absent after a non-warn-only step completes | Runner logs FATAL and aborts the pipeline. |
| **Validation skip** | Pre-AI seam validator returns `isValid: false` | Stage is skipped; `validationStatus: failed` written to step envelope; pipeline continues to next independent step. |
| **Warning** | Observability files (`summary.md`, `metrics.json`, etc.) missing after a step | Logged as WARNING; pipeline continues. |
| **Phase-gated** | `StepRegistry` in-memory registry diverges from `pipeline.config.json` | Phase 1: WARNING; Phase 2 and beyond: throws `StepRegistryConfigMismatchException`. |

`SkillsRelevanceStep` is **warn-only by design**: its `step-result.json` is required, but a `validationStatus: failed` in that file does not abort the pipeline.
