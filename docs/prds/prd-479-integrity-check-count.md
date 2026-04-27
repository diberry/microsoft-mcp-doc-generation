# PRD: Fix tool count integrity check (Issue #479)

## Problem

During beta.5 content generation, Step 4 failed for 3 namespaces (azurebackup,
compute, storage) with "tool count integrity check" errors. The validator
reported missing tools even though all tools were present in the generated
output.

## Root cause

The validator counted H2 (`##`) headings in the tool-family article and compared
that count to the number of tool files on disk. In the Azure MCP Server content
model, **every tool is an H2 section** — but the article also contains non-tool
H2 sections such as `## Related content`. These structural headings inflate the
H2 count above the actual tool count, causing a false mismatch.

For example, the `compute` tool-family article has 12 tools but 13 H2 headings
because of the trailing `## Related content` section. The old validator saw
13 ≠ 12 and flagged it as a failure.

### What was NOT the problem

The original issue text described the structure as "H2 category headings with H3
tool sections underneath." That description was incorrect. Baseline files confirm
that tools are always H2 headings — there is no H2-category / H3-tool nesting in
the generated content. The mismatch was caused by non-tool H2s, not by a
heading-level discrepancy.

## Fix

Replace the H2-heading count with a count of `<!-- @mcpcli ... -->` HTML comment
markers. Each tool section contains exactly one `@mcpcli` marker, making it a
reliable 1:1 proxy for the true tool count. Non-tool sections like
`## Related content` do not contain these markers and are correctly excluded.

### Changes

| File | Change |
|------|--------|
| `ToolFamilyPostAssemblyValidator.cs` | Compare `toolFileCount` against `mcpMarkerCount` (count of `@mcpcli` markers) instead of `articleSectionCount` (count of H2 headings). |
| `ToolFamilyPostAssemblyValidator.cs` | Loosen `McpCliRegex` to allow optional leading whitespace (`^\s*<!--`). |
| `ToolFamilyPostAssemblyValidatorTests.cs` | Add two tests: one for an article with a `## Related content` section that previously caused a false failure, and one confirming correct marker-count validation. |

### Validation evidence

| Namespace | H2 headings | @mcpcli markers | tool_count (frontmatter) | Tool files |
|-----------|-------------|-----------------|--------------------------|------------|
| azurebackup | 17 | 16 | 16 | 16 ✅ |
| compute | 13 | 12 | 12 | 12 ✅ |
| storage | 8 | 7 | 7 | 7 ✅ |

In every case, the `@mcpcli` marker count matches both the frontmatter
`tool_count` and the number of individual tool files on disk.

## Status

- [x] Code change implemented
- [x] Unit tests added
- [x] PRD rationale corrected (non-tool H2s, not H3 nesting)
- [ ] PR merged
- [ ] Published articles validated post-merge
