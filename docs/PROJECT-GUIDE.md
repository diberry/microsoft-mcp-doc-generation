# Azure MCP Server Documentation Generation — Project Guide

**Audience:** Developers joining or extending the Azure MCP Server documentation generation system.  
**Updated:** 2026-03-15

---

## Table of Contents

1. [How to Run This Project](#how-to-run-this-project)
2. [How to Extend This Project](#how-to-extend-this-project)
3. [Project Norms](#project-norms)
4. [Goal & Business Case](#goal--business-case)

---

## How to Run This Project

This section covers everything needed to run the documentation generation pipeline from scratch.

### Prerequisites

Before you begin, ensure your system has:

- **Node.js** (v18 or later) — for CLI metadata extraction and auxiliary tooling
- **.NET 9 SDK** — required for the typed `PipelineRunner` orchestrator and all C# generators
- **Azure OpenAI credentials** (if running steps that require AI) — Steps 2, 3, 4, and 6 invoke Azure OpenAI

Verify your setup:
```bash
dotnet --version  # Should output .NET 9.x.x
node --version    # Should output v18+
```

### Environment Setup

#### Create Your `.env` File

Navigate to the `docs-generation/` directory and create a `.env` file (template: `docs-generation/sample.env`):

```bash
cd docs-generation
cp sample.env .env
# Edit .env with your Azure OpenAI credentials
```

#### Required Environment Variables

Your `.env` file **must** contain:

| Variable | Purpose | Example |
|----------|---------|---------|
| `FOUNDRY_API_KEY` | Azure OpenAI API key | `(from your Azure Portal)` |
| `FOUNDRY_ENDPOINT` | Azure OpenAI endpoint URL | `https://mcp-doc-gen-dib.openai.azure.com/` |
| `FOUNDRY_MODEL_NAME` | Primary AI model for Steps 2, 3, 6 | `gpt-4.1-mini` |
| `FOUNDRY_MODEL_API_VERSION` | OpenAI API version | `2025-01-01-preview` |
| `TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME` | AI model for Step 4 (tool assembly) | `gpt-4o` |
| `TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION` | API version for Step 4 | `2025-01-01-preview` |

**⚠️ Security Note:** The `.env` file is **never committed** to source control (protected by `.gitignore`). Each developer creates their own locally.

#### Verify Configuration

Before running the full pipeline, verify your setup:
```bash
./start.sh --help
```

If you see usage information and no errors about missing credentials, you're ready to proceed.

### Running the Full Pipeline

#### Generate Documentation for All 52 Azure Services

This is the standard, complete run. Expect 22–26 hours:

```bash
cd <project-root>  # C:\Users\diberry\repos\project-azure-mcp-server\microsoft-mcp-doc-generation
./start.sh
```

**Output:** Creates `./generated/` directory containing:
- `cli/` — extracted Azure CLI metadata
- `tools/` — AI-generated tool documentation (markdown)
- `tool-family/` — assembled per-namespace articles
- `horizontal-articles/` — cross-cutting feature articles
- `example-prompts/` — example usage prompts
- `logs/` — step execution logs

#### Generate for a Single Namespace

Use this during development to iterate on a specific service. Outputs to `./generated-<namespace>/`:

```bash
./start.sh advisor       # Generates advisor namespace only
./start.sh cosmos        # Generates cosmos namespace only
```

**Duration:** 15–30 minutes per namespace (vs. 22–26 hours for all 52).

#### Generate Specific Steps Only

Run a subset of the 7-step pipeline for a namespace. Useful for testing or debugging:

```bash
./start.sh advisor 1,2,3     # Run steps 1, 2, 3 for advisor
./start.sh 1,2,3             # Run steps 1, 2, 3 for all namespaces
./start.sh compute 4         # Run step 4 (tool-family assembly) for compute
```

#### Skip Dependency Validation

When iterating on a single step whose prerequisites already exist from a prior run, skip dependency checks:

```bash
./start.sh advisor 4 --skip-deps   # Run step 4 without requiring steps 1-3
```

**Pipeline Steps:**

| Step | Name | Scope | Requires AI | Output |
|------|------|-------|-------------|--------|
| 0 | Bootstrap | Global | No | Validates config, extracts CLI metadata, cleans output dirs |
| 1 | Annotations & Parameters | Per-namespace | No | `annotations/`, `parameters/`, `tools-raw/` |
| 2 | Example Prompts | Per-namespace | Yes | `example-prompts/`, `example-prompts-prompts/` |
| 3 | Tool Generation | Per-namespace | Yes | `tools/` (AI-improved markdown) |
| 4 | Tool-Family Assembly | Per-namespace | Yes | `tool-family/{namespace}.md` |
| 5 | Skills Relevance | Per-namespace | No | `skills-relevance/{namespace}-skills-relevance.md` |
| 6 | Horizontal Articles | Per-namespace | Yes | `horizontal-articles/` |

### Running Individual Generators Directly

Each generator is a standalone .NET console application. You can invoke them directly for testing or debugging, bypassing `start.sh`:

#### CSharpGenerator — Annotations & Parameters

```bash
cd docs-generation/CSharpGenerator
dotnet run generate-docs <filtered-cli.json> <output-dir> --annotations --version <version>
dotnet run generate-docs <filtered-cli.json> <output-dir> --parameters --version <version>
```

**Inputs:**
- `<filtered-cli.json>` — Filtered Azure CLI metadata (created by Bootstrap step)
- `<output-dir>` — Output directory path
- `<version>` — CLI semantic version (e.g., `2.63.0`)

**Outputs:**
- `<output-dir>/annotations/{tool}.json`
- `<output-dir>/parameters/{tool}.json`

#### ToolGeneration_Raw — Raw Tool Markdown

```bash
cd docs-generation/ToolGeneration_Raw
dotnet run <filtered-cli.json> <output-dir>
```

**Output:** `<output-dir>/tools-raw/{tool}.md`

#### ExamplePromptGeneratorStandalone — Example Usage Prompts

```bash
cd docs-generation/ExamplePromptGeneratorStandalone
dotnet run <filtered-cli.json> <output-dir> <version> \
  --param-manifests <parameter-dir>
```

**Outputs:**
- `<output-dir>/example-prompts/{tool}.md`
- `<output-dir>/example-prompts-raw-output/{tool}.md`

#### ToolGeneration_Improved — AI-Enhanced Tool Markdown

```bash
cd docs-generation/ToolGeneration_Improved
dotnet run <output-dir> <version>
```

**Reads:** `<output-dir>/tools-raw/` and example prompts  
**Outputs:** `<output-dir>/tools/{tool}.md` (AI-refined)

#### HorizontalArticleGenerator — Cross-Service Articles

```bash
cd docs-generation/HorizontalArticleGenerator
dotnet run <output-dir> <version>
```

**Output:** `<output-dir>/horizontal-articles/horizontal-article-{namespace}.md`

### Building the Solution

To build the entire .NET solution without running the pipeline:

```bash
cd docs-generation
dotnet build
```

Or with strict warnings-as-errors mode:
```bash
dotnet build --no-incremental -p:TreatWarningsAsErrors=true
```

**Output:** Build artifacts in `bin/` and `obj/` directories across all projects.

### Running Tests

The solution includes unit and integration tests. Run them before committing changes:

```bash
cd docs-generation
dotnet test
```

Or run tests for a specific project:
```bash
dotnet test PipelineRunner.Tests/PipelineRunner.Tests.csproj
dotnet test CSharpGenerator.Tests/CSharpGenerator.Tests.csproj
```

**Test structure:**
- `PipelineRunner.Tests/Unit/` — Pipeline orchestration logic
- `PipelineRunner.Tests/Integration/` — End-to-end step execution
- Individual project test suites (e.g., `CSharpGenerator.Tests/`)

### Common Troubleshooting

#### Error: "Missing required AI configuration"

**Cause:** `FOUNDRY_API_KEY` or `FOUNDRY_ENDPOINT` not set in `.env`  
**Solution:**
```bash
# Verify .env exists in docs-generation/
cat docs-generation/.env | grep FOUNDRY

# If missing, copy from sample
cp docs-generation/sample.env docs-generation/.env
# Edit .env and add your credentials
```

#### Error: "429 Too Many Requests" (Rate Limiting)

**Cause:** Azure OpenAI quota exceeded  
**Solution:**
- Check your Azure OpenAI resource quota in the Azure Portal
- Reduce the number of namespaces in a single run:
  ```bash
  ./start.sh advisor        # Try a single namespace
  ./start.sh 1,2            # Try only Steps 1–2 (no AI calls)
  ```
- Wait for quota reset (usually 1 minute), then retry

#### Error: ".env file not found" or build failures

**Cause:** Running from wrong directory  
**Solution:**
```bash
# Always run from project root
cd C:\Users\diberry\repos\project-azure-mcp-server\microsoft-mcp-doc-generation
./start.sh
```

#### Step 2 or 3 hangs or times out

**Cause:** Long-running AI generation, especially on first run  
**Solution:**
```bash
# Check process logs
tail -f generated/logs/step2.log

# If hanging for >10 minutes, check Azure OpenAI service health
# and API quota. Restart with verbose output:
./start.sh advisor 2 --verbose
```

#### Step 4 tool-count mismatch in validation report

**Cause:** Phantom `## ` sections injected by AI heading replacement or assembly, inflating the detected tool count  
**Solution:** The pipeline now strips phantom H2 sections during Phase 1.5 (heading replacement) and Step 4 (post-assembly). If validation still fails, inspect the tool-family article for stray `## ` headings with no body content.

#### ParameterCoverageChecker false positives freezing sections

**Cause:** Single-word parameters (e.g., `query`) or JSON values inside quoted parameter strings (e.g., `--filter '{"key":"value"}'`) were rejected by the coverage checker  
**Solution:** `ParameterCoverageChecker` now handles single-word and array parameters correctly, and allows JSON-like content within quoted values. CLI switch prefixes (`--`) are stripped before matching.

---

## How to Extend This Project

This section explains how to add new pipeline steps or modify existing ones.

### Understanding the Step Architecture

#### IPipelineStep Contract

All pipeline steps implement the `IPipelineStep` interface defined in `PipelineRunner/Contracts/IPipelineStep.cs`:

```csharp
public interface IPipelineStep
{
    int Id { get; }                                          // Unique step identifier (0–6)
    string Name { get; }                                     // Human-readable step name
    StepScope Scope { get; }                                 // Global (once) or Namespace (per-service)
    FailurePolicy FailurePolicy { get; }                     // Fatal (block pipeline) or Warn (log and continue)
    IReadOnlyList<int> DependsOn { get; }                    // Step IDs that must complete first
    IReadOnlyList<IPostValidator> PostValidators { get; }    // Structural validation after step runs
    int MaxRetries { get; }                                  // Retry limit on failure
    
    // Main execution method
    ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken);
}
```

**Key Concepts:**

- **Scope**: 
  - `Global` — Runs once for all namespaces (e.g., Bootstrap, metadata extraction)
  - `Namespace` — Runs once per Azure service namespace

- **FailurePolicy**:
  - `Fatal` — Stop the entire pipeline on failure
  - `Warn` — Log the failure but continue to next step

- **DependsOn**: List of step IDs that must succeed before this step runs
  - Example: Step 3 depends on Steps 1 and 2

- **PostValidators**: Run after the step completes to check structural integrity
  - Example: `ToolFamilyPostAssemblyValidator` checks tool counts in Step 4 output

### Creating a New Pipeline Step

#### Step 1: Create the Step Class

Create a new file in `PipelineRunner/Steps/` (either `Bootstrap/` or `Namespace/` depending on scope):

```csharp
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

public sealed class MyCustomStep : NamespaceStepBase  // or inherit StepDefinition for Global steps
{
    public MyCustomStep()
        : base(
            id: 7,                                    // Next available step ID
            name: "My Custom Step",
            failurePolicy: FailurePolicy.Fatal,
            dependsOn: [1, 2],                        // Depends on Steps 1 and 2
            requiresAiConfiguration: false,           // Set true if calling Azure OpenAI
            createsFilteredCliView: false,            // Set true if you need filtered CLI JSON
            expectedOutputs: ["my-output-dir"])       // Directories this step creates
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        // Step logic here
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();

        try
        {
            // Your implementation
            var result = await DoSomethingAsync(context, cancellationToken);
            
            if (!result.Succeeded)
            {
                warnings.Add("Something failed");
                return BuildResult(context, processResults, false, warnings);
            }

            return BuildResult(context, processResults, true, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"Unexpected error: {ex.Message}");
            return BuildResult(context, processResults, false, warnings);
        }
    }

    private async Task<ProcessExecutionResult> DoSomethingAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        // Invoke a separate .NET project (see "Generator Subprocess Pattern" below)
        var projectPath = GetProjectPath(context, "MyGeneratorProject");
        return await context.ProcessRunner.RunDotNetProjectAsync(
            projectPath,
            ["arg1", "arg2"],
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
    }
}
```

#### Step 2: Register the Step in StepRegistry

Edit `PipelineRunner/Registry/StepRegistry.cs`, in the `CreateDefault()` method:

```csharp
public static StepRegistry CreateDefault(string scriptsRoot)
    => new([
        new BootstrapStep(),
        new AnnotationsParametersRawStep(),
        new ExamplePromptsStep(),
        new ToolGenerationStep(),
        new ToolFamilyCleanupStep(),
        new SkillsRelevanceStep(),
        new HorizontalArticlesStep(),
        new MyCustomStep(),  // Add your step here
    ]);
```

#### Step 3: Test the New Step

Create a test file in `PipelineRunner.Tests/Unit/` or `Integration/`:

```csharp
[TestClass]
public class MyCustomStepTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithValidInput_Succeeds()
    {
        // Arrange
        var step = new MyCustomStep();
        var context = CreateTestContext();

        // Act
        var result = await step.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.IsTrue(result.Succeeded);
    }
}
```

Run the test:
```bash
dotnet test PipelineRunner.Tests/PipelineRunner.Tests.csproj
```

### Generator Subprocess Pattern

Steps typically invoke **separate .NET projects** as subprocesses. This design isolates concerns and allows each generator to maintain its own dependencies and testing.

**Example: Step 3 (Tool Generation) invokes `ToolGeneration_Improved`**

```csharp
public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
{
    var generatorProject = GetProjectPath(context, "ToolGeneration_Improved");
    var processResult = await context.ProcessRunner.RunDotNetProjectAsync(
        generatorProject,
        [context.OutputPath, cliVersion],
        context.Request.SkipBuild,
        context.DocsGenerationRoot,
        cancellationToken);

    if (!processResult.Succeeded)
    {
        // Handle error
        return BuildResult(context, [processResult], false, warnings);
    }

    return BuildResult(context, [processResult], true, warnings);
}
```

**Benefits:**
- Each generator project can have its own test suite
- Failures are isolated and easier to debug
- Projects are independently deployable
- Easier to parallelize in future

**How to create a new generator project:**

1. Create a new C# console project in `docs-generation/MyGenerator/`
2. Define command-line arguments using `System.CommandLine`
3. Read from input files (CLI JSON, templates, etc.)
4. Write output to the specified directory
5. Return exit code 0 on success, 1 on failure
6. Create a corresponding `.Tests` project

**Example generator structure:**
```
docs-generation/
├── MyGenerator/
│   ├── Program.cs              # Entry point, argument parsing
│   ├── MyGeneratorLogic.cs      # Core implementation
│   └── MyGenerator.csproj
└── MyGenerator.Tests/
    ├── UnitTest1.cs
    └── MyGenerator.Tests.csproj
```

### Adding AI-Powered Steps

If your step calls Azure OpenAI (like Steps 2, 3, 4, and 6), follow this pattern:

#### 1. Set `requiresAiConfiguration: true`

```csharp
public MyAiStep()
    : base(
        id: 8,
        name: "AI-Powered Processing",
        requiresAiConfiguration: true,  // Enable AI config validation
        ...)
{
}
```

#### 2. Use the `GenerativeAIClient`

```csharp
var aiClient = GenerativeAIClient.CreateDefault();  // Uses .env credentials
var response = await aiClient.GenerateAsync(
    "Your prompt here",
    temperature: 0.5,
    maxTokens: 2000,
    cancellationToken);
```

#### 3. Store Prompts Centrally

Prompts for AI-powered steps are stored in `docs-generation/prompts/`:

```
docs-generation/prompts/
├── tool-generation-prompt.txt
├── example-prompts-prompt.txt
└── horizontal-articles-prompt.txt
```

Load a prompt:
```csharp
var promptPath = Path.Combine(context.DocsGenerationRoot, "prompts", "my-prompt.txt");
var prompt = await File.ReadAllTextAsync(promptPath, cancellationToken);
```

#### 4. Handle Rate Limiting

The `GenerativeAIClient` includes automatic exponential backoff (5 retries, 1s–16s delays). If you need custom retry logic:

```csharp
const int maxRetries = 5;
var delay = TimeSpan.FromSeconds(1);

for (int attempt = 0; attempt < maxRetries; attempt++)
{
    try
    {
        return await aiClient.GenerateAsync(prompt, cancellationToken);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == 429)
    {
        if (attempt == maxRetries - 1) throw;
        await Task.Delay(delay, cancellationToken);
        delay = delay.Multiply(2);  // Exponential backoff
    }
}
```

### Looking at Existing Steps for Reference

#### BootstrapStep — Simplest Example

Located: `PipelineRunner/Steps/Bootstrap/BootstrapStep.cs`

**What it does:**
- Validates Azure OpenAI config (if needed)
- Extracts Azure CLI metadata
- Initializes output directories
- Builds the solution

**Key takeaway:** Bootstrap is **Global** scope (runs once), but most steps are **Namespace** scope (run per service).

#### ExamplePromptsStep — AI-Powered Step

Located: `PipelineRunner/Steps/Namespace/ExamplePromptsStep.cs`

**What it does:**
- Requires AI configuration
- Invokes `ExamplePromptGeneratorStandalone` project
- Validates output using `ExamplePromptValidator`
- Creates filtered CLI view

**Key takeaway:** Shows how to invoke a subprocess generator and attach post-validators.

#### ToolGenerationStep — Complex AI Step

Located: `PipelineRunner/Steps/Namespace/ToolGenerationStep.cs`

**What it does:**
- Runs `ToolGeneration_Composed` and `ToolGeneration_Improved` in sequence
- Attaches multiple post-validators
- Handles both successful and failed runs

**Key takeaway:** Demonstrates chaining multiple generators and multi-validator attachment.

---

## Project Norms

This section documents team standards for code, branching, configuration, and quality.

### Branching Strategy

- **Content and generation changes** → Branch off `main`, open pull request
  - Branch naming: `feature/short-description` or `fix/issue-number`
  - Examples: `feature/add-acronym-mapping`, `fix/rate-limit-handling`

- **Squad state** (`.squad/` directory) → Commit directly to `main`
  - No PR required for agent decisions, history updates, or session state

- **Pipeline output** (`generated/`, `generated-*`) → **Never committed**
  - All generated content is ephemeral; regenerated with each run

### AI Model Selection

- **Primary model for Steps 2, 3, 6:** `gpt-4.1-mini`
- **Model for Step 4 (tool assembly):** `gpt-4o`
- **No premium tiers** → Use standard (non-premium) endpoints
- **Why:** Cost control and determinism for team runs

### Namespace Naming

Use exact, lowercase Azure service namespace names. Do not abbreviate.

| ✅ Correct | ❌ Incorrect |
|-----------|------------|
| `extensionfoundry` | `foundry` |
| `appservice` | `app-service` |
| `cosmosdb` | `cosmos` |
| `appconfig` | `app-configuration` |

**Rationale:** Canonical names match Azure CLI output and prevent confusion.

### Test Expectations

Before merging any PR:

1. **Solution must build:**
   ```bash
   dotnet build docs-generation/
   ```

2. **All existing tests must pass:**
   ```bash
   dotnet test docs-generation/
   ```

3. **If you add new functionality:**
   - Add corresponding unit tests
   - Ensure tests cover the happy path and error cases
   - Run tests locally before pushing

4. **If you change generation output:**
   - Regenerate a sample namespace and inspect output:
     ```bash
     ./start.sh advisor
     ```
   - Commit sample output if it's intentional improvement

### .env and Secrets Management

- **`.env` is never committed** — covered by `.gitignore` patterns:
  ```
  .env
  .env*
  **/.env**
  ```

- **Each developer creates their own `.env`:**
  ```bash
  cp docs-generation/sample.env docs-generation/.env
  # Add your FOUNDRY_API_KEY and other credentials
  ```

- **No hardcoded secrets in code** — Use environment variables only

- **Credential rotation:** If a key is accidentally committed, rotate it immediately in the Azure Portal

### Pipeline Output Location

All generated content goes to:

| Run Type | Output Directory |
|----------|------------------|
| `./start.sh` | `./generated/` |
| `./start.sh advisor` | `./generated-advisor/` |
| `./start.sh cosmos 1,2` | `./generated-cosmos/` |

**Directory structure:**
```
generated/
├── cli/                      # Extracted Azure CLI metadata
├── tools/                    # AI-generated tool markdown
├── tool-family/              # Assembled per-namespace articles
├── horizontal-articles/      # Cross-cutting feature articles
├── example-prompts/          # Example usage prompts
├── parameters/               # Parameter manifests
├── annotations/              # Tool annotations
├── tools-raw/                # Raw (non-AI) tool markdown
├── logs/                     # Step execution logs
└── reports/                  # Post-validation reports
```

### Templates and Prompts

- **Handlebars templates:** `docs-generation/templates/`
  - `tool-markdown.hbs` — Template for single tool markdown
  - `tool-family-article.hbs` — Template for assembled namespace article
  - `horizontal-article.hbs` — Template for cross-cutting articles

- **AI prompts:** `docs-generation/prompts/`
  - One `.txt` file per AI-powered step
  - Prompts include context, instructions, and output format specifications
  - When modifying prompts, regenerate sample output and review for quality

### Current AI Models and Configuration

**Step 2 (Example Prompts):**
- Model: `gpt-4.1-mini` (from `FOUNDRY_MODEL_NAME`)
- Temperature: 0.7
- Max tokens: ~2000

**Step 3 (Tool Generation):**
- Model: `gpt-4.1-mini` (from `FOUNDRY_MODEL_NAME`)
- Temperature: 0.5
- Max tokens: ~3000

**Step 4 (Tool-Family Assembly):**
- Model: `gpt-4o` (from `TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME`)
- Temperature: 0.2
- Max tokens: ~8000

**Step 6 (Horizontal Articles):**
- Model: `gpt-4.1-mini` (from `FOUNDRY_MODEL_NAME`)
- Temperature: 0.7
- Max tokens: ~4000

**Rate limiting:**
- Automatic exponential backoff with 5 retries
- Initial delay: 1 second
- Max delay: 16 seconds

---

## Goal & Business Case

### What

**Automated documentation generation system** that transforms Azure CLI metadata and tool definitions into high-quality, AI-enhanced markdown documentation for Azure's Model Context Protocol (MCP) server.

### Why

The Azure MCP Server exposes **52+ Azure service namespaces** with **hundreds of tools**. Maintaining consistent, up-to-date documentation across this catalog is labor-intensive:

- **Scope:** Each service has dozens of tools; each tool has multiple parameters, examples, and use cases
- **Freshness:** Tool definitions change with Azure service updates; documentation must stay in sync
- **Quality:** Consistency is hard to enforce manually across 52 services and hundreds of tools
- **Time:** Manual creation would require thousands of person-hours annually

**Solution:** A deterministic, AI-enhanced pipeline that generates documentation from canonical sources (Azure CLI metadata, tool definitions, prompts) and validates structural integrity at every step.

### How

The pipeline operates in **6 sequential steps per namespace**, taking a canonical source (Azure CLI metadata) through progressive refinement:

1. **Bootstrap (Step 0)** → Validate configuration, extract CLI metadata, build project
2. **Annotations & Parameters (Step 1)** → Generate structured tool metadata from CLI definitions
3. **Example Prompts (Step 2)** → Use AI to generate realistic usage examples for each tool
4. **Tool Generation (Step 3)** → Use AI to compose markdown for each tool, referencing examples and annotations
5. **Tool-Family Assembly (Step 4)** → Use AI to assemble per-namespace articles, validate structural integrity
6. **Skills Relevance (Step 5)** → Generate Copilot skills relevance metadata (supplementary)
7. **Horizontal Articles (Step 6)** → Generate cross-service feature articles using AI

**Key design principles:**
- **Deterministic:** Same input + same config = same output (reproducible)
- **Validated:** Each step includes post-execution validators to catch structural issues early
- **Modular:** Each step is a separate .NET project with its own tests and CLI interface
- **Traceable:** Logs at every step for debugging and auditing

### Scale

- **Full catalog run:** 52 namespaces, 22–26 hours (sequential namespace processing)
- **Single namespace:** ~15–30 minutes
- **Annual freshness:** Pipeline can regenerate the full catalog quarterly (or on-demand)

### Quality

The pipeline includes layered quality assurance:

- **Structural validation** → Tool counts, cross-reference integrity, frontmatter completeness
- **Post-assembly checking** → Verify all tools appear in both tool list and capability descriptions
- **Content coverage** → AI-generated examples, scenarios, and limitations
- **Brand consistency** → Acronym normalization (ETag, OAuth, JSON, etc.), term standardization

### Future: Architecture Modernization

Current infrastructure is stable but holds opportunities for significant improvement:

**Planned enhancements (6-week roadmap):**

1. **AI abstraction layer** → Replace custom `GenerativeAIClient` with `Microsoft.Extensions.AI.IChatClient`
   - Unlocks caching middleware, OpenTelemetry, provider portability
   - Enables A/B testing between models

2. **Evaluation framework** → Integrate `Microsoft.Extensions.AI.Evaluation`
   - Automate quality scoring (Relevance, Coherence, Fluency, Completeness)
   - Reduce manual review burden

3. **Bounded parallelism** → Replace sequential `foreach` namespace loop with bounded task parallelism
   - Reduce full-run duration from 22–26 hours to ~4–6 hours
   - Maintain determinism and rate-limit safety

4. **Centralized configuration** → Migrate from scattered `.env` and hardcoded config to structured settings
   - Enable per-step model selection (e.g., use cheaper model for Steps 2, 5)
   - Support feature flags for experimental steps

5. **Structural output** → Migrate from free-form markdown rewrites to structured output contracts
   - JSON-to-markdown serialization with validation
   - Easier integration with downstream publishing systems

**Migration approach:** Incremental (keep the pipeline running while modernizing layer-by-layer) rather than greenfield rebuild. The current pipeline's typed step model and validator framework are strong reusable assets.

---

## Appendix: Quick Reference

### Common Commands

```bash
# Generate all 52 namespaces (full run, ~22-26 hours)
./start.sh

# Generate one namespace for testing (~15-30 minutes)
./start.sh advisor

# Run specific steps only
./start.sh advisor 1,2,3      # Steps 1, 2, 3 for advisor
./start.sh 2,4                # Steps 2, 4 for all namespaces

# Dry-run (show plan without executing)
./start.sh --dry-run

# Build without running pipeline
cd docs-generation && dotnet build

# Run all tests
cd docs-generation && dotnet test

# Check build with strict warnings
cd docs-generation && dotnet build -p:TreatWarningsAsErrors=true
```

### Key Files and Directories

```
project-root/
├── start.sh                              # Entry point
├── docs-generation/
│   ├── sample.env                        # .env template (do not edit)
│   ├── .env                              # Local config (created by developer, never committed)
│   ├── PipelineRunner/
│   │   ├── PipelineRunner.cs             # Orchestrator
│   │   ├── Registry/StepRegistry.cs      # Step registration
│   │   ├── Contracts/IPipelineStep.cs    # Step interface
│   │   └── Steps/                        # Step implementations
│   ├── prompts/                          # AI prompts for Steps 2, 3, 4, 6
│   ├── templates/                        # Handlebars templates
│   └── [Generator projects]/             # Each .NET generator project
├── generated/                            # Output directory (single full run)
├── generated-{namespace}/                # Output directory (single namespace run)
└── docs/
    ├── PROJECT-GUIDE.md                  # This file
    └── [Other documentation]
```

---

**Questions or issues?** Refer to the troubleshooting section above, check the step logs in `generated/logs/`, or review relevant `.squad/decisions/` files for team context.
