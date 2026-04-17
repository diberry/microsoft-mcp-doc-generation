# Prompt Regression Testing

This project measures and compares AI-generated output quality across prompt changes. It provides objective, measurable metrics — not subjective assessments.

## Quick Start

### Using the regression runner (recommended)

```bash
# First time: seed baselines from current generated output
./prompt-regression.sh seed

# After making a prompt change: regenerate and compare
./prompt-regression.sh test 6          # Test Step 6 (horizontal articles)
./prompt-regression.sh test 4          # Test Step 4 (tool-family)
./prompt-regression.sh test 4,6        # Test both

# Compare without regenerating (after manual changes)
./prompt-regression.sh compare

# Check status
./prompt-regression.sh status
```

### Representative namespaces

The runner tests 5 namespaces (small → large): `applens`, `cloudarchitect`, `deploy`, `compute`, `fileshares`.

### Run regression tests directly

```bash
dotnet test mcp-tools/DocGeneration.PromptRegression.Tests/
```

### What the tests check

- **RegressionComparisonTests**: Compare baselines vs candidates — section counts, missing sections, quality metrics, content volume
- **PromptContentTests**: Verify all 12 prompt files exist and contain expected rules (sentinel tests)
- **QualityMetricsTests**: Validate metric calculation accuracy
- **BaselineManagerTests**: Golden file storage/retrieval
- **MetricsComparisonTests**: Regression/improvement detection
- **DiffReporterTests**: Report generation

## Workflow: Testing a Prompt Change

### 1. Capture baseline (before your change)

Run the pipeline for representative namespaces and save the output as baselines:

```csharp
var manager = new BaselineManager();

// Copy generated output as golden files
var content = File.ReadAllText("generated-advisor/horizontal-articles/horizontal-article-advisor.md");
manager.SaveBaseline("advisor", "horizontal-article.md", content);
```

Or copy files manually into `Baselines/{namespace}/`.

### 2. Make your prompt change

Edit the prompt file in the relevant step's `prompts/` directory.

### 3. Regenerate and compare

```bash
# Regenerate
./start.sh advisor 6

# Copy new output as candidate
# Then run regression tests to compare
dotnet test mcp-tools/DocGeneration.PromptRegression.Tests/ --filter "Category=Regression"
```

### 4. Review the diff report

The `DiffReporter` generates a markdown comparison:

```
| Metric              | Baseline | Candidate | Delta    |
|---------------------|----------|-----------|----------|
| Sections            | 7        | 8         | +1 ✅    |
| Contractions        | 67%      | 89%       | +22% ✅  |
| Future tense        | 3        | 0         | -3 ✅    |
| Fabricated URLs     | 0        | 0         | =        |
```

### 5. Update baseline if improved

If the candidate is better, replace the baseline:

```csharp
manager.SaveBaseline("advisor", "horizontal-article.md", candidateContent);
```

## Quality Metrics

| Metric | What it measures | How |
|--------|-----------------|-----|
| **Section count** | H1/H2/H3 headings present | Regex `^#{1,3}\s+` |
| **Word count** | Content volume (truncation/bloat) | Whitespace split |
| **Frontmatter validity** | Required YAML fields present | Regex extraction |
| **Contraction rate** | "don't" vs "do not" usage | Pattern matching |
| **Future tense violations** | "will return" vs "returns" | Verb pattern matching |
| **Fabricated URLs** | Hallucinated `/docs/` URL patterns | URL regex |
| **Branding violations** | "CosmosDB", "Azure VMs", etc. | Brand name patterns |

**Scope note**: `MissingSections` checks for `## Prerequisites`, `## Best practices`, `## Related content` — these are required for **horizontal articles** (Step 6). Tool-family articles (Step 4) have different section requirements.

## Directory Structure

