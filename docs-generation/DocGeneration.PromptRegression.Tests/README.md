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
dotnet test docs-generation/DocGeneration.PromptRegression.Tests/
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
dotnet test docs-generation/DocGeneration.PromptRegression.Tests/ --filter "Category=Regression"
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
