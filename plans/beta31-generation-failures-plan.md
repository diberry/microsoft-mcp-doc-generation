# Beta.31 Generation Failures — Investigation & Fix Plan

**Date**: 2026-08-02  
**Run**: `generated-20260801T112536`  
**Result**: 45/65 succeeded, 20 failed  
**CLI Version**: 3.0.0-beta.31+2aa161acf58c99752bc9f53dff086b1dba3bd5e9

---
## Dina Questions and answers

The answers should result in changes and additions to the architecture document and remember the architecture document needs to be in chuncks so that you can read and find what you need. 

## Finding 1: Transient Step 0 Bootstrap Timeouts (acr, applens)

**Symptom**: `CLI metadata extraction failed (exit code 1)` — `The operation was canceled` in `ProcessRunner.RunAsync`  
**Affected**: acr (1/65), applens (5/65)  
**Root Cause**: The first namespace run installs `azure.mcp@3.0.0-beta.31` via dotnet tool. The `azmcp server tools-list` command timed out during this initial cold-start. Subsequent namespaces skip install (`--skip-npm-update`) and succeed.

### Resolution Options

| Option | Description | Effort |
|--------|-------------|--------|
| A (Recommended) | Increase `ProcessRunner` timeout from default to 120s for first run | Low |
| B | Add retry logic in `BootstrapStep` when CLI extraction fails | Medium |
| C | Accept as transient — rerun failed namespaces | None |

### Verification
- Rerun just `acr` and `applens` after the first namespace succeeds — they should pass.
- `pwsh ./start-with-logs.ps1 -NamespaceList "acr,applens"`

### Files to Change
- `mcp-tools/McpCliMetadata/AzmcpRunner.cs` — increase `CancellationTokenSource` timeout
- OR: `mcp-tools/DocGeneration.PipelineRunner/Steps/Bootstrap/BootstrapStep.cs` — add retry on CLI extraction failure

### Tests
- Unit test: `AzmcpRunnerTests` — verify timeout is configurable and retries on cancellation

### Dina Questions and answers

Is this a cold start problem? Why not just add a loop that tries 3 times for the cold start and if that cold start loop fails, fail the whole process with plenty of informaiton of why it failed?

### Resolution (Updated per Dina feedback)

**Option B (Retry loop)** — Add a 3-attempt retry loop in `BootstrapStep` for CLI metadata extraction. On all 3 failures, fail the entire pipeline with a clear error message explaining the cold-start timeout and what to check.

