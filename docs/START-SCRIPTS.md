# Start Scripts

## Primary Entry Point

```bash
./start.sh [namespace] [steps] [flags]
```

`start.sh` is a thin bash wrapper around the typed .NET orchestrator (`DocGeneration.PipelineRunner`). It handles backward-compatible argument parsing and invokes the runner with the correct output directory.

## Versioned all-namespace family generation

Use the root `start-with-logs.ps1` script when you need to generate namespace families from repository-tracked, versioned CLI metadata. This specialized PowerShell entry point complements `start.sh`; it doesn't replace the typed primary entry point or provide the general-purpose namespace, step, and flag controls described in this guide.

It supports three namespace selection modes:

- **Full list** (default) — all concrete metadata namespaces from `cli-namespace.json`
- **Comma list** — concrete metadata namespaces or command-family roots via `-NamespaceList "advisor,appservice,compute"`
- **Text file** — one concrete metadata namespace or command-family root per line via `-NamespaceFile ./my-namespaces.txt` (`#` comments and blank lines are ignored)

Explicit selectors can name either a concrete namespace in `cli-namespace.json` or a command-family root discovered from the first token of a command in `cli-output.json`. The default all-namespace mode intentionally dispatches only the concrete namespaces from `cli-namespace.json`; command-family roots are available only when explicitly selected.

Before running the script, ensure:

- PowerShell 7 (`pwsh`) is available.
- `.azure/<environment>/.env` contains `FOUNDRY_ENDPOINT`, `FOUNDRY_MODEL_NAME`, `FOUNDRY_MODEL_API_VERSION`, and `FOUNDRY_USE_DEFAULT_CREDENTIAL=true`.
- `mcp-cli-metadata/tracked-version.txt` names a version with exactly one matching snapshot directory containing `cli-version.json`, `cli-namespace.json`, `cli-output.json`, and `namespace-mapping.json`.

The script resolves the environment named by `defaultEnvironment` in Azure Developer CLI (AZD) configuration. Without a resolvable default, it uses a single unambiguous nested `.env` file. If no nested environment file exists, it falls back to `.azure/.env`. Multiple unresolved nested candidates cause preflight to fail.

Run the script from PowerShell or Git Bash:

```powershell
# All namespaces
pwsh -File ./start-with-logs.ps1

# Specific metadata namespaces or command-family roots
pwsh -File ./start-with-logs.ps1 -NamespaceList "advisor,appservice,compute"

# Selectors from a text file
pwsh -File ./start-with-logs.ps1 -NamespaceFile ./my-namespaces.txt
```

The script:

1. Resolves the version named by `mcp-cli-metadata/tracked-version.txt` and validates its required JSON artifacts, failing before generation if the snapshot is absent, ambiguous, or unusable.
1. Resolves and loads the AZD environment file.
1. Validates the required keyless `FOUNDRY_*` settings.
1. Reads concrete namespace names from the snapshot's `cli-namespace.json` and command-family roots from `cli-output.json`.
1. Uses all concrete namespaces by default. If `-NamespaceList` or `-NamespaceFile` was provided, it validates each explicit selector against the combined concrete-namespace and command-root metadata.
1. Calls `start.sh <selector> 1,2,3,4,5,6` for every selected entry, so the typed pipeline remains the only generation entry point.
1. Reuses the shared build and CLI installation (`--skip-build --skip-npm-update`) only after an earlier namespace **actually built and exited 0** — a confirmed successful build, not loop position. If a namespace built but exited nonzero (for example from a suppressed fatal root), the build stays unconfirmed and the next namespace rebuilds.
1. Writes each namespace to the normal `generated-<namespace>/` directory and streams `start.sh` output to the console.

To validate the selected metadata and resolved environment without creating or changing `generated/`, run:

```powershell
pwsh -File ./start-with-logs.ps1 -PreflightOnly
```

The script stops before generation on invalid metadata, a missing or ambiguous environment file, or invalid keyless settings. It does **not** stop on a namespace-generation failure: it records the failed namespace, prints a warning, **continues with the next namespace**, and exits `1` at the end if any namespace failed (exit `0` otherwise). It doesn't run Step 6 horizontal article generation. AI-backed steps 2-4 still take time for every tool; the wrapper avoids additional per-namespace rebuilds once a shared build has been confirmed successful.

