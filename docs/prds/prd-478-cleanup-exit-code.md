# PRD: Fix Step 4 Tool-Family Cleanup Exit Code Failures

**Issue Reference:** #478  
**Status:** Draft  
**Date Created:** 2025  
**Component:** DocGeneration Pipeline - Step 4 (Tool-Family Cleanup Subprocess)

---

## Executive Summary

The Step 4 tool-family cleanup subprocess exits with code 1 (failure) for three namespaces—appservice, functions, and virtualdesktop—despite successfully generating output for appservice and virtualdesktop. For functions, no output file is generated. The non-zero exit code blocks automated downstream pipeline steps. This PRD addresses root cause analysis, robust error handling, and ensuring the cleanup process either succeeds with exit code 0 or fails fast with clear diagnostics.

---

## Problem Statement

### What's Happening
During content generation, the tool-family cleanup subprocess (Step 4) encounters failures for specific namespaces that prevent pipeline continuation:

1. **appservice**: Cleanup completes (output file generated), but exits with code 1
2. **functions**: Cleanup fails completely (no output file generated), exits with code 1  
3. **virtualdesktop**: Cleanup completes (output file generated), but exits with code 1

### Symptoms
- Exit code 1 from cleanup subprocess
- Pipeline halts at Step 4 for these namespaces
- Downstream automated processing (deployment, validation, publishing) cannot proceed
- **functions** is critical—loss of output blocks dependent systems

### Evidence
- appservice and virtualdesktop have complete, usable output files despite non-zero exit
- functions has no output file, indicating incomplete cleanup
- Exit code mismatch with actual completion status

### Business Impact
- Pipeline automation fails
- Manual intervention required to investigate and continue processing
- Downstream systems (documentation deployment, validation checks) are blocked
- functions namespace is not available for beta.5 release

---

## Root Cause Analysis

### Potential Root Causes (Investigation Needed)

#### 1. Resource or Permission Issues During Cleanup
- Subprocess may fail to delete temporary files (file locking, permission denied)
- Temp directory cleanup may fail while main output succeeds
- File handle not released before cleanup attempt

#### 2. Incomplete Error Handling in Subprocess
- Cleanup throws unhandled exception, subprocess terminates with exit code 1
- Error occurs after output file is written, but before cleanup completes
- Exception is swallowed somewhere in the process pipeline

#### 3. Language/Runtime Differences (C# vs PowerShell/Node.js)
- Tool may be written in a different language than pipeline orchestrator
- Cross-process error handling may not propagate correctly
- Exit code semantics differ between languages (e.g., exit 1 vs. throw)

#### 4. Missing Output or Incomplete Generation
- functions namespace produces no output → cleanup detects missing output and exits 1
- appservice/virtualdesktop produce output but cleanup still fails for unknown reason
- Inconsistent behavior suggests namespace-specific or data-dependent failure

#### 5. Dependencies or External Resources
- Cleanup depends on external service that fails intermittently
- Network or file I/O timeout during cleanup
- Resource contention (disk space, memory) during cleanup phase

### Why Current Error Handling Fails
- Exit code 1 indicates generic failure (not specific diagnosis)
- Subprocess may not log detailed error messages to pipeline logs
- Pipeline treats any non-zero exit as complete failure (no partial recovery)
- No distinction between "output generated but cleanup failed" vs. "generation failed completely"

---

## Proposed Solution

### Technical Approach

**Goal**: Ensure cleanup subprocess either succeeds (exit 0) or fails fast with actionable diagnostics, allowing pipeline to handle failures appropriately.

#### Phase 1: Diagnostics & Root Cause Discovery
1. **Enhanced Logging in Cleanup Subprocess**
   - Log detailed messages before and after each cleanup operation
   - Capture stack traces for any exceptions
   - Log exit code reason explicitly
   - Include namespace name, file paths, and timestamps

2. **Isolate Failure Point**
   - Add logging checkpoints:
     - After output file generation
     - Before cleanup begins
     - For each cleanup operation (delete temp, close handles, etc.)
     - After cleanup completes
   - Identify which step fails for each namespace