### Files to Change (Updated)
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Bootstrap/BootstrapStep.cs` — add 3-attempt retry loop around CLI extraction with descriptive failure message
- Remove Option A (just increasing timeout) and Option C (accept as transient) from consideration

### Tests (Updated)
- Unit test: `BootstrapStepTests` — verify 3 retries attempted on CLI timeout
- Unit test: verify clear failure message after 3 exhausted retries
- Unit test: verify success on retry 2 or 3 does not fail the pipeline

### Squad follow-up questions for Dina

1. **Retry delay**: Should there be a delay between retries? (e.g., 5s, 10s, 15s exponential backoff, or fixed?) DINA'S answer: exponential backoff
2. **Scope**: Should this retry logic apply only to the very first namespace (cold-start install), or to ALL namespace CLI extractions?  DINA'S answer: always 
3. **Architecture doc update**: You mentioned architecture doc changes — should the retry behavior be documented in `docs/ARCHITECTURE.md` under BootstrapStep, or is there a better location? add it to bootstrapstep and its own retry section

---

## Finding 2: Namespace `foundry` Removed in Beta.31

**Symptom**: `No tools found matching 'foundry'` in Step 0  
**Affected**: foundry (9/65)  
**Root Cause**: The `foundry` namespace existed in beta.30 but is **gone in beta.31** — only `foundryextensions` remains. The pipeline still tries to generate it because `brand-to-server-mapping.json` (line 206) still lists `"mcpServerName": "foundry"`.

### Evidence
- `cli-namespace.json` (beta.31): Only `foundryextensions` exists, no `foundry`
- `namespace-mapping.json` (beta.31): No `foundry` entry
- `brand-to-server-mapping.json`: Still has `foundry` at line 206

### Resolution
**Remove or disable** the `foundry` entry in `brand-to-server-mapping.json`. Two options:

| Option | Description |
|--------|-------------|
| A (Recommended) | Remove the `foundry` entry entirely from `brand-to-server-mapping.json` |
| B | Add a `"disabled": true` flag (requires pipeline code to honor it) |

### Verification
- After removing, run: `pwsh ./start-with-logs.ps1 -NamespaceList "foundryextensions"` — should pass
- Confirm `start-with-logs.ps1` no longer lists `foundry` in its namespace list (should drop to 64)

### Files to Change
- `mcp-tools/data/brand-to-server-mapping.json` — remove `foundry` entry (lines ~205-211)

### Tests
- Existing `MergeGroupValidator` and brand mapping tests should still pass after removal
- `dotnet test mcp-doc-generation.sln`

### Dina Questions and answers

Foundry changed names from foundry to foundryextension some many versions ago. You need a way to remember this and look at the version proceesing and the cli json to know that moving forward, foundryextension is the correct extention. 

### Resolution (Updated per Dina feedback)

**Option A + namespace drift detection.** Remove the stale `foundry` entry from `brand-to-server-mapping.json` AND add an automated check that compares `brand-to-server-mapping.json` entries against the actual CLI namespace list (`cli-namespace.json`) during Step 0 Bootstrap. Any mapping entry whose `mcpServerName` is not found in the live CLI output should be flagged as a warning (or error) with a clear message: `"Namespace 'foundry' exists in brand-to-server-mapping.json but was not found in CLI beta.31. It may have been renamed or removed."`

This prevents the same class of problem from recurring silently when Microsoft renames or removes namespaces in future beta versions.

### Files to Change (Updated)
- `mcp-tools/data/brand-to-server-mapping.json` — remove `foundry` entry
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Bootstrap/BootstrapStep.cs` (or a new validator) — add namespace drift detection comparing mapping config vs live CLI namespaces

### Tests (Updated)
- Unit test: drift detector flags a mapping entry not found in CLI namespace list
- Unit test: drift detector passes when all mapping entries match CLI namespaces
- Existing `MergeGroupValidator` and brand mapping tests should still pass after `foundry` removal

### Squad follow-up questions for Dina

1. **Severity**: Should a namespace drift mismatch be a **warning** (log and continue, skip that namespace) or a **hard error** (fail the pipeline)? Warnings let the rest of the generation proceed; errors force the config to be updated before any generation runs. DINA'S answer: it isn't really drift - it is planned in issues and PRs on Microsoft/MCP - there is supposed to a file that catalogs these changes and include namespace joining so the final family file in C:\my-squad-projects\microsoft-mcp-doc-generation\merge-namespaces.sh and C:\my-squad-projects\microsoft-mcp-doc-generation\config\namespace-mapping.json
2. **Auto-disable vs manual removal**: When drift is detected, should the pipeline automatically skip the missing namespace (like an implicit `"disabled": true`), or should it require a human to manually remove/update the entry?   DINA'S answer: if it isn't covered in these files then error and make it very clear in run summary that DINA/HUMAN needs to determine what is happening
1. **Architecture doc**: Should namespace drift detection be documented as part of the Step 0 Bootstrap section in `docs/ARCHITECTURE.md`? The files that document the config should be linked in README.md and ARCHITECTURE.md

---

## Finding 3: Example Prompt Parameter Validation False Positives (14 namespaces)

**Symptom**: Step 2/4 fails with "missing 'X' in example prompts" for params that may be optional  
**Affected**: appconfig, azurebackup, azureterraform, compute, datadog, eventhubs, foundryextensions, group, loadtesting, mysql, postgres, search, sreagent, storage, storagesync (14 namespaces, ~16 distinct tools)

**User Clarification**: "resource-group is only in param table if it is required"

