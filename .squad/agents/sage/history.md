# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-25: Acrolinx Compliance Research

**Task:** Researched how to make tool-family articles score 80+ on Acrolinx.

**Findings:**
- Analyzed 10 tool-family PRs in `MicrosoftDocs/azure-dev-docs-pr`. Current pass rate is 30% (3/10 above 80). Worst: Deploy at 61, Postgres at 64. Best: Cosmos DB at 92.
- Identified 20+ specific Acrolinx rule violations across 7 categories (Clarity, Grammar, Tone, Terminology, Consistency, Brand, Inclusive Language).
- The #1 score killer is **embedded JSON schemas in parameter descriptions** (Deploy article). The #2 is **long compound sentences** (40-60+ words). The #3 is **missing positive contractions** ("it is" instead of "it's").
- ContractionFixer (#145) only covers negative contractions. Extending to positive forms ("it is"→"it's", "you are"→"you're") is a quick P1 win.
- Adding an Acrolinx compliance section to the Step 4 system prompt (with sentence length, active voice, present tense rules) is the highest-impact single change.
- `static-text-replacement.json` only has 10 entries — needs Microsoft Entra ID, deprecated terms, ableist language removals.

**Output:** `docs/acrolinx-compliance-strategy.md` — comprehensive strategy with prioritized implementation plan (P0-P4), specific code examples, and estimated score improvements.

### 2026-03-25: P0 Acrolinx Prompt Implementation

**Task:** Implemented the P0 item from the Acrolinx compliance strategy — added dedicated Acrolinx compliance sections to all AI system prompts.

**Changes:**
- Added 10-rule Acrolinx compliance sections to 6 prompt files: Step 2 (example generation), Step 3 (tool improvements), Step 4 (tool family cleanup), Step 6 (horizontal articles), plus 2 shared prompt copies.
- Step 4 already had 3 partial rules (#143, #145, #146). Expanded to full 10-rule coverage including active voice, no first person, acronyms, relative URLs, sentence length, word choice, and brand compliance.
- Each section is tailored to the prompt's context (e.g., Step 2 focuses on conversational prompt style, Step 6 targets genai- JSON fields).
- Wrote 42 new tests (`AcrolinxComplianceSectionTests.cs`) that verify all prompt files contain required instructions. Tests are parameterized across all 4 step-specific prompts + 2 shared copies.

**Key Insight:** The shared `docs-generation/prompts/system-prompt.txt` is only 1 line — it's NOT a copy of the Step 3 prompt. The step-specific prompts in each `DocGeneration.Steps.*/prompts/` directory are the actual runtime prompts.

**PR:** #223
### 2026-03-25: Acrolinx Compliance P0+P1 Implementation — 4 Services + 9 Static Entries

**Task:** Implement Acrolinx P0+P1 fix plan using strict TDD (AD-007: tests first, then implement).

**Work completed:**

1. **P0: static-text-replacement.json** — Added 9 wordy-phrase entries:
   - `etc.` → `and more`, `in order to` → `to`, `make sure` → `ensure`, `a number of` → `several`, `utilize` → `use`, `functionality` → `feature`, `via` → `through`, `leverage` → `use`, `prior to` → `before`
   - Dropped `please ` — word-boundary regex prevents matching trailing-space keys before next word. Needs a dedicated service.
   - 20 new tests in StaticTextReplacementTests.cs

2. **P1: IntroductoryCommaFixer** — New service inserts commas after: For example, In addition, By default, In this case, If not. Skips code blocks/backticks. Dropped "For each" and "When using" — these need comma after the whole dependent clause, not just the two-word phrase.
   - 20 tests in IntroductoryCommaFixerTests.cs

3. **P1: PresentTenseFixer** — New service converts "will be <verb>ed" → "is/are <verb>ed", "will <verb>" → "<verb>s", "will not be" → "is not". Whitelist of 16 common verbs avoids false positives. Skips code blocks/backticks.
   - 24 tests in PresentTenseFixerTests.cs

4. **P1: AcronymExpander** — Generalized PostProcessor.ExpandMcpAcronym() to config-driven multi-acronym expander. Handles VM, VMSS, AKS, RBAC, IaC, WAF, NSG, VNet, ACR + MCP context pattern. Skips frontmatter/headings/backticks.
   - Config: `data/acronym-definitions.json`
   - 11 tests in AcronymExpanderTests.cs

5. **FamilyFileStitcher wiring** — Stage 4: AcronymExpander (replaces MCP-only), Stage 8: PresentTenseFixer (before ContractionFixer), Stage 10: IntroductoryCommaFixer (after ContractionFixer). Order ensures "will not be" → "is not" → "isn't" flows correctly.
   - 7 integration tests in StitcherAcrolinxIntegrationTests.cs

**Test totals:** 62 new behavioral tests. All 202 ToolFamilyCleanup tests pass. Full solution: 0 build warnings, 0 regressions.

**Key learning:** The static-text-replacement engine's word-boundary regex `(?![A-Za-z0-9_-])` prevents matching keys with trailing spaces when followed by a word character. Entries ending in spaces (like "please ") silently fail. Always use word-boundary-compatible keys.

**PR:** #223 (Created, full team approved, PENDING MERGE)

**Decision issued:** AD-022 (Acrolinx Compliance Strategy), AD-025 (QA Assessment by Parker)

---

### 2026-03-25: Acrolinx Compliance Research

**Task:** Researched how to make tool-family articles score 80+ on Acrolinx.
