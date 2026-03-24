# Decision: Acrolinx Compliance Sections in All AI System Prompts

**Date:** 2026-03-25
**Author:** Sage (AI / Prompt Engineer)
**Status:** Implemented (PR #223)

## Decision

Every AI system prompt that generates prose for published articles must include a dedicated **Acrolinx Compliance Guidelines** section with 10 standardized rules.

## Rules Added to All Prompts

1. Present tense (no "will return" — use "returns")
2. Contractions ("doesn't" not "does not")
3. Active voice ("The tool lists" not "Resources are listed")
4. Introductory commas ("For example, ..." "By default, ...")
5. No first person (never "we", "our", "us")
6. Acronym expansion on first use ("role-based access control (RBAC)")
7. Site-root-relative URLs ("/azure/..." not full learn.microsoft.com)
8. Sentence length under 35 words
9. No wordy phrases ("to" not "in order to")
10. Brand compliance (official Azure service names)

## Affected Prompts

- Step 2: `ExamplePrompts.Generation/prompts/system-prompt-example-prompt.txt`
- Step 3: `ToolGeneration.Improvements/Prompts/system-prompt.txt`
- Step 4: `ToolFamilyCleanup/prompts/tool-family-cleanup-system-prompt.txt`
- Step 6: `HorizontalArticles/prompts/horizontal-article-system-prompt.txt`
- Shared: `prompts/system-prompt.txt`, `prompts/tool-family-cleanup-system-prompt.txt`

## Rationale

This is the P0 (highest-leverage) item from `docs/acrolinx-compliance-strategy.md`. Adding style rules directly to prompts is more effective than post-processors alone because the AI generates compliant text from the start, reducing the number of violations that downstream fixers need to catch.

## Impact on Team

- **Morgan:** If you modify any system prompt, preserve the Acrolinx section. 42 tests in `AcrolinxComplianceSectionTests.cs` will fail if the section is removed.
- **Parker:** The new tests are in `DocGeneration.Steps.ToolFamilyCleanup.Tests/AcrolinxComplianceSectionTests.cs`. They read prompt files from disk and assert required keywords.
- **Quinn:** No pipeline changes needed — prompts are loaded at runtime.
