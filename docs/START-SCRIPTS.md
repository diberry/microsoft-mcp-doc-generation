# Start Scripts

## Primary Entry Point

```bash
./start.sh [namespace] [steps] [flags]
```

`start.sh` is a thin bash wrapper around the typed .NET orchestrator (`DocGeneration.PipelineRunner`). It handles backward-compatible argument parsing and invokes the runner with the correct output directory.

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
| `--output <path>` | auto | Output directory |
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

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All steps passed |
| 1 | Fatal step failure |
| 2 | Human review required (brand mapping) |
| 64 | Invalid arguments |
