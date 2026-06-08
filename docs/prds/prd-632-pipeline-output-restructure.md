# PRD: Content Generation Pipeline Output Restructure

**Status:** Implemented  
**Author:** Nigel-prd-planning (PRD Planning & Strategy)  
**Requested by:** Dina Berry  
**Date:** 2026-05-28  
**Related PR:** #632  
**Target repo:** [diberry/microsoft-mcp-doc-generation](https://github.com/diberry/microsoft-mcp-doc-generation)  
**Project:** azure-ai-tools  
> **Note on file paths:** All file paths in this PRD are relative to the root of this repo (`diberry/microsoft-mcp-doc-generation`).
**Related PRDs:**
- `projects/content/prds/tool-family-regression-protection.md` — structural regression tests (PRs #575–579)
- `projects/project-dina/prds/2026-05-16T06-25-prd-pipeline-improvement.md` — PRD quality improvement process

---

## 1. Problem Statement

The tool-family page generation pipeline currently produces output with three structural defects that misrepresent the nature of Azure MCP tools to readers and undermine documentation quality.

**Defect 1 — Tab order inverts the content hierarchy.**
The MCP tab renders first, the CLI tab second (`CliTabWrapper.cs` line 46). This is backwards. The CLI content is the ground truth — it is derived deterministically from the tools JSON with no AI transformation. The MCP content is an AI-improved derivative of the CLI content. Primary source material should appear first; the derived view second.

**Defect 2 — Tool description is buried inside a tab.**
The natural language description of each tool currently lives inside the MCP tab. This forces readers to click into a tab just to read a plain-English summary of what the tool does. The description belongs above both tabs, in the H2 tool section, where it is visible without interaction.

**Defect 3 — AI prompts inject "(MCP)" branding into tool descriptions.**
The system prompt (`system-prompt.txt` lines 5–7) and user prompt template (`user-prompt-template.txt` line 21) instruct the AI to brand every tool description with "(MCP)". The output text then reads "this is an MCP tool" as if that is a meaningful user-facing distinction. It is not. The tools are tools. The branding is inaccurate, redundant (the page context already establishes MCP), and produces noisy, low-quality descriptions.

These three defects are compounding: the most visible content (the first tab, the description at the top) is AI-branded filler, while the authoritative CLI content is hidden in the second tab and never shown above the fold.

---

## 2. Goals

| # | Goal |
|---|------|
| G1 | Make CLI tab the first (primary) tab, reflecting its role as ground-truth source |
| G2 | Surface the tool's natural language description above both tabs in the H2 section |
| G3 | Remove "(MCP)" branding from AI prompt instructions so tool descriptions are accurate and clean |
| G4 | Update all affected tests to reflect the new structural contract without reducing coverage |

---

## 3. Non-Goals (Explicitly Out of Scope)

- Changing the content or formatting of the CLI tab itself (content is correct; only position changes)
- Changing the content of the MCP tab beyond removing the "(MCP)" string injected by the prompts
- Altering the pipeline's Step 1–3a logic (tools-raw, annotations, parameters, tools-composed)
- Redesigning the `FamilyFileStitcher` stitching logic beyond what tab reordering requires
- Adding new pipeline steps, output artifacts, or generation phases
- Changing the `namespace-brand-mapping.json` or `cli-tab-config.json` configuration
- Modifying public-facing Learn article URLs or TOC structure

---

## 4. Requirements

### R1 — Reorder tabs: CLI first, MCP second
**Priority:** P0  
**File:** `shared/DocGeneration.Core.Shared/CliTabWrapper.cs`, `WrapWithTabs()` method (lines 32–68)  
**Description:** In `WrapWithTabs()`, swap the order so CLI tab content is emitted before MCP tab content. The CLI tab header (e.g., `# [Azure CLI]`) must appear as the first tab in the generated Markdown. The MCP tab header must appear second.  
**Acceptance criteria:**
- Generated tool-family pages have CLI tab as the first tab block
- Generated tool-family pages have MCP tab as the second tab block
- Tab switcher on rendered Learn page defaults to CLI view
- No content is lost; both tabs present their full existing content

---

### R2 — CLI tab label unchanged
**Priority:** P0  
**File:** `shared/DocGeneration.Core.Shared/CliTabWrapper.cs`  
**Description:** The string identifier used as the CLI tab label must remain exactly as it was before this change. This preserves any deep-link anchors or cross-references that target the CLI tab by its label.  
**Acceptance criteria:**
- CLI tab label string value is identical before and after the reorder
- MCP tab label string value is identical before and after the reorder

---

### R3 — Extract tool description from MCP tab content
**Priority:** P0  
**File:** `shared/DocGeneration.Core.Shared/CliTabWrapper.cs`, and the class that calls it in Step 4 (`FamilyFileStitcher`)  
**Description:** Before wrapping content in tabs, extract the natural language description from the MCP tab content. This description is currently produced by `NlpDescriptionExtractor`. It must be removed from the MCP tab payload and returned separately so the caller can place it above the tab block.  
**Acceptance criteria:**
- The tool description does NOT appear inside either tab
- `WrapWithTabs()` either accepts the description as a separate parameter, or returns it alongside the tab block so the caller can position it
- The extracted description is plain prose (no Markdown headings, no bullet lists, no code blocks)

---

### R4 — Place tool description above the tab block
**Priority:** P0  
**File:** `FamilyFileStitcher` (Step 4), and the template or assembly logic that builds the H2 tool section  
**Description:** The natural language description extracted in R3 must be written to the output file between the H2 tool heading and the opening of the tab block. The layout must be: `## Tool Name` → blank line → description paragraph → blank line → tab block.  
**Acceptance criteria:**
- Every generated tool section has the natural language description directly below the H2 heading
- The description paragraph is separated from the H2 heading by exactly one blank line
- The description paragraph is separated from the tab block by exactly one blank line
- No description text appears inside either tab

---

### R5 — Remove "(MCP)" branding instruction from system prompt
**Priority:** P0  
**File:** `mcp-tools/DocGeneration.Steps.ToolGeneration.Improvements/prompts/system-prompt.txt`, lines 5–7  
**Description:** Delete or rewrite the "Branding Rule — MCP Tools, Not CLI Commands" section that instructs the AI to inject "(MCP)" into tool descriptions. The replacement instruction must be neutral: tools are tools, not "MCP tools". The system prompt must not mention "(MCP)" as a string to inject into output.  
**Acceptance criteria:**
- `system-prompt.txt` contains no instruction to add "(MCP)" to descriptions
- The branding rule section heading and its body are removed
- The system prompt remains coherent and complete for all other AI guidance it provides

---

### R6 — Remove "(MCP)" branding instruction from user prompt template
**Priority:** P0  
**File:** `mcp-tools/DocGeneration.Steps.ToolGeneration.Improvements/prompts/user-prompt-template.txt`, line 21  
**Description:** Remove Item 13 ("These are MCP tools, not CLI commands") from the user prompt template's instruction list. Renumber remaining items if the list is numbered sequentially.  
**Acceptance criteria:**
- `user-prompt-template.txt` contains no instruction referencing "MCP tools, not CLI commands"
- The instruction list in the user prompt template is internally consistent (no gaps in numbering if numbered)
- All other prompt instructions are preserved verbatim

---

### R7 — Verify no other prompt files inject "(MCP)" branding
**Priority:** P0  
**File:** All files under `mcp-tools/DocGeneration.Steps.ToolGeneration.Improvements/prompts/`  
**Description:** Audit every prompt file in the prompts directory. If any other prompt file contains an instruction to inject "(MCP)" into descriptions or to brand tools as "MCP tools", remove that instruction.  
**Acceptance criteria:**
- A grep for `\(MCP\)` across all prompt files returns zero matches that are instructions to the AI (as opposed to example output being validated against)
- A grep for `MCP tools, not CLI` across all prompt files returns zero matches

---

### R8 — Update `CliTabWrapperTests.cs` for new tab order
**Priority:** P0  
**File:** `mcp-tools/DocGeneration.Steps.ToolFamilyCleanup.Tests/CliTabWrapperTests.cs`  
**Description:** Update the 4–5 existing assertions in `CliTabWrapperTests.cs` that validate tab order. Each assertion must now expect CLI tab content to appear before MCP tab content in the output string. Do not delete assertions; update expected values only.  
**Acceptance criteria:**
- All existing tests pass after the tab reorder change
- No test is deleted; existing test count is maintained or increased
- Each test that previously asserted MCP-first order now asserts CLI-first order
- Test descriptions/names accurately reflect CLI-first ordering

---

### R9 — Add `CliTabWrapperTests.cs` tests for description extraction
**Priority:** P0  
**File:** `mcp-tools/DocGeneration.Steps.ToolFamilyCleanup.Tests/CliTabWrapperTests.cs`  
**Description:** Add new test cases covering the description extraction and placement behavior introduced in R3 and R4. Tests must cover: description is absent from both tabs, description appears above tab block, description is plain prose, empty-description edge case does not break output.  
**Acceptance criteria:**
- At least 3 new tests covering description placement scenarios
- Test for description appearing in tab content: must FAIL before fix, PASS after
- Test for description appearing above tab block: must PASS after fix
- Edge case test: tool with no extractable description produces valid output (no crash, no empty paragraph)
- Edge case test: `NlpDescriptionExtractor` returns `null` → treated as empty description; pipeline does not throw a NullReferenceException; no description paragraph is emitted
- Edge case test: tool where CLI tab content is empty string → tab block still renders (both tabs present); description placement logic is unchanged
- Edge case test: MCP tab content consists solely of the description with no parameter table or examples → after description removal the MCP tab body is empty but the tab header is retained; no crash
- Edge case test: description exceeding 500 characters → placed verbatim above the tab block; no truncation or line-wrapping is inserted by the pipeline
- Failure mode: if `NlpDescriptionExtractor` throws, the exception propagates immediately (no silent swallow); no partial output is written

---

### R10 — Add regression test: no "(MCP)" in Step 3b output
**Priority:** P0  
**File:** New or existing test project covering Step 3b (tools/ output)  
**Description:** Add an automated test that runs the AI improvement step against a known input fixture and asserts the output does not contain the string "(MCP)". This test guards against future prompt changes that accidentally reintroduce branding.  
**Acceptance criteria:**
- Test fixture input is a representative tool description (can be synthetic)
- Test asserts output does not match the pattern `\(MCP\)` (case-sensitive)
- Test is included in the CI run
- Test is documented with a comment explaining why this assertion exists (guard against prompt regression)

---

### R11 — Update integration/snapshot tests for new output format
**Priority:** P1  
**File:** Any snapshot or golden-file tests under `DocGeneration.Steps.ToolFamilyCleanup.Tests/` or sibling test projects  
**Description:** If the pipeline has snapshot (golden file) tests that capture full tool-family page output, update those snapshots to reflect: (a) CLI tab first, (b) description above tabs, (c) no "(MCP)" in descriptions. Do not manually craft the snapshots — regenerate them by running the pipeline against existing fixtures after applying R1–R7 changes.  
**Acceptance criteria:**
- All snapshot tests pass after regeneration
- Snapshot diff between old and new confirms exactly the three structural changes (tab order, description placement, branding removal)
- No other content changes appear in the diff

---

### R12 — Validate `NlpDescriptionExtractor` output is prose-only
**Priority:** P1  
**File:** `NlpDescriptionExtractor` (class location to be confirmed by implementer in the shared or core project)  
**Description:** Confirm that `NlpDescriptionExtractor` returns plain prose (no Markdown headings, no bullet lists, no code blocks). If the extractor can return structured Markdown, add a sanitization step that strips Markdown formatting before the description is placed above the tabs.  
**Acceptance criteria:**
- `NlpDescriptionExtractor` output used in R4 placement is verified to be plain prose in at least one representative test
- If sanitization is added, a unit test covers that headings and bullets are stripped before output

---

### R13 — Confirm `FamilyFileStitcher` wiring for description placement
**Priority:** P1  
**File:** `FamilyFileStitcher` (Step 4)  
**Description:** Trace the call from `FamilyFileStitcher` → `CliTabWrapper.WrapWithTabs()` and confirm the description threading from R3/R4 is wired end-to-end. The stitcher must pass the extracted description into the H2 section for every tool it processes. If the stitcher iterates over a list of tools, the description must be per-tool, not shared across tools.  
**Acceptance criteria:**
- Every tool section in a generated tool-family page has its own distinct description (not a shared/repeated description)
- Tools with different descriptions produce different above-tab paragraphs
- The stitcher does not silently swallow or skip the description for any tool

---

### R14 — No change to CLI tab content or its assembly logic
**Priority:** P0  
**File:** `CliContentAssembler` and `tools-raw/` pipeline artifacts  
**Description:** The content assembled for the CLI tab must not be modified as part of this PRD. This is a position-only change for the CLI tab. No fields, formatting, or assembly steps in `CliContentAssembler` are in scope.  
**Acceptance criteria:**
- `CliContentAssembler` has no code changes in this PR
- `tools-raw/` artifacts are identical before and after the pipeline runs with the restructured code
- Diff of CLI tab content between old and new output is empty

---

### R15 — No change to MCP tab content structure (beyond description removal and branding)
**Priority:** P0  
**File:** Step 3b output (tools/), `CliTabWrapper.cs` MCP tab assembly  
**Description:** Beyond removing the tool description from the MCP tab (R3) and the branding "(MCP)" string (R5–R7), the MCP tab content structure must remain unchanged. Table formats, parameter names, example blocks, and annotation placement must be identical before and after.  
**Acceptance criteria:**
- Diff of MCP tab content between old and new output shows only: removed description paragraph and removed "(MCP)" strings
- No other MCP tab content changes appear in the diff

---

### R16 — Document the new output format contract
**Priority:** P1  
**File:** `README.md` or a `docs/output-format.md` in the target repo  
**Description:** Write or update documentation that specifies the structural contract of a generated tool-family page section. The document must describe: H2 heading → description paragraph → tab block (CLI first, MCP second) → annotation block. This becomes the reference for future contributors and the basis for regression tests.  
**Acceptance criteria:**
- Documentation exists and is reachable from the repo root (linked from README if in a subfolder)
- Documentation explicitly states CLI tab comes first
- Documentation explicitly states description lives above tabs
- Documentation notes that "(MCP)" branding is intentionally absent from descriptions

---

### R17 — Manual output review before merge
**Priority:** P1  
**Type:** Verification gate (no source code change; blocks merge if not satisfied)  
**File:** Pipeline output artifacts (not source code)  
**Description:** Before the PR is merged, a human reviewer must run the full pipeline end-to-end against at least one real tool family (e.g., `azure_storage` or equivalent) and visually inspect the rendered Learn Markdown output to confirm: CLI tab is first, description is above tabs, and "(MCP)" does not appear in any tool description. Attach a screenshot or output diff to the PR.  
**Acceptance criteria:**
- PR description contains a before/after diff or screenshot of at least one tool section
- The diff confirms all three structural changes
- Reviewer signs off that the output matches the format contract in R16
- **PASS:** PR description contains a diff showing CLI tab first, description above tabs, and zero `(MCP)` occurrences; at least one reviewer leaves an approval comment explicitly confirming the output
- **FAIL:** No diff is attached; or the diff shows content changes beyond the three expected structural changes (tab order, description move, branding removal); or no reviewer approval comment references the output

---

### R18 — CI gate: all tests pass before merge
**Priority:** P0  
**Type:** Verification gate (CI configuration; blocks merge if not satisfied)  
**File:** CI pipeline configuration (`.github/workflows/` or equivalent)  
**Description:** The CI pipeline must run the full test suite (including updated tests from R8, new tests from R9–R10, and updated snapshots from R11) before any PR touching `CliTabWrapper.cs`, `FamilyFileStitcher`, or the prompt files is merged. A failing test must block merge.  
**Acceptance criteria:**
- CI runs `dotnet test` (or equivalent) and reports results on the PR
- No new test-suppression comments (`// TODO`, `[Ignore]`, `[Skip]`) are introduced as part of this PR
- All tests in `DocGeneration.Steps.ToolFamilyCleanup.Tests` pass in CI
- **PASS:** CI status check is green; `dotnet test` exit code is 0; diff contains no `[Ignore]` or `[Skip]` annotations on tests introduced by this PR
- **FAIL:** Any test reports failure or is skipped via a suppression annotation introduced in this PR

---

## 5. Implementation Phases

Dependencies flow strictly: prompt changes (R5–R7) are independent of code changes (R1–R4), but both must be done before tests can be validated.

> **Phase dependency chain:** Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5. Each phase depends on the previous: snapshot regeneration in Phase 4 must use already-corrected prompts (Phase 1) and already-restructured output (Phases 2–3); documentation in Phase 5 can only describe verified behavior after Phase 4 confirms tests pass.

### Phase 1 — Prompt cleanup (R5, R6, R7)
Remove "(MCP)" branding instructions from all AI prompt files. This is the simplest change with no code dependencies. Doing it first means any regenerated fixtures in later phases will already be branding-free. **Must precede Phase 4:** snapshot regeneration uses the live prompts; running it before Phase 1 would embed branding in the new snapshots.

**Files:** `system-prompt.txt`, `user-prompt-template.txt`, all files under `prompts/`  
**Validation:** Grep confirms zero branding instructions remain.

### Phase 2 — Tab reorder (R1, R2, R14, R15)
Swap tab emission order in `WrapWithTabs()`. CLI tab content emitted first, MCP tab second. No content changes — position only. **Must precede Phase 3:** description extraction logic in Phase 3 assumes the tab ordering is finalized; validating placement against a still-reversed tab order would produce misleading test results.

**Files:** `CliTabWrapper.cs`  
**Validation:** Manual diff of one tool-family output confirms tab order inversion.

### Phase 3 — Description extraction and placement (R3, R4, R12, R13)
Extract natural language description from MCP tab content. Wire it through `FamilyFileStitcher` to appear above the tab block. Confirm `NlpDescriptionExtractor` returns prose-only output. **Must precede Phase 4:** unit tests added in Phase 4 assert the description placement contract; those tests cannot be written (or will trivially fail) before the extraction wiring exists.

**Files:** `CliTabWrapper.cs`, `FamilyFileStitcher`, `NlpDescriptionExtractor`  
**Validation:** Generated output shows description paragraph between H2 and tab block for each tool.

### Phase 4 — Test updates (R8, R9, R10, R11)
Update `CliTabWrapperTests.cs` assertions for new tab order. Add tests for description placement. Add "(MCP)" regression test. Regenerate any snapshot tests against updated output. **Must precede Phase 5:** documentation in Phase 5 describes the verified contract; it must reference passing tests, not aspirational ones.

**Files:** `CliTabWrapperTests.cs`, new test cases, snapshot files  
**Validation:** `dotnet test` passes with zero failures.

### Phase 5 — Documentation and review (R16, R17, R18)
Write/update output format contract documentation. Run full pipeline end-to-end. Attach diff to PR. Confirm CI gate is active. **Terminal phase:** depends on all prior phases being complete and tested.

**Files:** `README.md` or `docs/output-format.md`, PR description  
**Validation:** Human reviewer confirms output, CI passes.

---

## 6. Test Plan

### Tests to Update

| Test file | What changes |
|-----------|--------------|
| `CliTabWrapperTests.cs` — all tab-order assertions (4–5) | Expected output strings must reflect CLI-first tab order |
| Any snapshot / golden-file tests capturing tool-family page structure | Regenerate snapshots after Phases 1–3 are complete |

### New Tests to Add

| Test | Location | Covers |
|------|----------|--------|
| Description not in CLI tab | `CliTabWrapperTests.cs` | R3 |
| Description not in MCP tab | `CliTabWrapperTests.cs` | R3 |
| Description appears above tab block | `CliTabWrapperTests.cs` | R4 |
| Empty description edge case | `CliTabWrapperTests.cs` | R9 |
| Step 3b output contains no "(MCP)" | New test in Step 3b test project | R10 |
| `NlpDescriptionExtractor` returns prose-only | Unit test in extractor's test project | R12 |

### Test Coverage Mandate
No test deletions. Existing tests must either pass unchanged (if not affected) or be updated with corrected expected values. Test count after this PR must be equal to or greater than before.

### Requirements-to-Test Traceability Matrix

| Req | Test(s) | Location |
|-----|---------|----------|
| R1 | Updated tab-order assertions | `CliTabWrapperTests.cs` |
| R2 | Label-unchanged assertions | `CliTabWrapperTests.cs` |
| R3 | "Description not in CLI tab", "Description not in MCP tab" | `CliTabWrapperTests.cs` (new) |
| R4 | "Description appears above tab block" | `CliTabWrapperTests.cs` (new) |
| R5 | No `(MCP)` in Step 3b output; grep at review | R10 test + R7 grep |
| R6 | No `(MCP)` in Step 3b output; grep at review | R10 test + R7 grep |
| R7 | Grep across all prompt files | CI / manual grep |
| R8 | Updated tab-order assertions | `CliTabWrapperTests.cs` |
| R9 | Empty/null/no-CLI/description-only/long-description edge case tests | `CliTabWrapperTests.cs` (new) |
| R10 | Step 3b output contains no `(MCP)` | New test, Step 3b project |
| R11 | Snapshot regeneration + diff review | Snapshot test suite |
| R12 | `NlpDescriptionExtractor` prose-only unit test | Extractor test project |
| R13 | Multi-tool fixture: per-tool distinct descriptions | Integration test |
| R14 | Snapshot diff: CLI tab content unchanged | Snapshot diff review |
| R15 | Snapshot diff: MCP tab shows only description removal + branding removal | Snapshot diff review |
| R16 | Doc file exists and is linked | Manual review (Phase 5) |
| R17 | Before/after diff attached; reviewer approval | PR verification gate |
| R18 | `dotnet test` exit 0; no suppressions in diff | CI gate |

---

## 7. Success Criteria

The pipeline restructure is complete and correct when ALL of the following are true:

| # | Criterion | How to verify |
|---|-----------|---------------|
| SC1 | CLI tab is the first tab in every generated tool-family page | Grep output artifacts for tab block order |
| SC2 | MCP tab is the second tab in every generated tool-family page | Grep output artifacts for tab block order |
| SC3 | Natural language description appears above the tab block for every tool | Grep output artifacts for H2 → description → tab pattern |
| SC4 | No tool description contains the string "(MCP)" | `Select-String -Pattern "\(MCP\)" -Path artifacts/**` returns zero matches |
| SC5 | All unit tests in `DocGeneration.Steps.ToolFamilyCleanup.Tests` pass | `dotnet test` exit code 0 |
| SC6 | No regression on PRs #575–579 fixes (annotation placement, NLP param names, tab structure, config gen, env setup) | Existing regression tests from `tool-family-regression-protection.md` pass |
| SC7 | Output format contract is documented | `docs/output-format.md` (or README section) exists and is linked |
| SC8 | PR contains before/after diff reviewed by a human | PR description includes attached diff |

---

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Tab reorder breaks an undiscovered tab-anchor deep link in published Learn articles | Low | Medium | Search published content for CLI/MCP tab anchor patterns before merging; if found, coordinate with Learn team |
| Description extraction produces empty string for some tools (extractor finds nothing) | Medium | Low | R9 edge case test + fallback: emit no description paragraph rather than an empty one; never emit a blank `<p></p>` |
| AI prompt changes cause unexpected style drift in regenerated tool descriptions | Medium | Medium | Run prompt changes against 5–10 representative tools and diff descriptions before committing; revert if quality drops |
| Snapshot test regeneration introduces hidden content regressions | Low | High | Manually inspect snapshot diffs line-by-line; diff must show only the three expected changes (tab order, description move, branding removal) |
| Phase ordering mistake: snapshots regenerated before prompt cleanup → branding still present in snapshots | Medium | Medium | Enforce Phase 1 before Phase 4 strictly; code review checklist item |
| `NlpDescriptionExtractor` returns Markdown-structured content for some tools | Low | Low | R12 sanitization step; unit test covers at least one heading-containing case |
| FamilyFileStitcher iterates tools in non-deterministic order causing description cross-contamination | Low | High | R13 acceptance criteria require per-tool descriptions; add assertion that tool A description ≠ tool B description in multi-tool fixture |

---

## 9. Edge Cases & Failure Modes

| Scenario | Expected behavior | Covered by |
|----------|------------------|------------|
| `NlpDescriptionExtractor` returns `null` | Treat as empty description; emit no description paragraph; no crash | R9 test |
| `NlpDescriptionExtractor` throws | Exception propagates immediately; no partial output written | R9 test |
| CLI tab content is empty string | Tab block still renders (both tabs present); description placement unchanged | R9 test |
| MCP tab content = description only (no param table/examples) | MCP tab header retained; body is empty after description removal; no crash | R9 test |
| Description exceeds 500 characters | Placed verbatim; no truncation or pipeline-inserted line breaks | R9 test |
| Tool with no CLI content on a single-tool page | Same behavior as multi-tool page; no special-casing needed | R13 / R9 |
| Multi-tool page: tool A and tool B share same description string | Each tool section emits its own copy; no cross-contamination | R13 |
| Model behavior drift after prompt changes (accidental reintroduction of "(MCP)") | R10 regression test catches it in CI | R10 |
| Snapshot regenerated before prompt cleanup | Phase ordering rule prevents this; Phase 1 is gated prerequisite | Phase 1 note |

---

*PRD drafted by Nigel-prd-planning on 2026-05-28. Pending full 11-agent team review using the 8-dimension scoring system.*
