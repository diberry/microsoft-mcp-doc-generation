# Pipeline Output Regression — Contributor Runbook

## Overview

The **Pipeline Output Regression** workflow (`.github/workflows/pipeline-output-regression.yml`) automatically validates that PRs touching generation logic do not degrade pipeline output quality. This runbook explains how to work with the gate, interpret results, and handle failures.

## When Does It Run?

The workflow triggers on PRs to `main` that modify paths affecting documentation generation:

| Path Pattern | Gate Type |
|---|---|
| `mcp-tools/DocGeneration.Steps.AnnotationsParametersRaw.*` | Deterministic |
| `mcp-tools/DocGeneration.Steps.ExamplePrompts.Validation/` | Deterministic |
| `mcp-tools/DocGeneration.Steps.ExamplePrompts.Generation/` | AI |
| `mcp-tools/DocGeneration.Steps.ToolGeneration.*` | AI |
| `mcp-tools/DocGeneration.Steps.ToolFamilyCleanup/` | AI |
| `mcp-tools/DocGeneration.Steps.HorizontalArticles/` | AI |
| `mcp-tools/templates/**` | Both |
| `mcp-tools/scripts/**` | Both |
| `mcp-tools/data/*.json` | Both |
| `mcp-tools/**/prompts/**` | AI |
| `start.sh`, `prompt-regression.sh`, `merge-namespaces.sh` | Both |

Infrastructure-only changes (CI workflows, docs, tests without logic changes) skip the gate entirely.

## Workflow Jobs

### 1. classify-change

Determines which gates to run based on changed files:

- **requires_deterministic** — file touches deterministic pipeline steps (annotations, parameters, raw tools)
- **requires_ai** — file touches AI-dependent steps (prompts, composition, improvements, cleanup, horizontal articles)
- **affected_steps** — list of pipeline steps (0–6) affected by the change
- **namespaces_to_run** — targeted namespaces if path contains a namespace name; otherwise falls back to 5 representative namespaces (applens, cloudarchitect, deploy, compute, fileshares)

Downstream jobs consume these outputs to run only relevant gates on relevant namespaces.

### 2. deterministic-regression

Runs when `requires_deterministic == true`:

1. Resolves target namespaces and deterministic steps from classify output
2. Builds the full .NET solution
3. Runs all solution tests
4. Executes affected deterministic steps (subset of 0, 1, 2, 5) for target namespaces
5. Runs the fingerprint gate comparing output against `fingerprint-baseline.json`
6. Generates a fingerprint diff summary

**Timeout**: 45 minutes

### 3. ai-regression

Runs when `requires_ai == true`:

1. Fails immediately for fork PRs (no AI credential access)
2. Resolves target namespaces and AI steps from classify output
3. Builds the full .NET solution
4. Runs all solution tests
5. Executes affected AI steps (always includes step 1 as prerequisite) for target namespaces
6. Runs prompt regression comparison (populates candidates and generates report)
7. Runs fingerprint gate + prompt regression gate
8. Uploads regression artifacts

**Timeout**: 120 minutes

## Representative Namespaces

Both gates use 5 namespaces chosen for diversity:

| Namespace | Why |
|---|---|
| `applens` | Small, fast, good baseline |
| `cloudarchitect` | Medium complexity |
| `deploy` | Deployment patterns |
| `compute` | Large, merge-group member |
| `fileshares` | Storage family |

## Interpreting Results

### Fingerprint Gate

The fingerprint gate compares structural properties of generated output against a known-good baseline:

- **PASS** — output structure matches baseline within acceptable tolerance
- **FAIL** — structural drift detected (tool count, parameter count, file count changes)

Check `artifacts/fingerprint-diff-summary.md` for details on what drifted.

### Prompt Regression Gate

The prompt regression gate compares AI-generated content quality:

- **PASS** — generated content meets quality thresholds
- **FAIL** — quality regression detected (check `artifacts/prompt-regression-report.md`)

## Handling Failures

### Deterministic Gate Failure

1. Review `artifacts/deterministic-regression.log`
2. Check `artifacts/fingerprint-diff-summary.md` for drift details
3. If the change intentionally changes output structure:
   - Update `fingerprint-baseline.json` with the new snapshot
   - Add a note in the PR body under `regression_evidence.notes`
4. If unintentional: fix the regression in your code

### AI Gate Failure

1. Review `artifacts/ai-regression.log` and `artifacts/prompt-regression-report.md`
2. If transient (rate limits, timeouts): re-run the workflow
3. If consistent failure:
   - Check prompt changes for unintended quality regression
   - Compare generated output against baseline samples
   - Update baselines if the new output is genuinely better

### Flaky Gate Policy

Per team decision (PRD #599):

- **1 automatic rerun** for transient failures (rate limits, network timeouts)
- **Quarantine after 3 failures in 7 days** — the specific gate is marked flaky and reported to the team
- Parker (QA) owns flaky gate triage

## Updating Baselines

When a PR intentionally improves output (new format, better prompts, etc.):

1. Run the pipeline locally for representative namespaces:
   ```bash
   ./start.sh applens 1    # deterministic baseline
   ./start.sh applens 1,2,3,4,6  # full baseline
   ```

2. Generate new fingerprint snapshot:
   ```bash
   dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint -- snapshot --output fingerprint-baseline.json
   ```

3. Update prompt regression baselines:
   ```bash
   ./prompt-regression.sh seed    # captures new baseline
   ```

4. Include baseline updates in the same PR with explanation in the PR body.

5. Avery (Lead) must approve baseline replacements.

## Baseline Freshness SLO

- Baselines must be refreshed at least every **14 days**
- Parker owns baseline freshness monitoring
- Stale baselines trigger a team notification

## Fork PRs

Fork PRs cannot access repository secrets (AI credentials). The AI regression job will:

- Detect the fork context
- Fail with a clear message: "AI regression required but fork PRs cannot access AI credentials"
- A maintainer must re-run from the base repo after reviewing the code

## PR Template

The `.github/PULL_REQUEST_TEMPLATE.md` includes a `regression_evidence` YAML block. Fill it with results from the workflow artifacts:

```yaml
regression_evidence:
  fingerprint_gate: pass
  prompt_regression_gate: skip
  deterministic_dry_run: pass
  ai_dry_run: skip
  namespaces_tested: [applens, cloudarchitect, deploy, compute, fileshares]
  affected_steps: [1]
  baseline_date: 2026-03-28
  notes: "Step 1 only — no AI changes"
```

## Local Verification

Before pushing, you can run a local regression check:

```bash
# Quick deterministic check
./start.sh applens 1
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint -- diff \
  --baseline fingerprint-baseline.json \
  --candidate <(dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint -- snapshot)

# Full prompt regression (requires AI credentials in mcp-tools/.env)
./prompt-regression.sh seed    # first time: create baseline
./prompt-regression.sh test    # after changes: generate candidates
./prompt-regression.sh compare # diff baseline vs candidates
./prompt-regression.sh report  # human-readable report
```

## Related Files

| File | Purpose |
|---|---|
| `.github/workflows/pipeline-output-regression.yml` | The workflow definition |
| `fingerprint-baseline.json` | Structural baseline for fingerprint gate |
| `mcp-tools/DocGeneration.PromptRegression.Tests/Baselines/` | Prompt regression baselines |
| `prompt-regression.sh` | Local prompt regression script |
| `.github/PULL_REQUEST_TEMPLATE.md` | PR template with evidence block |
