# P0: Deterministic prompt repair pass to fix Step 2 example prompt validation failures

**Priority**: P0 — next to work on  
**Status**: Open  
**Created**: 2026-07-31  
**GitHub**: https://github.com/diberry/microsoft-mcp-doc-generation/issues/781  
**Latest run**: `generated-20260731T072405/` — **42/65 namespaces failed (170 critical failures)**

## Failure Breakdown (170 total)

| Category | Count | % | Root cause |
|----------|-------|---|------------|
| Step 2: Missing required params | 129 | 76% | AI prompts don't include param names the validator expects |
| Step 4: Cascade from Step 2 | 38 | 22% | Validator re-checks same missing params at article assembly |
| Step 0: Bootstrap failures | 2 | 1% | `foundry` (no tools found), `subscription` (CLI extraction failed) |
| Step 5: Skills relevance | 1 | <1% | `get_azure_bestpractices` missing GITHUB_TOKEN |

**167 of 170 failures (98%) are fixed by solving Step 2.** Step 4 failures are pure cascades.

### Most frequently missing parameters (Step 2)

| Missing param | Count | Example namespaces |
|---------------|-------|--------------------|
| `resource-group` | 25 | storage, servicefabric, fileshares, managedlustre |
| `eventhub` | 5 | eventhubs |
| `account` | 4 | appconfig, storage |
| `message` | 4 | communication |
| `snapshot-name` | 3 | sreagent |
| `resource-type` | 3 | bicepschema |
| `location` | 3 | redis, storage |
| `query` | 2 | search, kusto |
| `namespace` | 2 | servicebus |

## Root Cause

Pipeline design is **determinism first, AI second** (ARCHITECTURE.md). Step 1 extracts all parameter names and required status from CLI metadata. Step 2 passes this to the LLM, but the output is not deterministically verified and repaired before validation.

The required parameter names are **known facts** from Step 1 — we should not rely on AI compliance.

## Proposed Fix (v4 — all review items resolved)

**Deterministic prompt repair pass** in Step 2, between AI generation and validation.

### Architecture

**Call site: Option A** — inside the generator subprocess (`DocGeneration.Steps.ExamplePrompts.Generation`), in-memory after AI response parsing, before sanitization and rendering.

**Subprocess boundary diagram:**

```
┌─────────────────────────────────────────────────────────┐
│  Generator subprocess (ExamplePromptGenerator)           │
│                                                         │
│  Azure OpenAI call → parse JSON → [5 prompt strings]    │
│       │                                                 │
│       ▼                                                 │
│  DeterministicPromptRepairer.Repair()                   │
│       │  (in-memory, structured data)                   │
│       ▼                                                 │
│  CredentialSanitizer.Sanitize()                         │
│       │                                                 │
│       ▼                                                 │
│  Render to .md files → write to disk                    │
│       │                                                 │
│  Write repair-telemetry.json → disk                     │
└────────┼────────────────────────────────────────────────┘
         │ (subprocess exits)
         ▼
┌─────────────────────────────────────────────────────────┐
│  PipelineRunner (ExamplePromptsStep.cs)                 │
│                                                         │
│  Read exit code → launch Validator subprocess           │
└────────┼────────────────────────────────────────────────┘
         ▼
┌─────────────────────────────────────────────────────────┐
│  Validator subprocess (CodeBasedPromptValidator)         │
│                                                         │
│  Read .md files from disk → validate → report           │
└─────────────────────────────────────────────────────────┘
```

### Class Design

**Uses existing model**: `ExamplePromptGeneratorStandalone.Models.Option` (has `Name`, `Required`, `Description`).

```csharp
// In ExamplePromptGeneratorStandalone.Generators namespace
public sealed class DeterministicPromptRepairer
{
    /// <summary>
    /// Repairs prompts to ensure parameter coverage. Returns diagnostics.
    /// Post-repair verification: re-runs GetConcretePromptCoverage on each
    /// required param; logs warning if any remain uncovered after repair.
    /// </summary>
    public RepairResult Repair(
        IReadOnlyList<string> prompts,
        IReadOnlyList<Option> requiredParameters);
}

public sealed record RepairResult(
    IReadOnlyList<string> RepairedPrompts,
    IReadOnlyList<RepairAction> Actions,
    IReadOnlyList<string> StillUncovered);

public sealed record RepairAction(
    string ParameterName,
    int PromptIndex,
    string InjectedText);
```

### Key Design Decisions

