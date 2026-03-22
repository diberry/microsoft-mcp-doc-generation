# Architecture

The Azure MCP Documentation Generator is a typed .NET pipeline that transforms raw Azure MCP CLI metadata into 800+ publication-ready markdown files across 52 Azure service namespaces.

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
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

## Data Flow

Raw CLI metadata flows through each step, transformed into richer content at each stage:

```
npm (Azure MCP package)
  │
  ▼
Step 0: Bootstrap ─────────────────────────────────────────────────
  │  • npm install + extract CLI metadata → cli-output.json
  │  • Build .NET solution
  │  • Brand mapping validation → reports/
  │  • E2E test prompt parsing → e2e-test-prompts/
  │  • Deterministic H2 headings → h2-headings/
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
  │  • AI generates: frontmatter, intro, related content
  │  • Post-processing: MCP acronym expansion, frontmatter enrichment,
  │    duplicate example stripping
  │  • Post-assembly validator checks: tool count, cross-references,
  │    parameter coverage, branding
  │  • Retries up to 2x on validation failure
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
  │
  ▼
Final Output ──────────────────────────────────────────────────────
  generated-{namespace}/
  ├── tool-family/{namespace}.md         ← Primary deliverable
  ├── horizontal-articles/{namespace}.md ← Overview article
  ├── annotations/*.md                   ← Include files
  ├── parameters/*.md                    ← Include files
  ├── example-prompts/*.md               ← Include files
  └── reports/                           ← Validation reports
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
| 0 | `BootstrapStep` | No | Fatal | 0 | `cli/`, `h2-headings/`, `e2e-test-prompts/` |
| 1 | `AnnotationsParametersRawStep` | No | Fatal | 0 | `annotations/`, `parameters/`, `tools-raw/` |
| 2 | `ExamplePromptsStep` | Yes | Fatal | 0 | `example-prompts/` |
| 3 | `ToolGenerationStep` | Yes | Fatal | 0 | `tools-composed/`, `tools/` |
| 4 | `ToolFamilyCleanupStep` | Yes | Fatal | **2** | `tool-family/`, `reports/` |
| 5 | `SkillsRelevanceStep` | No | **Warn** | 0 | `skills-relevance/` |
| 6 | `HorizontalArticlesStep` | Yes | Fatal | 0 | `horizontal-articles/` |

### Dependencies

```
Step 1 → (no deps, uses CLI metadata from Step 0)
Step 2 → depends on Step 1
Step 3 → depends on Step 2
Step 4 → depends on Step 3
Step 5 → (no deps, reads tools/ directly)
Step 6 → (no deps, reads tools/ + cli-output.json)
```

## Key Design Decisions

### Typed .NET Orchestrator (PipelineRunner)

The pipeline migrated from PowerShell scripts to a typed C# orchestrator. This provides:
- **Compile-time safety** for step registration and dependency declarations
- **Integrated retry logic** for AI-dependent steps
- **Post-validation framework** (`IPostValidator`) attached to specific steps
- **Isolated workspaces** via `WorkspaceManager` for parallel execution

Legacy PowerShell scripts remain in `docs-generation/scripts/` as fallback.

### Isolated Workspaces (Step 4)

Step 4 runs in a temporary directory (`pipeline-runner-step4-{guid}`) to enable parallel namespace execution. Files are copied in, generation runs in isolation, and outputs are copied back. This prevents file conflicts when multiple namespaces run simultaneously.

### Post-Assembly Validation (Step 4)

After Step 4 generates a tool-family article, `ToolFamilyPostAssemblyValidator` checks:
- **Tool count integrity** — frontmatter `tool_count` matches H2 sections and tool files
- **Cross-reference check** — every tool file has a matching article section
- **Required parameter coverage** — example prompts mention all required parameters
- **Branding consistency** — no "CosmosDB", "this command", etc.

If validation fails, Step 4 retries (up to 2 attempts) since AI output is non-deterministic.

### Deterministic Post-Processing

The `FamilyFileStitcher.Stitch()` method chains deterministic fixes after AI assembly:
1. `PostProcessor.ExpandMcpAcronym()` — expand "MCP" on first body mention
2. `FrontmatterEnricher.Enrich()` — inject required Microsoft Learn fields
3. `DuplicateExampleStripper.Strip()` — remove non-canonical example blocks

These are reliable, testable fixes that compensate for AI inconsistency.

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
| Single namespace | `./generated-{namespace}/` |
| Validated output | `./generated-validated-{namespace}/` |

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Fatal error (step failure) |
| 2 | Human review required (brand mapping suggestions) |
| 64 | Invalid CLI arguments |

## AI Configuration

Steps 2, 3, 4, and 6 require Azure OpenAI. Configure in `docs-generation/.env`:

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
├── docs-generation.sln               # .NET solution
├── docs-generation/
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
├── docs/                             # Documentation
├── generated-validated-*/            # Validated pipeline output
└── test-npm-azure-mcp/               # npm project for CLI extraction
```
