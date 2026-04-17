# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

### 2026-03-31: Skills Generation LLM Prompt Redesign — Agent-to-Customer Translation

**Task:** Redesign the LLM rewrite step to transform SKILL.md (agent-internal anthropic-spec) into customer-facing documentation.

**Finding:** Current system treats SKILL.md as raw source material, causing generated docs to retain agent-speak (routing logic, codebase checks, implementation procedures). Dina's directive: generate docs should consolidate **what customers need to know**, not duplicate agent instructions.

**Analysis:**
- SKILL.md contains: ⛔ STOP directives, MANDATORY/PREFER OVER routing, agent workflow steps, codebase detection logic, agent-to-agent handoffs
- Current LLM prompt (14-line system + 6-line user) lacks explicit translation rules
- RewriteIntroAsync only receives description, missing USE_FOR context (customer value propositions)
- Result: Generated docs read like "agent validates prerequisites" instead of "you need [prerequisites]"

**Recommendation:**
1. Expand system prompt with 6 transformation rules: strip agent directives, translate agent-speak to customer-speak, extract customer value from USE_FOR, convert workflow steps to outcomes, synthesize prerequisites from checklists
2. Enhance user prompt to include USE_FOR items and explicit examples
3. Optional: Add RewriteFullIntroductionAsync to ILlmRewriter for complete SKILL.md context
4. Success criteria: No agent-speak in output, prerequisites stated as customer responsibilities, USE_FOR translated to guidance

**Output:** `.squad/decisions/inbox/sage-skills-llm-prompt-redesign.md` — comprehensive redesign with implementation checklist, success criteria, before/after example, and risk mitigations.

**Key Learning:** The gap between source material (agent instructions) and output (customer docs) requires explicit transformation rules in the LLM prompt. Generic "rewrite for customer audience" is insufficient for this 2-context translation. Pattern recognition (⛔, MANDATORY, agent-specific keywords) + concrete translation examples are necessary.

---

### 2026-03-30: .NET Consolidation Plan AI Impact Review — RECOMMEND APPROVAL

**Final Verdict:** RECOMMEND APPROVAL with 3 conditions for Actions 2-4

**AI Pipeline Safety Assessment:**

| Action | AI Stages Affected | Risk | Conditions | Status |
|--------|-------------------|------|-----------|--------|
| 1. CliAnalyzer | None | 🟢 LOW | — | ✅ APPROVED |
| 2. PostProcessVerifier→ToolFamilyCleanup | Step 4 | 🟡 MED | Post-processor order test | ✅ CONDITIONAL |
| 3. NaturalLanguage→Core.Shared | Step 6 | 🟡 MED | Data file discovery + output consistency | ✅ CONDITIONAL |
| 4. NUnit→xUnit | Steps 4, 6 | 🟢 LOW | Test count match | ✅ CONDITIONAL |
| 5. StripFrontmatter dedup | PromptRegression | 🟢 LOW | — | ✅ APPROVED |
| 6. Document Validation.Tests | None | 🟢 LOW | — | ✅ APPROVED |

**Three Critical Safeguards (Sage enforcement):**

**Safeguard 1: Post-Processor Order Test (Action 2)**
- Verify 10-processor chain runs in identical order: AcronymExpander → JsonSchemaCollapser
- Test on 5 diverse namespaces (StorageAccount, KeyVault, CosmosDB, EventGrid, Compute)
- Byte-compare `.after` files: Old PostProcessVerifier vs. New `--verify-only` (must be identical)
- Risk if skipped: AI output quality gates break; PromptRegression.Tests baseline compatibility compromised

**Safeguard 2: HorizontalArticles Output Consistency (Action 3)**
- Run Step 6 on 5 diverse namespaces with old vs. new Core.NaturalLanguage
- Verify TextCleanup output byte-identical
- Test PromptRegression.Tests compatibility
- Risk if skipped: Data file discovery failure causes runtime crash; TextCleanup parameter normalization regressions