3. **Compare Namespaces**
   - Identify what's different about appservice, functions, virtualdesktop
   - Compare against passing namespaces
   - Look for namespace-specific edge cases (large files, special characters, dependencies)

#### Phase 2: Fix Based on Root Cause
Once root cause is identified:

- **If file locking issue**: Implement retry logic with exponential backoff and file handle cleanup
- **If exception in cleanup**: Add try-catch around cleanup operations, log details, continue to next operation
- **If incomplete generation (functions case)**: Validate output before cleanup; fail fast with "generation incomplete" message
- **If external resource failure**: Implement graceful degradation (proceed without full cleanup if output is valid)

#### Phase 3: Robust Error Handling
```csharp
// Pseudocode for enhanced cleanup with better exit codes
try 
{
    // Phase 1: Validate output was generated
    if (!File.Exists(outputPath))
    {
        Log.Error($"Output file not generated for namespace {namespace}");
        return 1; // Fail: generation incomplete
    }
    
    // Phase 2: Perform cleanup operations
    try
    {
        CleanupTempFiles();
        CloseFileHandles();
        // ... other cleanup
    }
    catch (Exception ex)
    {
        Log.Warning($"Cleanup error: {ex.Message}. Output is valid; continuing.");
        // Decide: return 0 (success with warning) or 1 (failure)
        // based on severity of cleanup failure
    }
    
    // Phase 3: Exit with appropriate code
    Log.Info($"Cleanup completed for namespace {namespace}");
    return 0; // Success
}
catch (Exception ex)
{
    Log.Error($"Unexpected error: {ex}");
    return 1; // Failure
}
```

### Implementation Changes