1. **Repair-safe `ParameterValueBank`** — extracted from `private static ValueBank` in `DeterministicExamplePromptGenerator.cs:23` into `internal static class ParameterValueBank` in the same project. Existing `InternalsVisibleTo` attribute already covers the test project. Existing `ValueBank.TryGetValue` calls in `DeterministicExamplePromptGenerator` redirected to `ParameterValueBank`.

   **Credential exclusion**: The `value` key's entries (`P@ssw0rd!2026`, `sk_live_*`, JWT-like strings) are credential-shaped and will be replaced by `CredentialSanitizer` with placeholders. The repair bank **excludes** the `value` key entirely and provides a separate safe entry: `["value"] = ["config-setting-1", "feature-flag-value", "application-data", "setting-value-01", "parameter-value"]`. All other ValueBank entries are safe (resource names, locations, queries).

2. **Value resolution precedence** (no type metadata on `Option`):
   1. **Enum from description** → if `ParameterCoverageChecker.ParseAllowedValues(option.Description)` returns values, use the first value (already exists in shared lib, line 290)
   2. **ValueBank lookup** → if `ParameterValueBank` has the param name, use first entry
   3. **Name heuristic** → `-id`/`Id` suffix → GUID `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"`, `-endpoint`/`-url`/`-uri` → `"https://my-service.azure.net"`, `-date`/`-time` → `"2026-01-15"`
   4. **Fallback** → `"my-{slug}"` (e.g., `my-eventhub`)

3. **Round-robin param distribution** (fully specified):
   - Sort missing params by declaration order (preserves CLI source order)
   - Start at prompt index 0
   - Skip blank/empty prompts (they get no injection)
   - Cycle through non-blank prompts: param[0]→prompt[0], param[1]→prompt[1], ..., param[5]→prompt[0]
   - **No hard clause limit** — if a tool has 9 required params and 5 non-blank prompts, distribute ~2 per prompt; excess params assigned to prompts with fewest clauses. All missing params MUST be assigned; none go to `StillUncovered` due to capacity. `StillUncovered` is reserved for params the checker still can't match post-injection (unexpected checker behavior).
   - If all prompts are blank → return unchanged + all params in `StillUncovered`

4. **Injection grammar**: Append ` for {natural-name} '{value}'` before final punctuation. If prompt has no final punctuation, append with period: `. Specify {natural-name} '{value}'.`

5. **Post-repair verification**: After mutation, re-run `ParameterCoverageChecker.GetConcretePromptCoverage()` for each required param with **exact checker arguments**:
   ```csharp
   GetConcretePromptCoverage(
       repairedPrompts,
       option.Name!,
       requiredParameters.Count,    // totalRequiredParameters
       option.Description)          // enables enum matching
   ```
   Any still-uncovered params go into `RepairResult.StillUncovered` and are logged as warnings. The repairer is advisory — the validator subprocess is the enforcement boundary.

6. **Empty-input behavior**: If `prompts` is empty or all entries are blank, return unchanged prompts + all required param names in `StillUncovered` + zero `Actions`. Never crash.

### Retry Loop

- **Keep retry loop** for non-coverage failures (prompt count, malformed output, format errors)
- **Repair runs on every invocation**, including retries — ensures retried prompts also get repaired
- Post-repair, retries should rarely trigger for coverage reasons, but remain for other validation failures

### Telemetry

**Artifact**: `{outputPath}/repair-telemetry/{safe-tool-name}-repair.json` per tool. Filename uses `ToolFileNameBuilder` for collision-safe naming (same as other pipeline artifacts).

**Lifecycle**:
- **Cleanup**: Generator cleans `repair-telemetry/` directory **only on initial batch invocation** (when `--tool-command` is NOT set). When `--tool-command` is supplied (retry for a single tool), the directory is NOT cleaned — only that tool's file is overwritten.
- **Retry scoping**: Single-tool retry overwrites only the target tool's `.json` file. Other tools' telemetry is preserved intact.
- **Denominator**: "Repaired N/M tools" where M = number of tools that went through AI generation (excludes deterministic-only tools that skip AI)
- **Aggregation timing**: PipelineRunner reads `repair-telemetry/*.json` only AFTER all retries complete (the final state of each file reflects the final attempt for that tool)

```json
{
  "tool": "appconfig kv delete",
  "totalRequiredParams": 3,
  "paramsRepaired": 1,
  "actions": [
    { "param": "account", "promptIndex": 2, "injected": "for account 'mystorageacct'" }
  ],
  "stillUncovered": [],
  "timestamp": "2026-07-31T20:15:00Z"
}
```