```
DocGeneration.PromptRegression.Tests/
├── Baselines/          # Golden files (committed to git)
│   └── {namespace}/    # One directory per namespace
├── Candidates/         # Generated at test time (gitignored)
├── Reports/            # Diff reports (gitignored)
├── Infrastructure/     # Reusable components
│   ├── QualityMetrics.cs
│   ├── MetricsComparison.cs
│   ├── BaselineManager.cs
│   └── DiffReporter.cs
└── Tests/              # xUnit test classes
```

## Interpreting Results

- **✅ Improved**: Fewer violations, better contraction rate, more sections
- **⚠️ Regression**: More violations, lost sections, worse metrics
- **➖ No change**: Metrics identical (prompt change had no measurable effect)

When a regression is detected, investigate before merging — the prompt change may have unintended side effects.

## CI Integration

The **Prompt Regression Tests** workflow (`.github/workflows/prompt-regression-ci.yml`) runs automatically on PRs that modify prompt files or regression test infrastructure.

### Trigger paths

The CI job runs when a PR changes any of:

- `mcp-tools/**/prompts/**` — AI system/user prompt files
- `prompt-regression.sh` — Regression runner script
- `mcp-tools/DocGeneration.PromptRegression.Tests/**` — Test code or baselines
- `.github/workflows/prompt-regression-ci.yml` — The workflow itself

PRs that don't touch these paths skip the job entirely.

### What CI runs

The CI job executes `dotnet test` on the `DocGeneration.PromptRegression.Tests` project. This runs:

| Test class | Runs in CI? | What it checks |
|---|---|---|
| **PromptContentTests** | ✅ Always | Prompt files exist and contain expected rules |
| **QualityMetricsTests** | ✅ Always | Metric calculation accuracy |
| **BaselineManagerTests** | ✅ Always | Baseline file storage and retrieval |
| **MetricsComparisonTests** | ✅ Always | Regression/improvement detection logic |
| **DiffReporterTests** | ✅ Always | Report generation |
| **RegressionComparisonTests** | ⏭️ Skips | Requires candidates (populated by `prompt-regression.sh compare`) |

The `RegressionComparisonTests` gracefully skip when no candidates directory is populated — this is by design. Full regression comparison requires AI regeneration and is performed locally via `prompt-regression.sh test`.

### Manual trigger

You can also trigger the workflow manually from the Actions tab using **workflow_dispatch**.

### AI credentials

The workflow has access to `FOUNDRY_*` repository secrets for future expansion of AI-powered regression tests in CI.

## Baseline Management

### When to update baselines

Update baselines after **intentional** prompt improvements that result in better output quality. Never update baselines to mask a regression.

### How to update baselines

```bash
# 1. Make your prompt changes

# 2. Regenerate output for representative namespaces
./start.sh applens 4,6 --skip-deps
./start.sh cloudarchitect 4,6 --skip-deps
./start.sh deploy 4,6 --skip-deps
./start.sh compute 4,6 --skip-deps
./start.sh fileshares 4,6 --skip-deps

# 3. Compare against current baselines
./prompt-regression.sh compare

# 4. Review the regression report
./prompt-regression.sh report

# 5. If output improved, re-seed baselines
./prompt-regression.sh seed

# 6. Commit updated baselines alongside your prompt changes
git add mcp-tools/DocGeneration.PromptRegression.Tests/Baselines/
git commit -m "chore: update regression baselines for prompt improvements"
```

### Baseline files

Baselines live in `Baselines/{namespace}/` with two articles per namespace:

- `tool-family.md` — Step 4 output (tool-family assembly)
- `horizontal-article.md` — Step 6 output (horizontal overview article)

These are committed to git and serve as the golden reference for quality comparison.

### Representative namespaces

The 5 namespaces cover a range of service sizes and complexity:

| Namespace | Size | Why selected |
|---|---|---|
| `applens` | Small (1 tool) | Minimum viable article |
| `cloudarchitect` | Small (2 tools) | Simple multi-tool service |
| `deploy` | Medium (5 tools) | Moderate complexity |
| `compute` | Large (10+ tools) | High tool count |
| `fileshares` | Medium (4 tools) | File-oriented operations |