**File(s):** 
- `mcp-tools/DocGeneration.PipelineRunner/CleanupProcesses/ToolFamilyCleanupRunner.cs` (or equivalent)
- Cleanup subprocess entry point (C# console app, PowerShell script, Node.js script—TBD per investigation)

**Changes:**
1. Add comprehensive logging around each cleanup operation
2. Distinguish between "output missing" (fail hard) and "cleanup failed" (log warning, return 0)
3. Implement file handle cleanup before attempting file deletion
4. Add retry logic for transient file system errors
5. Document exit codes: 0 = success, 1 = missing output/generation incomplete, 2 = cleanup error
6. Return 0 if output is valid, even if cleanup has minor issues

### Pipeline Integration
- Pipeline should check output file existence independently
- If output exists, treat non-zero exit as warning (log, continue)
- If output missing, treat non-zero exit as hard failure (halt)
- Document behavior in pipeline orchestration code

---

## Acceptance Criteria

- [ ] Cleanup subprocess exits with code 0 for appservice and virtualdesktop
- [ ] Cleanup subprocess generates output file for functions (then exits 0)
- [ ] Detailed diagnostic logs are available for any failure
- [ ] Each cleanup step is independently logged with timestamps
- [ ] Temporary files are properly cleaned up
- [ ] Exit code meanings are documented (0 = success, 1 = generation incomplete, 2 = cleanup error)
- [ ] Pipeline continues to next step when cleanup succeeds
- [ ] Pipeline halts only when output is missing
- [ ] All three namespaces (appservice, functions, virtualdesktop) proceed past Step 4
- [ ] Cleanup process handles edge cases gracefully (file locks, permissions, large files)
- [ ] No temporary files remain after cleanup
- [ ] Root cause for each namespace's failure is identified and fixed

---

## Test Plan

### Diagnostics Phase (Investigation)
1. **Enable verbose logging**
   - Run cleanup subprocess with debug logging enabled
   - Capture full output for appservice, functions, virtualdesktop
   - Identify exact failure point

2. **Compare namespaces**
   - Run cleanup for passing namespaces (e.g., storage, compute)
   - Compare log output between passing and failing namespaces
   - Identify differences (file size, characters, permissions, etc.)

3. **Isolated testing**
   - Run cleanup for single namespace in isolation
   - Run cleanup for multiple namespaces in sequence
   - Test under resource constraints (low disk space, file handle limits)

### Unit Tests
1. **Test output validation**
   - Verify cleanup checks for output file existence
   - Verify cleanup fails with code 1 if output missing
   - Verify cleanup succeeds with code 0 if output exists

2. **Test cleanup operations**
   - Mock file system operations
   - Verify cleanup calls correct operations in correct order
   - Verify cleanup handles missing temp files gracefully

3. **Test error handling**
   - Simulate file lock error during cleanup
   - Simulate permission denied error
   - Verify proper exit codes and logging

4. **Test logging**
   - Verify each cleanup step logs with correct level (Info/Warning/Error)
   - Verify exception details are captured and logged
   - Verify timestamps and namespace context included

### Integration Tests
1. **Run cleanup for appservice, functions, virtualdesktop**
   - Verify output files are generated (or identified as missing)
   - Verify exit code is 0 for appservice and virtualdesktop
   - Verify exit code is 0 for functions (and output exists)
   - Verify temp files are cleaned up

2. **Run full Step 4 pipeline**
   - Verify pipeline continues to next step for all three namespaces
   - Verify no manual intervention required
   - Verify logs are complete and diagnostic

3. **Regression testing**
   - Run cleanup for all other namespaces
   - Verify no regressions in previously-passing namespaces
   - Verify exit codes consistent and expected

### Edge Case Testing
1. Very large output files (GB+)
2. Output files with special characters in path
3. Read-only file system scenarios
4. Concurrent cleanup operations (race conditions)
5. Cleanup with missing intermediate temp directories
6. Cleanup with orphaned file handles

---

## Risk & Impact Assessment

### Risks
- **Risk**: Root cause investigation may take longer than anticipated (unknown failure type)
  - **Mitigation**: Implement diagnostic logging first; run against failing namespaces immediately
  - **Severity**: Medium

- **Risk**: Fixing one namespace's failure may not fix others (different root causes)
  - **Mitigation**: Investigate all three in parallel; identify common patterns
  - **Severity**: Medium

- **Risk**: Cleanup fix may mask underlying generation issues (e.g., functions producing invalid output)
  - **Mitigation**: Validate output correctness as separate step; document assumptions
  - **Severity**: High

### Impact
- **Positive**: Unblocks Step 4 for appservice, functions, virtualdesktop
- **Positive**: Enables beta.5 release to include functions namespace
- **Positive**: Pipeline automation can proceed without manual intervention
- **Positive**: Diagnostic logging improves future troubleshooting
- **Neutral**: No impact on content generation speed or quality (cleanup is cleanup phase)
- **Neutral**: No changes to public APIs or namespace output

---

## Success Metrics

- ✅ All three namespaces pass Step 4 with exit code 0
- ✅ functions namespace output file is generated
- ✅ appservice and virtualdesktop output files remain valid
- ✅ Pipeline proceeds to Step 5 for all three namespaces
- ✅ Zero manual intervention required
- ✅ Root cause is identified and documented
- ✅ Cleanup time stays within acceptable range (< 5 sec per namespace)
- ✅ Diagnostic logs provide actionable failure information

---

## Timeline
- **Investigation & Diagnostics**: 2–3 days
- **Root Cause Analysis**: 1 day
- **Implementation**: 2–3 days (varies by root cause complexity)
- **Testing**: 1–2 days
- **Beta.5 Rollout**: Ready once all tests pass

---

## Appendix: Debugging Checklist

When investigating:
- [ ] Enable full debug logging in cleanup subprocess
- [ ] Capture stderr and stdout for failed namespaces
- [ ] Check file system permissions for temp and output directories
- [ ] Monitor disk space during cleanup
- [ ] Check for file handle leaks (tool compatibility)
- [ ] Verify output files are not read-only or locked
- [ ] Compare appservice/virtualdesktop behavior (why do they succeed despite error?)
- [ ] Determine why functions produces no output