**Aggregation**: PipelineRunner reads `repair-telemetry/*.json` after all retries complete and reports:
- Total tools needing repair / total AI-generated tools
- Total params repaired
- Emitted as step warnings visible in console output

### Guarantee

Uses the **same** `ParameterCoverageChecker` the validator uses — repaired prompts are **guaranteed to satisfy parameter coverage** when post-repair verification succeeds. Does NOT guarantee all validation passes (validator also checks prompt count, format, credential presence, etc.).

## Validation Plan

### Unit tests (proof of correctness)

The `DeterministicPromptRepairer` is deterministic. All 80 failure patterns can be validated with unit tests.

**Required test categories** (20):

| # | Category | Example param | Acceptance criteria |
|---|----------|--------------|---------------------|
| 1 | Single common param | `resource-group` | Injects "resource group 'rg-prod'" into one prompt |
| 2 | Single short param | `account` | Injects "account 'mystorageacct'" |
| 3 | Single short param | `query` | Injects "query 'Heartbeat \| take 10'" |
| 4 | Compound param with suffix | `module-name` | Checker strips `-name` → coverage passes |
| 5 | Compound param | `cli-type` | Injects "cli type 'my-cli-type'" |
| 6 | Multi-param (2) | `server-id, resource-group` | Both injected into different prompts (round-robin) |
| 7 | Multi-param (4) | `from, to, message, endpoint` | All 4 distributed across prompts |
| 8 | Multi-param (9) | 9 required params, 5 prompts | All distributed (~2/prompt), none in StillUncovered |
| 9 | Param already present | `resource-group` in text | No-op, prompt unchanged, zero Actions |
| 10 | All params present | — | No-op, zero Actions, empty StillUncovered |
| 11 | All prompts blank | `resource-group` | Returns unchanged, StillUncovered=['resource-group'] |
| 12 | Fewer than 5 prompts (3) | `account` | Repairs within available prompts |
| 13 | Enum param with description | `resource-type` ("Available options: 'keyvault_vaults'...") | Uses first value from `ParseAllowedValues()` |
| 14 | Name-heuristic typed value | `endpoint` (URI) | Injects valid URI format |
| 15 | Idempotence | any | `Repair(Repair(prompts))` == `Repair(prompts)` |
| 16 | Special chars in value | `query` with pipes | Properly quoted in injected text |
| 17 | Credential-safe `value` param | `value` (required) | Injects `'config-setting-1'`, NOT `'P@ssw0rd!'` |
| 18 | Checker arg forwarding | any 3 required | Verifies `totalRequiredParameters` and `description` passed correctly |
| 19 | Rendered content survives sanitizer | `account` | After repair + `CredentialSanitizer.Sanitize()`, value is concrete (not placeholder) |
| 20a | Telemetry write (Generation) | multiple tools | Per-tool JSON written with correct schema after repair |
| 20b | Telemetry cleanup respects retry | batch + retry | Batch clears dir; retry with `--tool-command` preserves other files |
| 20c | Telemetry aggregation (PipelineRunner) | 3 tool files | Reads files, computes correct N/M, emits warning |

### Integration tests (simulated chain)