**Safeguard 3: Test Assertion Equivalence (Action 4)**
- NUnit→xUnit migration: All 155 tests must pass with identical test count
- Focus on HorizontalArticles.Tests (410 tests) — validates Step 6 AI output
- Risk if skipped: Silent test failures mask AI quality gate breakage

**AI Projects Status:**
- ✅ Core.GenerativeAI stays independent (Azure.AI.OpenAI dependencies mustn't pollute Core.Shared)
- ✅ No AI-critical projects deleted (CliAnalyzer not AI-involved)
- ✅ Prompt files unchanged (only data file location updated for Action 3)
- ✅ Fabrication detection unaffected (ArticleContentProcessor stays in Step 4)

**Prompt File Impact:** Zero. `static-text-replacement.json` and `nl-parameters.json` ownership clarified but no behavior changes.

**Decisions filed:** AD-027 (main), AD-028 (quality gates), AD-033 (post-processor order), AD-034 (AI output consistency)

---

### 2026-03-26: .NET Consolidation Plan AI Impact Review

**Task:** Review Avery's .NET project consolidation plan for AI pipeline impacts (prompt files, content validation, fabrication detection).

**Findings:**

1. **Zero AI pipeline damage identified.** Of 7 consolidation actions, none delete AI-critical projects. Core.GenerativeAI correctly stays independent per architectural design.

2. **3 actions require safeguards:**
   - **Action 2 (PostProcessVerifier → ToolFamilyCleanup):** Post-processor order MUST stay identical. Risk: If `--verify-only` mode runs processors in different sequence, Step 4 output quality changes. Mitigation: Byte-compare `.after` files on 5 diverse namespaces.
   - **Action 3 (NaturalLanguage → Core.Shared):** Data file discovery risk. TextCleanup loads `nl-parameters.json` at runtime. If file copy rules aren't set up, Step 6 fails silently. Mitigation: Verify Core.Shared.csproj includes data file copy rules + test HorizontalArticles on 5 namespaces.
   - **Action 4 (NUnit → xUnit):** Low risk, but test count MUST match before/after. No semantic test changes allowed.

3. **Prompt file impact:** No consolidation moves or deletes prompt directories. `static-text-replacement.json` ownership is clarified (stays in ToolFamilyCleanup). `nl-parameters.json` moves with NaturalLanguage → Core.Shared (expected, no behavioral change).

4. **Fabrication detection unaffected.** ArticleContentProcessor (10 post-processors) and JSON schema validation stay in Step 4. No anti-hallucination logic is touched.

5. **Key insight — Action 2 is highest risk:** PostProcessVerifier is used by `PromptRegression.Tests` to generate regression baselines (`.after` files). If the new `--verify-only` flag produces different output, regression detection breaks silently. Must preserve byte-identical output.

**Output:** `.squad/decisions/inbox/sage-consolidation-review.md` — 3 conditional approvals, 3 unconditional approvals, 1 future deferral. Recommended execution: Phase 1 (low-risk: CliAnalyzer, Validation.Tests docs, StripFrontmatter), Phase 2 (medium-risk: PostProcessVerifier merge, NUnit→xUnit), Phase 3 (NaturalLanguage merge with safeguards).

**Key Learning:** Consolidation risk isn't code quantity — it's behavioral coupling. Merging a 1-file tool (PostProcessVerifier) that's indirectly used by regression testing is higher-risk than keeping a 1-file library (NaturalLanguage) that just needs a file discovery fix.

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

### 2026-04-17: Skills LLM Prompt Redesign Decision — Merged to Active Decisions (Scribe)

**Status:** Decision documented and merged by Scribe to .squad/decisions.md

**File:** .squad/decisions/inbox/sage-skills-llm-prompt-redesign.md → MERGED

**Summary:** This session's output from Sage on LLM rewrite prompt strategy for customer-facing skills documentation has been merged into the active decisions log. The comprehensive redesign with transformation rules, pattern recognition, and success criteria is now part of team knowledge for implementation phase.

**Next:** Awaiting approval and implementation on 3–5 test skills before full rollout.