After all namespaces run, the wrapper prints a **six-category run-accounting summary** (see [Run Accounting Summary](#run-accounting-summary)) aggregated from each namespace's `run-accounting.json`.

## Usage Patterns

```bash
# Full catalog (all 52 namespaces, steps 1-6)
./start.sh

# Single namespace
./start.sh advisor                    # → ./generated-advisor/

# Specific steps
./start.sh 1,2,3                      # All namespaces, steps 1-3
./start.sh advisor 1,2                # advisor, steps 1-2

# Skip dependency validation (fast iteration on one step)
./start.sh advisor 4 --skip-deps

# Dry run (print plan, don't execute)
./start.sh advisor --dry-run

# Direct passthrough to PipelineRunner
./start.sh --namespace compute --steps 1,2,3,4 --output ./my-output
```

## Argument Parsing

| Position | Example | Meaning |
|----------|---------|---------|
| 1st arg matches `^[1-6](,[1-6])*$` | `1,2,3` | Steps (all namespaces) |
| 1st arg is a word | `advisor` | Namespace |
| 2nd arg after namespace | `advisor 1,2` | Steps for that namespace |
| Trailing flags | `--skip-deps` | Forwarded to PipelineRunner |
| Leading `-` flag | `--dry-run` | Direct passthrough mode |

## Output Directories

| Mode | Output |
|------|--------|
| All namespaces | `./generated/` |
| Single namespace | `./generated-{namespace}/` |
| Custom `--output` | Specified path |

## PipelineRunner CLI Options

These flags are passed through to the .NET runner:

| Flag | Default | Description |
|------|---------|-------------|
| `--namespace <name>` | all | Process single namespace |
| `--steps <csv>` | `1,2,3,4,5,6` | Comma-separated step IDs |
| `--output <path>` | auto | Output directory. If omitted when calling PipelineRunner directly, defaults to `./generated-<timestamp>` or `./generated-<namespace>-<timestamp>` with `yyyyMMddTHHmmssfffZ` precision |
| `--mcp-branch <branch>` | `release/azure/2.x` | Branch of `microsoft/mcp` for upstream files |
| `--skip-build` | false | Reuse existing Release build |
| `--skip-validation` | false | Skip post-assembly validation |
| `--skip-env-validation` | false | Skip Azure OpenAI env check |
| `--skip-deps` | false | Skip step dependency validation |
| `--dry-run` | false | Print execution plan only |

### Switching MCP Upstream Branch

The `--mcp-branch` flag controls which branch of `microsoft/mcp` is used to fetch upstream documentation files (`azmcp-commands.md` and `e2eTestPrompts.md`). The default is `release/azure/2.x`.

```bash
# Generate docs from 2.x release branch (default)
./start.sh

# Generate docs from main branch (preview/next)
./start.sh --mcp-branch main

# Generate docs from 1.x branch
./start.sh advisor --mcp-branch release/azure/1.x

# Override via environment variable
MCP_BRANCH=main ./start.sh
```

**Resolution order**: CLI flag `--mcp-branch` > environment variable `MCP_BRANCH` > default (`release/azure/2.x`).

If the upstream fetch fails (e.g., network issue), the pipeline falls back to the local copy at `mcp-tools/azure-mcp/azmcp-commands.md` with a warning.

## Parallel Execution (Fan-Out)

After Step 0 (bootstrap) runs once, namespaces can execute in parallel:

```bash
# Run preflight once (builds solution, extracts CLI metadata)
./start.sh advisor 1 --skip-deps   # This triggers bootstrap

# Then fan out (each in background)
./start.sh advisor --skip-build &
./start.sh compute --skip-build &
./start.sh storage --skip-build &
wait

echo "All namespaces complete"
```

Each namespace writes to its own `generated-{namespace}/` directory with no shared mutable state. Use `--skip-build` after the first run to avoid redundant builds.

## Step Reference

| Step | Name | AI? | What It Does |
|------|------|-----|--------------|
| 0 | Bootstrap | No | Build, CLI extraction, brand validation (auto-runs) |
| 1 | Annotations + Parameters | No | Extract tool metadata, parameter tables |
| 2 | Example Prompts | Yes | Generate 5 NL prompts per tool |
| 3 | Tool Composition | Yes | Merge + AI-improve tool descriptions |
| 4 | Tool Family Assembly | Yes | Assemble per-service articles (retries 2x) |
| 5 | Skills Relevance | No | GitHub Copilot skills mapping (non-blocking) |
| 6 | Horizontal Articles | Yes | Overview articles with capabilities, RBAC |

## Common Workflows

**Iterate on AI prompts (Step 2):**
```bash
./start.sh advisor 1,2         # Generate + validate prompts
# Check: generated-advisor/example-prompts/
```

**Regenerate tool-family article (Step 4):**
```bash
./start.sh advisor 4 --skip-deps   # Skip steps 1-3, reuse existing
# Check: generated-advisor/tool-family/advisor.md
```

**Full single-namespace run:**
```bash
./start.sh advisor               # All steps, full validation
# Check: generated-advisor/reports/tool-family-validation-*.txt
```

## Focus Regression Helper

`run-focus.sh` is a thin convenience wrapper for high-friction namespace combinations such as `cosmos`, `storage`, and `monitor-workbooks`.

```bash
./run-focus.sh cosmos
./run-focus.sh storage 3,4 --dry-run
```

Before dispatching any namespace, `run-focus.sh` ensures `azure.mcp@3.0.0-beta.15` is installed and always forwards `--skip-npm-update` to `start.sh`. This keeps focus runs on the expected prerelease tool version and prevents `BootstrapStep` from trying to downgrade to the latest stable package during regeneration.

## Post-Assembly Merge (AD-011)

After all namespaces complete, `start.sh` automatically calls `merge-namespaces.sh` to combine multi-namespace tool-family articles. This is config-driven via `brand-to-server-mapping.json` merge fields.

```bash
# Automatic: runs after successful pipeline completion
./start.sh                        # All namespaces → merge runs at end

# Manual: run merge independently
./merge-namespaces.sh             # Merge all configured groups
./merge-namespaces.sh --dry-run   # Preview what would be merged
```

**Currently configured merge groups:**

| Group | Primary | Secondary | Result |
|-------|---------|-----------|--------|
| `azure-monitor` | monitor (15 tools) | workbooks (5 tools) | `monitor.md` (20 tools) |

Namespaces without `mergeGroup` config are standalone — the merge step is a no-op for them.

## Utility Scripts

### Tool counter (`scripts/count-tools.ps1`)

Audits a `tools-list.json` (Azure MCP CLI metadata) by counting tools and grouping them by service/namespace. The service is the first token of each tool's `command` (e.g. `acr registry list` → `acr`). Use it to compare tool counts across metadata versions and validate coverage in documentation PRs.

```powershell
# Top 10 services by tool count (default)
./scripts/count-tools.ps1 -FilePath ./mcp-cli-metadata/3.0.0-beta.6/tools-list.json

# Show every service
./scripts/count-tools.ps1 -FilePath ./tools-list.json -Top 0
```

Output: total tool count, a per-service breakdown table (descending by count), and the structured `{ Total, ByService }` object emitted to the pipeline for programmatic use. It accepts both the results-wrapped shape (`{ "results": [ … ] }`) and a bare top-level array, and errors clearly on a missing or unrecognized file.

## Run Accounting Summary

Since #813 Step 2, every pipeline run writes a machine-readable `run-accounting.json` at the root
of its output directory and prints a six-category summary. When a selected `Fatal` step does **not
cleanly succeed** — a nonzero exit, **or** a recorded **blocking** artifact failure even when the step
reports `success` and maps to exit `0` — the runner suppresses that step's selected downstream
dependents (following the full transitive dependency graph, so suppression propagates through
unselected intermediate steps), keeps running the independent steps and later namespaces, and reports
the outcome across these categories. (Since AD-044, an exhausted-retries Step 2 required-parameter/
content-validation failure is recorded as **non-blocking**: it is still surfaced as a warning, but does
not by itself make Step 2 a root-failed namespace.)

| # | Category | Meaning |
|---|----------|---------|
| 1 | Successful namespaces | All selected steps succeeded (or warn-failed) with zero fatal roots. |
| 2 | Root-failed namespaces | A selected `Fatal` step did not cleanly succeed (nonzero exit **or** recorded **blocking** artifact failures); named with its stable `rootFailureId`. |
| 3 | Warning-only failures | A selected `Warn` step (for example Step 5) that did not succeed. Never suppresses anything. |
| 4 | Suppressed steps | Downstream dependents skipped because a fatal root blocked them; each linked to its `rootFailureId`. |
| 5 | Cascades imported from historical fixtures | Constant, read once from the frozen beta.34 baseline manifest. |
| 6 | Unclassified records | Constant, read once from the frozen beta.34 baseline manifest. |

`start-with-logs.ps1` aggregates every namespace's `run-accounting.json` into one catalog-level
summary: live categories 1–4 are **summed** across namespaces, while catalog-constant categories
5–6 are taken **once** (never summed). A missing or malformed `run-accounting.json` is skipped with
a warning and can never mask a failure — the per-namespace exit-code list remains the authoritative
status signal. See [ARCHITECTURE.md → Runtime Dependency Suppression](ARCHITECTURE.md#runtime-dependency-suppression-ad-029)
for the full contract and the `run-accounting.json` schema.

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All steps passed |
| 1 | Fatal step failure |
| 2 | Human review required (brand mapping) |
| 64 | Invalid arguments |

A single namespace's fatal step no longer stops the run. `start.sh` (one namespace) exits with that
namespace's worst code; `start-with-logs.ps1` (catalog) continues past a failed namespace and exits
`1` if any namespace failed. A hard fatal (`1`) dominates human-review (`2`).

## Live Azure OpenAI Endpoint Probe

Configuration presence (`FOUNDRY_*` settings) does not prove the endpoint is reachable. Right after
Bootstrap confirms configuration is present, the pipeline makes one live Azure OpenAI call to prove
the endpoint actually works, before Steps 2–6 run.

- **Non-interactive / redirected-input runs** (including `start-with-logs.ps1`, which pipes namespace
  runs non-interactively) **fail immediately with a nonzero exit** on probe failure — no prompt.
- **Interactive `start.sh <namespace>` runs** are prompted to continue on probe failure. Declining
  fails the same way. Confirming Continue persists a loud critical-failure record, disables all
  further Azure OpenAI calls for the rest of that run, and proceeds with deterministic/verbatim work
  only — every AI-required artifact is marked incomplete, never reported as fully successful.

See [ARCHITECTURE.md → Live AI Endpoint Probe & `partial_explicit` Offline Continuation (AD-042)](ARCHITECTURE.md#live-ai-endpoint-probe--partial_explicit-offline-continuation-ad-042)
for the full design and the observed-vs-designed AI behavior table for Steps 1–6.