**Root Cause Hypothesis**: The example prompt validator (`ExamplePromptValidator` or Step 4 post-assembly checker) requires ALL non-common parameters to appear in example prompts, but some of these params are **optional**. The validator should only enforce **required** parameters in prompts.

### Specific Failures

| Namespace | Tool | Missing Param | Likely Optional? |
|-----------|------|---------------|-----------------|
| group | resource-list | resource-group | Investigate |
| appconfig | kv-get | account | Investigate |
| azurebackup | governance-soft-delete | soft-delete | Investigate |
| azureterraform | aztfexport-query | query | Investigate |
| compute | vmss-delete | vmss name | Investigate |
| datadog | monitoredresources-list | datadog-resource | Investigate |
| eventhubs | eventhub-consumergroup-delete | eventhub | Investigate |
| foundryextensions | openai-chat-completions-create | deployment | Investigate |
| loadtesting | testrun-createorupdate | testrun-id | Investigate |
| mysql | server-param-get | Parameter | Investigate |
| postgres | server-param-get | Parameter | Investigate |
| search | knowledge-base-retrieve | knowledge-base | Investigate |
| sreagent | docs-memories-add | agent | Investigate |
| storage | account-create | account | Investigate |
| storagesync | cloudendpoint-changedetection | directory-path | Investigate |

### Investigation Steps
1. For each tool above, check CLI JSON to determine if the flagged param is `required: true` or `required: false`
2. Identify where in the pipeline the "must appear in example prompts" check lives
3. Determine if the validator distinguishes required vs optional params

### Resolution
- **If validator doesn't check required flag**: Fix the validator to only enforce required params in example prompts
- **If params ARE required**: The AI prompt generation (Step 2) needs better instructions to always include required params

### Files to Investigate
- `mcp-tools/DocGeneration.PipelineRunner/Validation/` — look for example prompt param validation logic
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/ExamplePromptsStep.cs` — Step 2 validation
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/ToolFamilyCleanupStep.cs` — Step 4 check

### Files to Change (pending investigation)
- Validator code — add `required` flag check before failing on missing params
- OR: AI system prompt — strengthen instruction to include all required params

### Tests
- Unit test: validator with optional param missing → should NOT fail
- Unit test: validator with required param missing → SHOULD fail
- E2E: rerun all 14 namespaces after fix

### Dina Questions and answers

There are 3 types of parameters - this needs to be clear and complete in the architecture document. 
1) Global params aren't in the param tables for each tool because they are catalogued else where. 
2) Resource group is special in that is it only in the tool paramter table if it is required. It is the only parmaeter of this catagory.
3) Tool specific parameters are always listed in the tool parameter tables. Because the CLI tab of the tool is generated first. All tool parameters unique to that tool should be found. You shouldn't lose or drop any. Perhaps a better approproach is to always have the global and resource params, then I can manually strip them away when I create the content prs. This would allow a more consistent generation for both the CLI tab and the NLP tab. 

### Resolution (Updated per Dina feedback)

**Two-part fix:**

**Part A — Architecture doc update:** Document the 3-tier parameter taxonomy clearly in `docs/ARCHITECTURE.md`:
1. **Global parameters** (`--tenant`, `--auth-method`, `--retry-*`): Never in per-tool param tables; documented once in a shared location.
2. **Resource-group** (singular special case): Only appears in a tool's param table when it is **required** for that tool. This is the only parameter with this conditional behavior.
3. **Tool-specific parameters**: Always listed in the tool's param table. These must never be lost or dropped during generation.

**Part B — Generation strategy change (pending Dina decision):** Dina is considering changing the approach to **always include global and resource-group params** in generated output, then manually strip them during content PR creation. This would make generation more consistent between CLI tab and NLP tab.

### Impact on the Validation Bug

The current validator failure ("missing param in example prompts") is a separate issue from the parameter taxonomy. The validator needs to understand which params are **required** vs **optional** before flagging missing params in example prompts. The fix:
- Validator should check the CLI JSON `required` flag for each parameter
- Only flag a missing param as an error if it is `required: true`
- Optional params missing from example prompts should be a warning at most

