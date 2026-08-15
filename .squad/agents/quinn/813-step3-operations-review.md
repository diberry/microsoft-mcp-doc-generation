# Quinn — Operations Review: #813 Step 3 (Canonical Parameter Contract)

**Branch:** `squad/813-step3-canonical-parameter-contract` (head `ee3ca02`, base `b58431f`)  
**PR:** https://github.com/diberry/microsoft-mcp-doc-generation/pull/816  
**Reviewer:** Quinn (DevOps / Scripts)  
**Date:** 2026-08-15

---

## Scope

Operational integration, fail-closed behavior, argument wiring, scripts/CI impact, migration safety, secrets/redaction.

## Method

| # | Check | Command / File | Result |
|---|-------|----------------|--------|
| 1 | DependencySuppression tests | `dotnet test ...PipelineRunner.Tests --filter "FullyQualifiedName~DependencySuppressionTests"` | **Passed: 29, Failed: 0** (exit 0) |
| 2 | RunAccounting tests | `dotnet test ...PipelineRunner.Tests --filter "FullyQualifiedName~RunAccountingTests"` | **Passed: 11, Failed: 0** (exit 0) |
| 3 | Pester validation tests | `Invoke-Pester -Path ./mcp-tools/validation/tests -Output Detailed -CI` | **Passed: 118, Failed: 8, Skipped: 1** (exit 8). All 8 failures are in `Scan-McpToolCoverage.Tests.ps1` — known pre-existing. |
| 4 | Full solution test | `dotnet test mcp-doc-generation.sln --configuration Release` | **3,604 passed, 1 failed** (exit 1). Single failure is `FamilyMetadataGeneratorTests.GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription` — **pre-existing**, not touched by this PR (`git diff b58431f..HEAD` shows zero changes to that file). |
| 5 | Script/CI changes | `git diff b58431f..HEAD --name-only \| Select-String "start\.sh\|scripts/\|\.github/"` | No matches — no scripts or CI workflows were modified. |
| 6 | Secrets scan | `git diff b58431f..HEAD -- "*.cs" \| Select-String "Environment\.GetEnvironment\|apikey\|token\|secret\|credential"` | No real secrets. Only benign test fixture references to `secret-name` as a parameter name. Error messages emit only file paths and stable error codes — no env values, endpoints, keys, or tokens. |
| 7 | AD-027 PowerShell check | No `.ps1` files touched by this PR. | N/A — no collision risk. |

---

## Findings

| # | Severity | Finding | Evidence |
|---|----------|---------|----------|
| 1 | **Low** | `ExamplePrompts.Generation/Program.cs:140-144`: when `--param-manifests` directory doesn't exist, the **standalone generator subprocess** logs a warning and sets `paramManifestsDir = null`, falling back to CLI JSON silently. This is architecturally intentional (subprocess best-effort → step validates post-generation), but a future reader may misread this as a fail-open gap. Consider a code comment clarifying the design intent. | `Program.cs:140-144` — `paramManifestsDir = null;` with only a `Console.WriteLine("⚠️  ...")` |
| 2 | **Low** | `ParameterCrossCheckService.cs:42-46`: `if (!File.Exists(manifestPath)) { rewrittenTools.Add(tool); continue; }` silently skips tools without a manifest in Step 4's cross-check. By design (Step 4 is optional improvement), but could mask a broken workspace. Documented in README. | `ParameterCrossCheckService.cs:42-46` |

---

## Verification Details

### 1. Fail-closed (requirement #1)

Traced the fail-closed path end-to-end:

