# PRD: Fix Tool-Family Integrity Check Heading Level Counting

**Issue Reference:** #479  
**Status:** Draft  
**Date Created:** 2025  
**Component:** DocGeneration Pipeline - Step 4 (Tool-Family Post-Assembly Validation)

---

## Executive Summary

The `ToolFamilyPostAssemblyValidator` in Step 4 of the content generation pipeline incorrectly counts H2 headings to validate tool coverage, causing integrity check failures for namespaces that organize tools under H2 category headers with H3 tool sections. The validator must be updated to count `<!-- @mcpcli -->` markers instead, which accurately reflect the actual number of tools in an article.

---

## Problem Statement

### What's Happening
During beta.5 content generation, the `ToolFamilyPostAssemblyValidator` fails validation for three namespaces: `azurebackup`, `compute`, and `storage`. The validator reports a mismatch between expected tool count and detected heading count.

### Evidence of the Issue
- **azurebackup**: 10 H2 headings detected, but 16 `<!-- @mcpcli -->` markers (actual tools) present
- **compute**: 4 H2 headings detected, but 12 `<!-- @mcpcli -->` markers (actual tools) present  
- **storage**: 5 H2 headings detected, but 7 `<!-- @mcpcli -->` markers (actual tools) present

All affected articles contain the correct number of tool markers, verifying that the content is complete. The mismatch is purely in the validation logic's heading-counting approach.

### Impact
- Pipeline fails for correctly-assembled articles
- Blocks downstream processing steps for critical namespaces
- Creates false positives in quality checks, reducing confidence in the validation pipeline

---

## Root Cause Analysis

### Current Implementation
The validator uses `HeadingRegex` pattern (`^##\s+(.*)$`) to find article sections:
- Matches H2 headings only
- Counts H2 occurrences as a proxy for tool count
- Compares this count against the expected tool count for the namespace

### Why This Fails
When tool families implement **categorical organization**, the document structure becomes:
```
## Category Name (H2)
### Tool Name (H3)
### Tool Name (H3)

## Another Category (H2)
### Tool Name (H3)
```

In this structure, H2 headings represent categories, not tools. The actual tools are at H3 level. The current regex-based counting strategy conflates categories with tools.

### Why Markers Are Reliable
The `<!-- @mcpcli -->` markers are:
- Placed exactly once per tool section
- Independent of document structure/hierarchy
- Already generated and validated during earlier pipeline steps
- A direct count of actual tool content

---

## Proposed Solution

### Technical Approach

**File:** `mcp-tools/DocGeneration.PipelineRunner/Validation/ToolFamilyPostAssemblyValidator.cs`

**Change Summary:**
1. Replace heading-based counting with `<!-- @mcpcli -->` marker counting
2. Update validation logic to search for markers instead of H2 headings
3. Maintain backward compatibility for edge cases where markers may not be present

### Implementation Details

#### Current Code (Pseudocode)
```csharp
var regex = new Regex(@"^##\s+(.*)$", RegexOptions.Multiline);
var headingMatches = regex.Matches(articleContent);
int detectedToolCount = headingMatches.Count;
```

#### Proposed Code (Pseudocode)
```csharp
const string MARKER = "<!-- @mcpcli -->";
int detectedToolCount = articleContent.Split(new[] { MARKER }, StringSplitOptions.None).Length - 1;

// Fallback to heading count if no markers found (edge case handling)
if (detectedToolCount == 0)
{
    var regex = new Regex(@"^##\s+(.*)$", RegexOptions.Multiline);
    var headingMatches = regex.Matches(articleContent);
    detectedToolCount = headingMatches.Count;
}
```

#### Changes Required
1. Add constant `MARKER = "<!-- @mcpcli -->"` 
2. Replace regex matching logic with marker-based counting
3. Implement fallback behavior for legacy or edge-case articles
4. Log both methods during validation for debugging

### Validation Logic
The validator should:
- Count `<!-- @mcpcli -->` markers in the assembled article
- Compare against the expected tool count from the namespace configuration
- Fail validation only if marker count does not match expected count
- Report both marker count and expected count in error messages

---

## Acceptance Criteria

- [ ] Validator counts `<!-- @mcpcli -->` markers instead of H2 headings
- [ ] Validation passes for azurebackup (16 tools), compute (12 tools), and storage (7 tools)
- [ ] Validation still correctly rejects articles with missing or duplicate tools
- [ ] Error messages report marker count, expected count, and mismatch details
- [ ] Fallback to heading count works for articles without markers (backward compatibility)
- [ ] No false positives for categorized tool families
- [ ] Code change is contained within `ToolFamilyPostAssemblyValidator.cs`
- [ ] Existing validation tests continue to pass
- [ ] New tests added for marker-based counting verify correct behavior

---

## Test Plan

### Unit Tests
1. **Test marker counting logic**
   - Create test article with N `<!-- @mcpcli -->` markers
   - Verify validator counts exactly N
   - Verify output matches expected count

2. **Test categorized articles** (Issue scenario)
   - Create article with H2 categories and H3 tools
   - Place markers at H3 level only
   - Verify validator counts tools at marker level, not H2 level

3. **Test marker counting with various structures**
   - Markers in standard section format
   - Markers in nested category structure
   - Markers with extra whitespace/formatting

4. **Test fallback to heading count**
   - Article with no markers present
   - Verify fallback to heading-based counting
   - Log message indicates fallback was used

5. **Test edge cases**
   - Article with 0 tools (0 markers)
   - Article with duplicate markers
   - Article with malformed markers
   - Article with markers in code blocks (should not count)

### Integration Tests
1. Run validator on azurebackup, compute, storage namespaces
2. Verify all three namespaces pass validation
3. Verify error logs show marker-based counting method
4. Verify pipeline continues to next step for these namespaces

### Regression Tests
1. Run validator on previously-passing namespaces
2. Verify no existing articles start failing validation
3. Verify marker counts remain consistent across runs

---

## Risk & Impact Assessment

### Risks
- **Risk**: Articles generated before this change may have missing markers, causing validation to fail
  - **Mitigation**: Implement fallback to heading-based counting for legacy articles
  - **Severity**: Medium

- **Risk**: Marker placement in code blocks or comments could inflate tool count
  - **Mitigation**: Enhance marker format to be unambiguous; validate marker placement during generation
  - **Severity**: Low

### Impact
- **Positive**: Fixes false validation failures for correctly-assembled articles
- **Positive**: Enables beta.5 content generation to proceed for affected namespaces
- **Positive**: More accurate validation aligned with actual content structure
- **Neutral**: No impact on content generation speed or output quality
- **Neutral**: No API changes; internal validator only

---

## Success Metrics

- ✅ azurebackup, compute, storage namespaces pass validation
- ✅ Pipeline exit code is 0 for these namespaces
- ✅ Zero validation false positives post-fix
- ✅ Marker counting accuracy >= 99.9%
- ✅ All existing tests continue to pass
- ✅ New tests achieve >= 90% code coverage for marker counting logic

---

## Timeline
- **Development**: 1–2 days
- **Testing**: 1 day
- **Beta.5 rollout**: Ready for next validation cycle
