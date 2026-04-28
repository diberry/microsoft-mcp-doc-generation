# Pipeline Validation Skill

> Validate pipeline changes by preserving a Last Known Good (LKG) baseline, regenerating content, and comparing outputs to detect regressions.

## When to Use

Invoke this skill when the user says any of:
- "validate pipeline changes", "run pipeline validation"
- "compare generated output", "check for regressions"
- "test pipeline on namespace X", "verify generation quality"
- "create LKG baseline", "compare against baseline"
- "run prompt regression", "fingerprint check"

## Architecture Notes

- **Step execution model:** Pipeline steps are in-process typed classes within PipelineRunner (e.g., `AnnotationsParametersRawStep : NamespaceStepBase`). Some steps shell out to legacy standalone projects via `ShimStep`/`ProcessRunner`, but orchestration and validation happen in-process.
- **Canonical entry point:** Always invoke steps through `PipelineRunner` (or `start.sh` which wraps it). Never run individual step projects directly — doing so bypasses the dependency graph, post-validators, and failure-policy enforcement.
- **Existing tooling:** This skill builds on `prompt-regression.sh` (seed/test/compare/report/status) — it does NOT replace or duplicate that infrastructure.
- **Non-determinism:** AI-generated content varies between runs. Steps 3, 4, 6 use AI. Step 4 (ToolFamilyCleanup) has `maxRetries: 2`; steps 3 and 6 have no retries. Step 5 (SkillsRelevance) has `FailurePolicy.Warn` — it's the only non-fatal step. Comparisons focus on structural integrity, section presence, and factual consistency — not byte-level equality.

## Prerequisites

| Requirement | Details |
|---|---|
| .NET SDK | 9.0+ (builds `docs-generation.slnx`) |
| AI credentials | `FOUNDRY_API_KEY`, `FOUNDRY_ENDPOINT`, `FOUNDRY_MODEL_NAME` in environment or `.env` |
| Working directory | Repository root (`microsoft-mcp-doc-generation/`) |
| Bash (for prompt-regression.sh) | Git Bash on Windows or native on Linux/macOS |

## Representative Namespaces

The following 5 namespaces provide broad coverage across pipeline complexity:

| Namespace | Why representative |
|---|---|
| `applens` | Complex diagnostics tooling, many tools |
| `cloudarchitect` | Architecture recommendations, cross-cutting |
| `deploy` | Deployment operations, multi-step procedures |
| `compute` | Large namespace (VMs, VMSS, disks), stress test |
| `fileshares` | Simple namespace, baseline sanity check |

## Workflow

### Phase 1: Preserve Last Known Good (LKG)

Before regenerating, preserve the current output as a named baseline:

```bash
# For a single namespace — rename the entire generated folder:
mv generated-{namespace} generated-{namespace}-lkg

# Or use prompt-regression.sh to seed a fingerprint baseline:
./prompt-regression.sh seed
```

The `seed` command creates fingerprint baselines in `mcp-tools/DocGeneration.PromptRegression.Tests/Baselines/`. A `fingerprint-baseline.json` also exists at repo root for quick structural comparison.

### Phase 2: Dry-Run Pre-Check

Run a dry-run to confirm pipeline wiring and dependency resolution before expensive AI regeneration:

```bash
./start.sh {namespace} --dry-run
```

Or via PipelineRunner directly:
```bash
dotnet run --project mcp-tools/DocGeneration.PipelineRunner -- \
  --namespace {namespace} --dry-run
```

**If dry-run fails:** Stop. Fix pipeline configuration before proceeding to full generation.

### Phase 3: Regenerate

Run full pipeline for target namespace(s):

```bash
# Single namespace, all steps:
./start.sh {namespace}

# Single step only (e.g., step 4 = ToolFamilyCleanup):
./start.sh {namespace} 4
```

Pipeline steps in order:
1. AnnotationsParametersRaw
2. ExamplePrompts
3. ToolGeneration
4. ToolFamilyCleanup
5. SkillsRelevance (FailurePolicy.Warn — non-fatal)
6. HorizontalArticles

### Phase 4: Compare

Compare regenerated output against the LKG baseline:

```bash
# Automated comparison using prompt-regression.sh:
./prompt-regression.sh test
./prompt-regression.sh compare
./prompt-regression.sh report
```

**Manual comparison focus areas** (in priority order):

1. **Tool-family file** (`generated-{namespace}/tool-family/azure-{namespace}.md`)
   - H2 section structure preserved
   - Tool parameter tables complete
   - No orphaned or duplicated sections

2. **Horizontal article** (`generated-{namespace}/horizontal-articles/`)
   - Cross-cutting themes identified correctly
   - Section organization logical
   - No content from wrong namespace

3. **Validation report** (`generated-{namespace}/reports/`)
   - Post-assembly validation passes
   - No new warnings introduced

### Phase 5: Trace Divergence (if issues found)

If comparison reveals regressions, trace backward through pipeline:

```
Tool-family file bad? → Check Step 4 output in generated-{namespace}/tool-family/
  ↓ still bad?
Check Step 3 output in generated-{namespace}/tools/
  ↓ still bad?
Check Step 2 output in generated-{namespace}/example-prompts/
  ↓ still bad?
Check Step 1 output in generated-{namespace}/annotations/ and generated-{namespace}/parameters/
  ↓ still bad?
Check Step 0 (bootstrap) — source data from microsoft/mcp repo
```

Each step's output is preserved in subdirectories of `generated-{namespace}/`, enabling precise isolation of where the regression was introduced.

### Phase 6: Report

Generate a summary status:

```bash
./prompt-regression.sh status
```

Or manually summarize:
- ✅ / ❌ per namespace
- Which pipeline step introduced the divergence (if any)
- Severity: cosmetic (formatting) vs structural (missing sections) vs factual (wrong content)
- Recommendation: merge / fix required / investigation needed

## Quality Gates (AD-028)

Three gates must pass before any pipeline PR merges:

1. **Full test suite:** `dotnet test` — all 2,397+ tests pass
2. **Pipeline dry-run:** `--dry-run` succeeds on representative namespaces
3. **Fingerprint regression:** `prompt-regression.sh compare` shows no structural regressions

## Output Locations

| Artifact | Path |
|---|---|
| Tool-family articles | `generated-{namespace}/tool-family/azure-{namespace}.md` |
| Horizontal articles | `generated-{namespace}/horizontal-articles/` |
| Validation reports | `generated-{namespace}/reports/` |
| Per-tool files | `generated-{namespace}/tools/` |
| Annotations | `generated-{namespace}/annotations/` |
| Parameters | `generated-{namespace}/parameters/` |
| Example prompts | `generated-{namespace}/example-prompts/` |
| Fingerprint baseline | `fingerprint-baseline.json` (repo root) |
| Regression baselines | `mcp-tools/DocGeneration.PromptRegression.Tests/Baselines/` |