- **`CanonicalParameterManifestLoader.cs`**: Never returns null. Every invalid state (missing, malformed JSON, bare-array legacy, wrong schema, command mismatch, namespace mismatch, stale build, empty params, duplicate canonicals, alias collisions) throws `ParameterManifestException` with a stable `ParameterManifestErrorCode` constant. No `catch (JsonException) { return empty; }` — the `JsonException` is wrapped into a new `ParameterManifestException`.
- **`ExamplePromptsStep.cs:495-500`**: `LoadRequiredOptionsAsync` throws `ParameterManifestException(PARAM_MANIFEST_NOT_FOUND, ...)` when the parameters directory is absent. Message includes "Ensure Step 1 completed."
- **`ExamplePromptsStep.cs:218-228`**: Retry loop catches `ParameterManifestException`, records a classified warning with `[pme.ErrorCode]`, and **breaks** (no further retries). The tool stays in `unresolvedCommands`, which becomes an `ArtifactFailure` in the final loop — fulfilling AD-029 classified-failure requirement.
- **`ParameterCrossCheckService.cs:53-56`**: `catch (ParameterManifestException) { throw; }` — re-throws, does not swallow.

### 2. AD-029 interaction (requirement #2)

- DependencySuppression (29 tests) and RunAccounting (11 tests) all pass — suppression/accounting behavior is undisturbed.
- The pre-AI gate returning `Success=true` with empty `ArtifactFailures` (non-fatal validator outcome) is in a separate code path (`BuildResult` with `succeeded: true`) and is not touched by this PR.
- `ParameterManifestException` surfaces as a recorded `ArtifactFailure` carrying the error code (line 222-224 logs it, line 228 breaks → tool ends up unresolved → `ArtifactFailure` created at line 259-270 of the retry outcome).

### 3. Argument wiring (requirement #3)

| Caller | Argument | Path |
|--------|----------|------|
| `ExamplePromptsStep.cs:58-60` | `--param-manifests` → generator subprocess | `Path.Combine(context.OutputPath, "parameters")` |
| `ExamplePromptsStep.cs:316` | `--parameter-manifests-dir` → validator subprocess | `Path.Combine(outputPath, "parameters")` |
| `ExamplePromptsStep.cs:214` | `EnrichValidationFeedbackAsync` internal | `Path.Combine(context.OutputPath, "parameters")` |
| `ParameterGenerator.cs:61` (emitter) | writes manifest to | `outputDir` which is `Path.Combine(parentDir, "parameters")` per `DocumentationGenerator.cs:118` |

All point to `{outputPath}/parameters/`. The emitter writes `{tool}-params.json` using `ToolFileNameBuilder.BuildParameterManifestFileName`; the loader reads using the same builder. ✅ Consistent.

### 4. Scripts and CI (requirement #4)

No scripts, workflows, or Pester specs were modified. No AD-027 violations (no `.ps1` touched).

### 5. Migration (requirement #5)

- Legacy bare-array manifests: `CanonicalParameterManifestLoader.cs:112-118` detects `JsonValueKind.Array` and throws `PARAM_MANIFEST_LEGACY_FORMAT` with message "Rerun Step 1." — actionable, non-crashing.
- Missing directory: `ExamplePromptsStep.cs:497-500` throws `PARAM_MANIFEST_NOT_FOUND` with "Ensure Step 1 completed." — actionable.
- Re-running in same workspace: Step 1 overwrites manifests (file write is idempotent), no duplication or stranded diagnostics.

### 6. Secrets/redaction (requirement #6)

Error messages contain: file paths, tool commands, schema versions, build version strings. None of these are secret. No `Environment.GetEnvironmentVariable` calls in the new code paths. Console output in the standalone generator prints directory paths (already visible to the user running the pipeline). ✅ Clean.

---

## Verdict

**APPROVE WITH NOTES**

**Finding counts:** Blocking: 0, High: 0, Medium: 0, Low: 2

The fail-closed contract is properly implemented end-to-end. Stable error codes propagate through the pipeline step into classified `ArtifactFailure` records. AD-029 suppression/accounting tests pass (29 + 11 = 40 tests green). No scripts or CI changes needed or missing. Migration from legacy/stale workspaces produces actionable "Rerun Step 1" messages. No secrets exposed. The two Low findings are documentation/readability suggestions only.

---

↩︎ Responding to: "You are **Quinn**, DevOps / Scripts Engineer. You hold the **operational integration / fail-closed / scripts approval seat** for Step 3 of issue diberry/microsoft-mcp-doc-generation#813..."
