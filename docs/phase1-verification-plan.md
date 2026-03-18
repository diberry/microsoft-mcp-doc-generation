# Phase 1 verification plan

## Goal
Validate that the Phase 1 IChatClient migration keeps the public GenerativeAI surface compatible, preserves current build/test health, and does not regress workbooks output quality.

## Baseline to compare against
- Build baseline: `docs\phase1-baseline.md`
- Pre-migration branch: `main`
- Baseline build result: success, 0 warnings, 0 errors
- Baseline test result: 638 total, 638 passed, 0 failed, 0 skipped
- Workbooks baseline output directory: `generated-workbooks\`
- Expected workbooks tool files:
  - `azure-workbooks-create.md`
  - `azure-workbooks-delete.md`
  - `azure-workbooks-list.md`
  - `azure-workbooks-show.md`
  - `azure-workbooks-update.md`
- Expected workbooks tool count: **5/5/5**
  - 5 files in `generated-workbooks\tools\`
  - `tool_count: 5` in `generated-workbooks\tool-family\workbooks.md`
  - 5 tool sections in `generated-workbooks\tool-family\workbooks.md` (excluding `Related content`)

## Verification steps

### 1. Build verification
**Objective:** Solution builds with zero errors.

**Command**
```powershell
dotnet build .\docs-generation.sln --nologo --tl:off -clp:Summary -v:minimal
```

**Pass criteria**
- Exit code is 0
- Build reports 0 errors
- Any warnings are documented and reviewed against baseline (baseline is 0 warnings)

**Evidence to capture**
- Build summary line
- Warning/error counts

### 2. Test verification
**Objective:** All existing tests still pass.

**Command**
```powershell
dotnet test .\docs-generation.sln --nologo --tl:off -v:minimal
```

**Pass criteria**
- All pre-existing tests still pass
- Existing baseline count remains green: **638 passing tests**
- If new tests are added, record the delta, but do not allow any regression in the original baseline set

**Evidence to capture**
- Total / passed / failed / skipped counts
- Any failed test names and stack traces

### 3. API compatibility verification
**Objective:** `GenerativeAIClient.GetChatCompletionAsync(string, string, int, CancellationToken)` remains unchanged.

**Primary file**
- `docs-generation\GenerativeAI\GenerativeAIClient.cs`

**Pass criteria**
- Public method name is unchanged
- Parameter order and types are unchanged
- Default value behavior for `maxTokens` and `CancellationToken` remains compatible

**Evidence to capture**
- Method signature copied into verification notes
- File path and line reference

### 4. Constructor compatibility verification
**Objective:** `GenerativeAIClient(GenerativeAIOptions?)` remains unchanged.

**Primary file**
- `docs-generation\GenerativeAI\GenerativeAIClient.cs`

**Pass criteria**
- Constructor still exists as a public entry point
- Optional `GenerativeAIOptions?` parameter remains supported
- Existing call sites compile without modification

**Evidence to capture**
- Constructor signature
- Any impacted call sites, if found

### 5. New testability verification
**Objective:** New `GenerativeAIClient(IChatClient)` constructor exists and is usable for tests.

**Primary file**
- `docs-generation\GenerativeAI\GenerativeAIClient.cs`

**Pass criteria**
- Public constructor accepting `IChatClient` exists
- A test or compile-time usage proves it can be instantiated with a fake/stub chat client

**Evidence to capture**
- Constructor signature
- Test name or sample usage proving dependency injection works

### 6. Integration verification on `workbooks`
**Objective:** Re-run the pipeline on a small namespace that has already passed and confirm no content regression.

**Recommended command**
```powershell
.\docs-generation\scripts\Generate-ToolFamily.ps1 -ToolFamily 'workbooks' -Steps @(1,2,3,4,5,6)
```

**Artifacts to compare**
- `generated-workbooks\tools\`
- `generated-workbooks\tool-family\workbooks.md`
- `generated-workbooks\logs\`

**Pass criteria**
- Same number of output files as baseline
- Same file names as baseline
- Tool count remains **5/5/5**
- No missing tool sections in `workbooks.md`
- No missing parameter tables or missing required parameters in generated markdown

**Specific file-name check**
- `azure-workbooks-create.md`
- `azure-workbooks-delete.md`
- `azure-workbooks-list.md`
- `azure-workbooks-show.md`
- `azure-workbooks-update.md`

**Suggested content checks**
- Compare frontmatter block
- Compare H2 tool sections in `workbooks.md`
- Spot-check parameter tables and example prompts for each tool
- Run post-assembly validation if available

### 7. Middleware verification
**Objective:** Confirm Microsoft.Extensions.AI middleware is wired and observable.

**Primary files**
- `docs-generation\GenerativeAI\GenerativeAIClient.cs`
- `docs-generation\Shared\LogFileHelper.cs`
- `generated-workbooks\logs\`

**Pass criteria**
- The chat client is built through the Microsoft.Extensions.AI pipeline rather than direct ad-hoc calls
- Logging output from a real run shows middleware activity or telemetry evidence during AI calls
- Retry/telemetry behavior is observable in logs or console output

**Evidence to capture**
- Relevant log lines from `generated-workbooks\logs\*.log`
- Code reference showing middleware composition

### 8. Auth fallback verification
**Objective:** DefaultAzureCredential works when API key is not set.

**Primary files**
- `docs-generation\GenerativeAI\GenerativeAIClient.cs`
- `docs-generation\GenerativeAI\GenerativeAIOptions.cs`

**Recommended setup**
- Unset `FOUNDRY_API_KEY`
- Keep endpoint and deployment configured
- Set `FOUNDRY_USE_DEFAULT_CREDENTIAL=true`

**Pass criteria**
- The client chooses `DefaultAzureCredential` when no API key is available
- A smoke run or targeted test succeeds without `FOUNDRY_API_KEY`
- Logs clearly show default-credential mode was selected

**Evidence to capture**
- Relevant environment/config values used for the run
- Success output or targeted test result
- Log evidence confirming fallback path

### 9. Quality checklist for generated tool files
**Objective:** Verify every generated workbooks tool file still meets content quality standards.

Apply this checklist to each file in `generated-workbooks\tools\`:
- Frontmatter is present and valid
- Parameter table is complete
- Example prompts are present
- Tool annotation hints section is present
- No missing required parameters in examples
- No content regression versus the pre-migration workbooks baseline

**Files to review**
- `azure-workbooks-create.md`
- `azure-workbooks-delete.md`
- `azure-workbooks-list.md`
- `azure-workbooks-show.md`
- `azure-workbooks-update.md`

## Exit criteria
Phase 1 is ready from QA once all of the following are true:
- Build succeeds with zero errors
- Existing baseline tests remain green
- Public API compatibility is preserved
- New `IChatClient` constructor exists and is testable
- Workbooks regeneration matches baseline file/count expectations
- Middleware activity is observable
- DefaultAzureCredential fallback works without an API key
- Generated markdown passes the quality checklist without regression