Exercises the full in-process path (not E2E subprocess — that's pipeline confirmation):
1. **Repair → `CredentialSanitizer` → render → parse → `ParameterCoverageChecker`** — proves repair survives downstream transforms *(in `DocGeneration.Steps.ExamplePrompts.Generation.Tests`)*
2. **Retry preservation**: Generate telemetry for 3 tools; invoke retry for 1 tool → assert other 2 files preserved *(in `DocGeneration.Steps.ExamplePrompts.Generation.Tests`)*
3. **Corpus test**: Load all required params from a real `cli-output.json` → generate minimal prompts → repair → assert all covered *(in `DocGeneration.Steps.ExamplePrompts.Generation.Tests`)*
4. **Telemetry aggregation**: Given 3 `.json` files on disk, PipelineRunner helper reads them and computes correct N/M *(in `DocGeneration.PipelineRunner.Tests`)*

### Pipeline validation (E2E confirmation, not proof)

After unit + integration tests pass:
1. `dotnet test mcp-doc-generation.sln` — all tests pass (existing + new)
2. `./start.sh appconfig 2` — quick spot-check (4 tools, was 4 failures)
3. `./start.sh azureterraform 2` — larger spot-check (was 9 failures)

## Squad Review Log

### Review 1 (2026-07-31) — CHANGES REQUESTED → resolved in v2

| # | Item | Reviewer | Status |
|---|------|----------|--------|
| 1 | Use Option A (in-memory, inside generator subprocess) | Riley | ✅ |
| 2 | Exact ordering: AI parse → repair → sanitize → render → validate | Riley | ✅ |
| 3 | Richer return type with diagnostics | Morgan | ✅ |
| 4 | Reuse existing ValueBank for typed example values | Morgan | ✅ |
| 5 | Distribute params across prompts (round-robin) | Morgan | ✅ |
| 6 | Add missing test categories | Cameron | ✅ |
| 7 | Integration test for full chain | Cameron | ✅ |
| 8 | Add repair telemetry | Riley | ✅ |
| 9 | Keep retry loop for non-coverage failures | Riley | ✅ |
| 10 | Soften guarantee | Cameron | ✅ |

### Review 2 (2026-07-31) — CHANGES REQUESTED → resolved in v3

| # | Item | Reviewer | Resolution |
|---|------|----------|------------|
| 1 | `CliParam` doesn't exist — use `Option` | Morgan | ✅ Uses `ExamplePromptGeneratorStandalone.Models.Option` (Name, Required, Description) |
| 2 | `ValueBank` is private — needs extraction | Morgan | ✅ Extract to `internal static class ParameterValueBank` + InternalsVisibleTo |
| 3 | No type metadata — can't generate typed values | Morgan | ✅ Name-based heuristics: `-id`→GUID, `-endpoint`→URI, `-date`→ISO date, else ValueBank/fallback |
| 4 | Empty-input behavior undefined | Cameron | ✅ Return unchanged + StillUncovered list, never crash |
| 5 | Round-robin underspecified | Cameron | ✅ Fully specified: declaration order, index 0 start, skip blanks, no hard limit, fewest-clauses assignment |
| 6 | Architecture diagram missing subprocess boundary | Riley | ✅ Added subprocess boundary diagram with disk I/O marked |
| 7 | Telemetry artifact format undefined | Riley | ✅ `repair-telemetry/{tool}-repair.json` + PipelineRunner aggregation |

### Review 3 (2026-07-31) — CHANGES REQUESTED → resolved in v4

| # | Item | Reviewer | Resolution |
|---|------|----------|------------|
| 1 | Credential-shaped ValueBank values get sanitized | Morgan | ✅ `value` key excluded; repair bank uses safe values (`config-setting-1`, etc.) |
| 2 | Capacity exhaustion (9 params, 5 prompts) | Cameron | ✅ No hard clause limit — all missing params assigned, distributed evenly |
| 3 | Checker args must include count + description | Morgan | ✅ Exact call signature specified with `requiredParameters.Count` and `option.Description` |
| 4 | Enum parsing algorithm missing | Morgan | ✅ Uses existing `ParameterCoverageChecker.ParseAllowedValues()` (line 290), first value used |
| 5 | Telemetry lifecycle (cleanup, retry, denominator) | Riley | ✅ Dir cleaned at start, retry overwrites, M = AI-generated tools, `ToolFileNameBuilder` names |
| 6 | Tests must assert rendered content not just coverage | Cameron | ✅ Test #19: repair + sanitizer → assert concrete value survives |

### Review 4 (2026-07-31) — CHANGES REQUESTED → resolved in v4 (updated)

| # | Item | Reviewer | Resolution |
|---|------|----------|------------|
| 1 | Telemetry cleanup destroys other tools on retry | Riley | ✅ Clean only on batch (`--tool-command` absent); retry overwrites single file |
| 2 | Test #20 crosses project boundaries | Cameron | ✅ Split: #20a (write) + #20b (retry preservation) in Generation.Tests; #20c (aggregation) in PipelineRunner.Tests |

## Other Failures (separate from P0)

| Namespace | Step | Issue | Fix |
|-----------|------|-------|-----|
| `foundry` | 0 | "No tools found matching 'foundry'" | Namespace mapping config |
| `subscription` | 0 | CLI metadata extraction failed (exit code 1) | Investigate CLI extraction |
| `extension_azqr` | 4 | tool_count mismatch (1 vs 3) | Namespace mapping or merge group config |
| `virtualdesktop` | 4 | Phantom parameter `host-pool-resource-id` | Upstream CLI renamed this parameter |
| `get_azure_bestpractices` | 5 | Skills relevance output missing | Needs GITHUB_TOKEN or exemption |