### Files to Change (Updated)
- `docs/ARCHITECTURE.md` — add 3-tier parameter taxonomy section
- Validator code (location TBD from investigation) — add `required` flag check
- AI system prompt for example prompts — strengthen instruction to always include required params

### Tests (Updated)
- Unit test: validator with optional param missing from example prompts → warning, not error
- Unit test: validator with required param missing from example prompts → error
- Unit test: resource-group missing from example prompts when it IS required → error
- Unit test: resource-group missing from example prompts when it is NOT required → no error

### Squad follow-up questions for Dina

1. **Generation strategy decision**: Do you want to proceed with the "always include all params, strip manually later" approach? This is a significant change to the current filtering logic in `ParameterGenerator.cs`, `PageGenerator.cs`, and `DocumentationGenerator.cs`. If yes, it affects Finding 3's fix scope substantially — we'd remove the common-parameter filtering entirely from generation and the validator would check all params.

 DINA'S answer: Go with this directory - yes

1. **Resource-group validation**: You said resource-group is the ONLY parameter in the "conditional" category. Can you confirm there are no other params that behave this way (e.g., `--subscription` which the copilot instructions say "filtered when optional, kept when required")?  DINA'S answer: no other parameters but is we include all I strip away it doesn't matter - just include all
1. **NLP tab consistency**: You mentioned the CLI tab is generated first and the NLP tab should be consistent. Is the current NLP tab generation dropping params that the CLI tab has? Or is this about example prompts specifically (Step 2)?  DINA'S answer: you need to be able to answer this on your own
1. **Architecture doc structure**: You said the architecture doc "needs to be in chunks so that you can read and find what you need." Is the current `docs/ARCHITECTURE.md` too monolithic? Should we split it into multiple files (e.g., `docs/architecture/parameters.md`, `docs/architecture/pipeline.md`)?  DINA'S answer: you make the determination

---

## Finding 4: Source CLI JSON Mismatches (monitor, virtualdesktop)

**Symptom**: Two different Step 4 validation failures related to CLI JSON source-of-truth checks  
**Affected**: monitor (42/65), virtualdesktop (63/65)

### 4a. monitor — Missing required params in article

**Error**: `Source CLI JSON check failed for 'monitor resource log query': required source parameter(s) missing from article: 'resource-id', 'table'`

**Root Cause Hypothesis**: The tool `monitor resource log query` has `resource-id` and `table` as required params in CLI JSON, but they're not appearing in the generated article's parameter table. This could be:
- A Step 1 generation issue (params not extracted correctly)
- A template issue (params filtered incorrectly)
- A new tool in beta.31 that hasn't been generated before

### 4b. virtualdesktop — Extra param in article not in CLI JSON

**Error**: `Source CLI JSON check failed for 'virtualdesktop hostpool host list': parameter(s) documented but not present in source CLI JSON: 'host-pool-resource-id'`

**Root Cause Hypothesis**: The article documents `host-pool-resource-id` but this param doesn't exist in beta.31's CLI JSON. Possible causes:
- Param was renamed between beta.30 and beta.31
- Param was removed in beta.31
- Stale generated content from a previous run being carried forward

### Investigation Steps
1. Compare `monitor resource log query` params between beta.30 and beta.31 CLI JSON
2. Compare `virtualdesktop hostpool host list` params between beta.30 and beta.31 CLI JSON
3. If params were renamed/removed, the pipeline should regenerate cleanly on a fresh run (no stale files)

### Resolution
- **If beta.31 changed param names**: Fresh generation (no stale output) should resolve — the validator is correctly catching the mismatch
- **If params are truly missing from Step 1 output**: Fix extraction logic in `AnnotationsParametersRawStep`
- **If stale content**: Ensure `start-with-logs.ps1` cleans output before regenerating

### Verification
- Run fresh (no prior output): `pwsh ./start-with-logs.ps1 -NamespaceList "monitor,virtualdesktop"`
- If same error persists, it's a generator/validator bug; if it passes, was stale content

