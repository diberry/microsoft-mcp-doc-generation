# Baseline Fingerprinting Tool

The fingerprint tool creates lightweight JSON snapshots of all generated documentation output. Use it to detect drift between pipeline runs — whether caused by prompt changes, model updates, or template fixes.

## Quick Start

```bash
# Generate a snapshot of current output
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- snapshot

# Compare two snapshots
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- diff --baseline before.json --candidate after.json
```

## Commands

### snapshot

Scans all `generated-*` directories and produces a JSON fingerprint file.

```bash
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- snapshot [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `--namespace, -n` | Fingerprint a single namespace | All namespaces |
| `--output, -o` | Output file path | `./fingerprint-baseline.json` |
| `--repo-root, -r` | Repository root directory | Auto-detect via `mcp-doc-generation.sln` |

### diff

Compares two snapshot JSON files and generates a markdown diff report.

```bash
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- diff [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `--baseline, -b` | Path to baseline snapshot (required) | — |
| `--candidate, -c` | Path to candidate snapshot (required) | — |
| `--output, -o` | Output file for report | stdout |

Returns exit code 1 if quality regressions are detected (useful for CI gating).

## What Gets Fingerprinted

For each namespace, the tool captures:

| Category | Details |
|----------|---------|
| **File inventory** | Total file count and size per namespace and subdirectory |
| **Tool-family article** | H2 headings, frontmatter fields, word count, section count, tool_count |
| **Horizontal article** | Same structural analysis as tool-family |
| **Quality metrics** | Future tense violations, fabricated URLs, branding violations, contraction rate |

## JSON Schema

The snapshot file (~63KB for 19 namespaces) follows this structure:

```json
{
  "version": "1.0",
  "timestamp": "2026-03-29T14:30:00Z",
  "namespaces": {
    "advisor": {
      "fileCount": 71,
      "totalSizeBytes": 4477952,
      "directories": {
        "tool-family": { "fileCount": 1, "totalSizeBytes": 45000 },
        "annotations": { "fileCount": 1, "totalSizeBytes": 500 }
      },
      "toolFamilyArticle": {
        "fileName": "advisor.md",
        "sizeBytes": 45000,
        "wordCount": 1500,
        "sectionCount": 8,
        "h2Headings": ["## Get recommendations", "## Related content"],
        "frontmatterFields": ["title", "description", "ms.date", "tool_count"],
        "toolCount": 3
      },
      "horizontalArticle": null,
      "qualityMetrics": {
        "futureTenseViolations": 0,
        "fabricatedUrlCount": 0,
        "brandingViolations": 0,
        "contractionRate": 0.85
      }
    }
  }
}
```

> **Nullable fields:** `toolFamilyArticle`, `horizontalArticle`, `qualityMetrics`, and `toolCount` are `null` when the corresponding content doesn't exist for that namespace (e.g., pipeline hasn't completed all steps yet).

## Quality Metrics Reference

| Metric | What it detects | Regression threshold |
|--------|----------------|---------------------|
| `futureTenseViolations` | "will return", "will list", etc. — future tense with action verbs | Any increase |
| `fabricatedUrlCount` | URLs matching `learn.microsoft.com/…/docs/…` (hallucinated link patterns) | Any increase |
| `brandingViolations` | Outdated names: "CosmosDB", "Azure VMs", "MSSQL", "Azure Active Directory", "Azure AD", "AAD", "VMSS" | Any increase |
| `contractionRate` | Ratio of contractions used to total opportunities (e.g., 0.85 = 85% of "do not"/"don't" opportunities use contractions). Higher is better. | Drop > 5 percentage points |

## Diff Report

The diff report is a markdown document with:

1. **Summary table** — one row per namespace showing file count delta, size delta, heading changes, and quality status
2. **Detailed sections** — per-namespace breakdown of headings added/removed, frontmatter changes, and quality regressions
3. **Verdict** — overall pass/fail with regression count

## Typical Workflows

### Before a prompt change

```bash
# 1. Snapshot current state
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- snapshot --output before.json

# 2. Make your prompt change and regenerate
./start.sh advisor 4

# 3. Snapshot new state
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- snapshot --output after.json

# 4. Compare
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- diff -b before.json -c after.json -o diff-report.md
```

### In CI

```bash
# Compare against committed baseline
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- diff \
  --baseline fingerprint-baseline.json \
  --candidate current-snapshot.json
# Exit code 1 = quality regressions detected
```

### Updating the baseline

When output improves (new features, better prompts), update the committed baseline:

```bash
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint/ -- snapshot
git add fingerprint-baseline.json
git commit -m "chore: Update fingerprint baseline"
```

## Test Coverage

Tests in `DocGeneration.Tools.Fingerprint.Tests/`:

- **MarkdownAnalyzerTests** (18) — Article analysis, frontmatter extraction, quality detection
- **SnapshotDifferTests** (16) — Diff computation, regression detection, report generation
- **SnapshotGeneratorTests** (13) — File scanning, namespace discovery, real filesystem tests
