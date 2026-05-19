# Bug Fixes: #603, #604, #602 — Namespace Resolution

**Author:** Morgan (C# Generator Developer)  
**Date:** 2026-05-19  
**Branch:** `squad/603-604-602-namespace-resolution-fixes`

---

## Summary

Fixed three interconnected bugs that caused the pipeline to fail for decomposed namespaces (e.g., `extension_azqr`, `extension_ghissues`) and when Step 3 is skipped.

---

## Bug #603 — ResolveFamilyName uses CLI prefix instead of raw namespace key

**Root cause:** `ResolveFamilyName()` in `ToolFamilyCleanupStep.cs` always took `tokens[0]` from the first CLI command (e.g., `"extension"` for `extension azqr scan`). The brand mapping keys use underscores (`extension_azqr`), so the lookup always missed.

**Files changed:**
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/ToolFamilyCleanupStep.cs`  
  `ResolveFamilyName()` now checks `currentNamespace` against brand mappings first; falls back to CLI prefix only if no direct match.
- `shared/DocGeneration.Core.Shared/ToolFileNameBuilder.cs`  
  `ResolveFamilyFileName()` now tries `familyName.Replace(' ', '_')` as a secondary key when direct lookup fails.

**Tests added:** `ToolFamilyCleanupStepTests.Step4_UsesDecomposedNamespace_AsFamilyName_Bug603`,  
`ToolFileNameBuilderTests.ResolveFamilyFileName_SpaceInFamilyName_TriesUnderscoreKey_Bug603`,  
`ToolFileNameBuilderTests.ResolveFamilyFileName_SpaceKey_NoUnderscoreMapping_FallsBackToFamilyName_Bug603`

---

## Bug #604 — BrandMappingValidator rejects prefix-covered namespaces

**Root cause:** `Program.cs` in `DocGeneration.Steps.Bootstrap.BrandMappings` extracted the first token of each CLI command as the namespace (e.g., `"extension"`) and required an exact match in brand mappings. Decomposed entries like `extension_azqr` were never checked.

**Files changed:**
- `mcp-tools/DocGeneration.Steps.Bootstrap.BrandMappings/Program.cs`  
  After exact-match fails, checks if any brand mapping key starts with `ns + "_"`. If yes, the namespace is considered covered and excluded from unmapped list.

**Tests added:** `BrandMapperValidatorTests.Validator_ConsidersNamespaceCovered_WhenDecomposedEntriesExist_Bug604`,  
`BrandMapperValidatorTests.Validator_ReportsUnmapped_WhenNamespaceHasNoExactOrPrefixMatch_Bug604`

---

## Bug #602 — Step 4 fails when Step 3 is skipped (tools/ empty)

**Root cause:** Step 4 (`ToolFamilyCleanupStep`) hard-failed if `tools/` directory didn't exist or was empty, with no fallback. When Step 3 is skipped (no AI steps), `tools/` is never populated.

**Files changed:**
- `mcp-tools/DocGeneration.PipelineRunner/Steps/Namespace/ToolFamilyCleanupStep.cs`  
  Before failing, checks if `tools-raw/` exists and is non-empty; if so, uses it as the input directory. Logs `"INFO: Using tools-raw/ as fallback (tools/ not available)."`.

**Tests added:** `ToolFamilyCleanupStepTests.Step4_FallsBackToToolsRaw_WhenToolsDirectoryAbsent_Bug602`,  
`ToolFamilyCleanupStepTests.Step4_FallsBackToToolsRaw_WhenToolsDirectoryEmpty_Bug602`,  
`ToolFamilyCleanupStepTests.Step4_Fails_WhenBothToolsAndToolsRawAbsent_Bug602`

---

## Test Results

- `DocGeneration.PipelineRunner.Tests` — 12/12 ToolFamilyCleanup tests pass ✅
- `DocGeneration.Core.Shared.Tests` — 6/6 ResolveFamilyFileName tests pass ✅  
- `DocGeneration.Steps.Bootstrap.BrandMappings.Tests` — 17/17 pass ✅  
- `DocGeneration.Steps.ToolFamilyCleanup.Tests` — 880/881 pass (1 pre-existing `R_CG2` failure unrelated to these changes) ✅