### Files to Investigate
- `mcp-cli-metadata/3.0.0-beta.31+.../cli-output.json` — search for these tools and check params
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/AnnotationsParametersRawStep.cs`
- `mcp-tools/DocGeneration.PipelineRunner/Validation/ToolFamilyPostAssemblyValidator.cs` — source CLI JSON check

### Dina Questions and answers

If tool parameter generation is deterministic - how is this happening? 

### Resolution (Updated per Dina feedback)

**Root cause investigation required.** Dina's question is exactly right — if parameter extraction (Step 1) is deterministic, these mismatches should not happen. This points to one of these causes:

1. **Stale output from a previous beta version** — If `generated-*/` directories weren't fully cleaned before the beta.31 run, Step 4's validator could be comparing fresh CLI JSON (beta.31) against stale Step 1 output (beta.30). The `monitor` and `virtualdesktop` tools may have had param changes between versions.
2. **A bug in the deterministic extraction** — If the extraction IS running fresh but still producing wrong output, there's a real bug in `AnnotationsParametersRawStep`.
3. **Validator comparing against wrong CLI JSON** — The validator may be loading a cached/stale `cli-output.json` instead of the one extracted for beta.31.

### Investigation Steps (Updated)
1. **First**: Confirm whether the output directories were cleaned before the beta.31 run (check `start-with-logs.ps1` or `preflight.ps1` clean step)
2. **Second**: Run a fresh generation for just `monitor` and `virtualdesktop` with a guaranteed clean output directory and see if the error persists
3. **Third**: If error persists on clean run, compare the Step 1 output (parameter include files) against the CLI JSON to find where the mismatch is introduced

### Files to Investigate (Updated)
- `mcp-cli-metadata/3.0.0-beta.31+.../cli-output.json` — verify `monitor resource log query` params and `virtualdesktop hostpool host list` params
- `mcp-tools/scripts/preflight.ps1` — verify clean step removes ALL prior generated output
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/AnnotationsParametersRawStep.cs` — verify parameter extraction logic
- `mcp-tools/DocGeneration.PipelineRunner/Validation/ToolFamilyPostAssemblyValidator.cs` — verify which CLI JSON file the validator loads

### Verification
- Run with explicit clean: `rm -rf generated-monitor generated-virtualdesktop && pwsh ./start-with-logs.ps1 -NamespaceList "monitor,virtualdesktop"`
- If passes: root cause was stale output → ensure preflight always cleans
- If fails: real extraction or validation bug → file separate bug

### Squad follow-up questions for Dina

1. **Clean run confirmation**: Was the `generated-20260801T112536` run a clean run (all previous output deleted), or was it a re-run on top of existing beta.30 output? This is the critical differentiator between "stale data" and "real bug."  DINA'S answer: you need to know this answer - if you can't determine through the file then you need to change the logging to be able to determine this
2. **Preflight clean scope**: Does `preflight.ps1` currently delete ALL `generated-*` directories, or only `generated/`? If it only cleans `generated/`, namespace-specific directories (`generated-monitor/`, etc.) could carry stale content.  DINA'S answer: we need to keep all old generations in `generated-old` - that should be the cleanup
3. **Version-specific output directories**: Should we include the CLI version in the output directory name (e.g., `generated-monitor-beta31/`) to prevent cross-version contamination? Or is a strict clean-before-run sufficient?  DINA'S answer: explain this question more - I'm not sure what you are asking

---

## Execution Priority

| Priority | Finding | Impact | Effort |
|----------|---------|--------|--------|
| 1 | Finding 2 (foundry removed) | Blocks 1 namespace, easy fix | 5 min |
| 2 | Finding 3 (param validation) | Blocks 14 namespaces, systemic | Medium-High |
| 3 | Finding 4 (CLI JSON mismatch) | Blocks 2 namespaces | Medium |
| 4 | Finding 1 (timeouts) | Transient, 2 namespaces | Low priority |

## Success Criteria

A full `pwsh ./start-with-logs.ps1` run produces **64/64 succeeded** (after `foundry` removal) or equivalent. Zero critical failures.
