# Test Strategy — Azure MCP Documentation Generation Pipeline

**Author:** Parker (QA / Tester)  
**Date:** 2026-03-25  
**Status:** Active  
**Triggered by:** #202 requirements review — "Draft a test strategy document before implementation to prevent method-level testing gaps that could hide pipeline-level regressions."

---

## 1. Current Test Landscape

### 1.1 Test Suite Baseline

| Metric | Value |
|--------|-------|
| **Total tests** | ~1,100 |
| **Active test projects** | 17 (in `mcp-doc-generation.sln`) |
| **Deprecated test projects** | 17 (legacy naming, not in .sln) |
| **Framework** | xUnit (.NET 9) |
| **Quality gate** | `dotnet test mcp-doc-generation.sln` |
| **Last known green** | 1,061 tests passing (2026-03-24, PRs #200/#201) |

### 1.2 Active Test Projects

| Test Project | Approx Tests | Coverage Target |
|-------------|-------------|-----------------|
| `DocGeneration.Steps.HorizontalArticles.Tests` | ~410 | Step 6: horizontal article generation |
| `DocGeneration.Core.TextTransformation.Tests` | ~305 | Text cleanup, transformation rules |
| `DocGeneration.PipelineRunner.Tests` | ~210 | PipelineRunner, step registry, validators, integration |
| `DocGeneration.Steps.Bootstrap.BrandMappings.Tests` | ~95 | Brand mapping validation, merge groups |
| `DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests` | ~42 | Annotations, parameters, template regression |
| `DocGeneration.Steps.SkillsRelevance.Tests` | ~13 | Step 5: skills relevance scoring |
| `DocGeneration.Steps.ToolFamilyCleanup.Validation.Tests` | ~10 | Step 4 post-assembly validation |
| `DocGeneration.Steps.ToolFamilyCleanup.Tests` | varies | Stitcher, contractions, acronyms, P1 regression |
| `DocGeneration.Steps.ExamplePrompts.Validation.Tests` | ~3 | Prompt validation rules |
| `DocGeneration.Steps.ExamplePrompts.Generation.Tests` | varies | Deterministic prompt generation |
| `DocGeneration.Steps.Bootstrap.CommandParser.Tests` | varies | CLI command parsing |
| `DocGeneration.Steps.Bootstrap.E2eTestPromptParser.Tests` | varies | E2E test prompt parsing |
| `DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Tests` | varies | Tool matching, param extraction |
| `DocGeneration.Steps.ToolGeneration.Improvements.Tests` | varies | Template label protection, mcpcli markers |
| `DocGeneration.Core.TemplateEngine.Tests` | ~2 | Handlebars engine basics |
| `DocGeneration.Core.Shared.Tests` | varies | ParameterCoverageChecker, headings, frontmatter |
| `DocGeneration.Core.GenerativeAI.Tests` | varies | AI prompt generation, log files |

### 1.3 Deprecated Test Projects (NOT in .sln)

These live under `mcp-tools/` with old naming conventions and are no longer built or run:

`AzmcpCommandParser.Tests`, `BrandMapperValidator.Tests`, `CSharpGenerator.Tests`, `E2eTestPromptParser.Tests`, `ExamplePromptGeneratorStandalone.Tests`, `ExamplePromptValidator.Tests`, `GenerativeAI.Tests`, `HorizontalArticleGenerator.Tests`, `PipelineRunner.Tests`, `RelatedSkillsGenerator.Tests`, `Shared.Tests`, `SkillList.Tests`, `SkillsRelevance.Tests`, `TemplateEngine.Tests`, `TextTransformation.Tests`, `ToolFamilyValidator.Tests`, `ToolGeneration_Improved.Tests`

> ⚠️ **Risk:** These may contain valuable test logic that was never migrated to the new `DocGeneration.*` namespace. A migration audit is recommended.

### 1.4 External Validation

| Tool | Location | What It Checks |
|------|----------|----------------|
| `verify-quantity/index.js` | Node.js script | File existence for annotations, parameters, example-prompts, param-and-annotation, complete-tools across all namespaces |
| `generated-validated-*` folders | Repo root (23 of ~49 namespaces) | Per-namespace validated output snapshots |

---

## 2. Test Pyramid

```
                    ┌──────────────┐
                    │   E2E Tests  │  ← Full namespace generation (few)
                    │  verify-qty  │    verify-quantity, generated-validated-*
                   ─┼──────────────┼─
                  │ Integration    │  ← Step contract tests, template rendering,
                  │  Tests         │    pipeline dry-run, post-assembly validation
                 ─┼────────────────┼─
               │     Unit Tests     │  ← Helpers, generators, parsers, validators
               │  (~1,100 today)    │    ParameterCoverageChecker, StripFrontmatter,
               └────────────────────┘    WrapExampleValues, BrandMapper, etc.
```

### Current Distribution Problem

The pyramid is **bottom-heavy in unit tests but hollow in the middle**. We have strong method-level coverage for helpers but weak coverage for:
- Step-level contract validation (inputs → outputs)
- Template rendering with realistic data
- Cross-step data integrity (Step 2 output → Step 3 input)

This is exactly the gap that PRs #200/#201 exposed: method tests pass even when template changes are reverted.

---

## 3. Test Categories

### 3.1 Method-Level Unit Tests

**What:** Tests for individual helper methods with isolated inputs/outputs.  
**Where:** All `*.Tests` projects under `mcp-tools/`.  
**Current state:** Strong. ~1,100 tests covering core logic.

**Key classes with test coverage:**
| Class | Test File | Status |
|-------|-----------|--------|
| `ParameterCoverageChecker` | `DocGeneration.Core.Shared.Tests/ParameterCoverageCheckerTests.cs` | ✅ Tested (ConvertToSlug, RemoveMarkup, placeholder detection) |
| `StripFrontmatter` | `Annotations.Tests/PageGeneratorStripFrontmatterTests.cs` | ✅ Tested (77+ cases) |
| `WrapExampleValues` | `Annotations.Tests/WrapExampleValuesTests.cs` | ✅ Tested (11 cases, regex edge cases) |
| `HandlebarsTemplateEngine` | `Core.TemplateEngine.Tests/HandlebarsTemplateEngineTests.cs` | ⚠️ Minimal (2 basic tests) |
| `BrandMapper` | `Bootstrap.BrandMappings.Tests/BrandMapperValidatorTests.cs` | ✅ Tested |
| `MergeGroupValidator` | `Bootstrap.BrandMappings.Tests/MergeGroupValidatorTests.cs` | ✅ Tested |
| `AnnotationGenerator` | `Annotations.Tests/AnnotationGeneratorTests.cs` | ✅ Tested |
| `FamilyFileStitcher` | `ToolFamilyCleanup.Tests/P1BugRegressionTests.cs` | ⚠️ Indirect (integration-style) |
| `ComposedToolGeneratorService` | `PipelineRunner.Tests/Unit/ComposedToolGeneratorServiceTests.cs` | ✅ Tested |
| `DeterministicH2HeadingGenerator` | `Core.Shared.Tests/DeterministicH2HeadingGeneratorTests.cs` | ✅ Tested |
| `TextCleanup` | `DocGeneration.Core.NaturalLanguage` | ❌ **No tests** |

**Priority gaps:**
- `TextCleanup` (NaturalLanguage) — zero coverage
- `HandlebarsTemplateEngine` — only 2 basic tests; needs helper registration, error handling, partial tests
- `FamilyFileStitcher` — needs dedicated unit tests (currently only tested indirectly via P1 regression tests)

### 3.2 Template Rendering Tests

**What:** Tests that load actual `.hbs` files from `mcp-tools/templates/` and render them with controlled data via `HandlebarsTemplateEngine.ProcessTemplateString()`.  
**Where:** `DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests/`  
**Current state:** Nascent. Established in PRs #200/#201. Pattern exists but coverage is thin.

**Templates requiring tests (10 total):**

| Template | Has Tests | Priority |
|----------|-----------|----------|
| `tool-family-page.hbs` | ✅ 13 regression tests (PRs #200/#201) | — |
| `tool-complete-template.hbs` | ❌ | 🔴 High — generates the main tool reference page |
| `parameter-template.hbs` | ❌ | 🔴 High — generates parameter tables |
| `annotation-template.hbs` | ❌ | 🟠 Medium |
| `example-prompts-template.hbs` | ❌ | 🟠 Medium |
| `param-annotation-template.hbs` | ❌ | 🟡 Low |
| `service-start-option.hbs` | ❌ | 🟡 Low |
| `common-tools.hbs` | ❌ | 🟡 Low |
| `area-template.hbs` | ❌ | 🟡 Low |

**Required test pattern (per AD-019):**
```csharp
[Fact]
public void Template_RendersWith_ExpectedOutput()
{
    var templateContent = File.ReadAllText("templates/tool-complete-template.hbs");
    var engine = HandlebarsTemplateEngine.CreateEngine();
    var data = new { /* realistic pipeline data */ };
    
    var result = engine.ProcessTemplateString(templateContent, data);
    
    Assert.Contains("expected-output", Normalize(result));
    // Must FAIL if template reverted
}
```

### 3.3 Step Contract Tests

**What:** Tests that verify each pipeline step's inputs produce expected outputs — the contract between steps.  
**Where:** Should be in each step's `.Tests` project.  
**Current state:** **Critical gap.** No step has formal contract tests.

**Step contracts (from AD-008):**

| Step | Reads From | Writes To | Contract Tests | Status |
|------|------------|-----------|----------------|--------|
| 0 (Bootstrap) | `cli/` | metadata, brand mappings | None | ❌ |
| 1 (AnnotationsParametersRaw) | `cli/`, metadata | `annotations/`, `parameters/`, `tools-raw/` | None | ❌ |
| 2 (ExamplePrompts) | `cli/`, annotations, parameters | `example-prompts/` | None | ❌ |
| 3 (ToolGeneration) | tools-raw, annotations, parameters, example-prompts | `tools-composed/`, `tools/` | None | ❌ |
| 4 (ToolFamilyCleanup) | `tools/` | `tool-family/`, `tool-family-metadata/`, `tool-family-related/` | ⚠️ Partial (post-assembly validator) | 🟡 |
| 5 (SkillsRelevance) | `tools/` | `skills-relevance/` | None | ❌ |
| 6 (HorizontalArticles) | `tools/` | `horizontal-articles/` | None | ❌ |

**What contract tests should verify:**
1. Given valid step N inputs on disk → step N produces expected output files
2. Given corrupted/missing step N-1 outputs → step N fails with clear error (not silent degradation)
3. Output file structure matches the schema expected by step N+1

### 3.4 Cross-Namespace Regression Tests

**What:** Verify that ALL namespaces produce correct output — not just the 3-4 we test manually.  
**Where:** `verify-quantity/index.js` (file existence), `generated-validated-*` (snapshots)  
**Current state:** Partial.

**Current coverage:**
- `verify-quantity/`: Checks file **existence** only (annotations, parameters, example-prompts, param-and-annotation, complete-tools). Does NOT validate file **content**.
- `generated-validated-*`: 23 of ~49 namespaces have validated snapshots.

**Namespaces in `brand-to-server-mapping.json`:** ~49 (previously cited as 52; actual count is 49 + 2 merge group entries = 49 unique service names with `monitor`+`workbooks` forming 1 merge group).

**Missing namespaces (no validated snapshots):**
The following namespaces have NO `generated-validated-*` folder: `acr`, `aks`, `appconfig`, `applicationinsights`, `azureterraformbestpractices`, `bicepschema`, `communication`, `compute` (wait—has one), `confidentialledger`, `datadog`, `deviceregistry`, `eventgrid`, `eventhubs`, `extension`, `foundry`, `functionapp`, `functions`, `get`, `get_azure_bestpractices`, `grafana`, `kusto`, `loadtesting`, `managedlustre`, `marketplace`, `mysql`, `policy`, `quota`, `redis`, `role`, `servicebus`, `servicefabric`, `signalr`, `speech`, `storagesync`, `subscription`, `virtualdesktop`.

> That's ~26 namespaces with zero validated output — over half the corpus.

### 3.5 AI Output Validation Tests

**What:** Structural checks on content generated by Azure OpenAI (Steps 2, 3b, 4, 6).  
**Where:** `DocGeneration.Steps.ExamplePrompts.Validation/` (Step 2 only)  
**Current state:** **Only Step 2 has AI output validation.** Steps 3, 4, 6 have none.

**What should be validated:**
- JSON parse success (Step 6 horizontal articles)
- Token truncation detection (all AI steps)
- Template token leakage (`<<<TPL_LABEL_N>>>` in final output, per AD-002)
- Structural completeness (all expected sections present)
- No hallucinated tool names or parameters

**Existing validators:**
| Validator | Step | What It Checks |
|-----------|------|----------------|
| `CodeBasedPromptValidator` | 2 | Deterministic prompt structure validation |
| `PromptValidator` | 2 | AI-assisted prompt quality validation |
| `PromptTemplateValidator` | 2 | Template placeholder validation |
| `ToolFamilyPostAssemblyValidator` | 4 | Post-assembly structural validation |

**Missing validators:**
| Step | What's Missing |
|------|---------------|
| 3 (Composition) | No validator for composed tool output — template token leakage risk (AD-002) |
| 3 (Improvements) | No validator for AI-improved tool content |
| 6 (HorizontalArticles) | No validator for horizontal article JSON/content — AI JSON parse failures (AD-002) |

### 3.6 Post-Assembly Validation Tests

**What:** Checks on the final merged/assembled output (frontmatter, link integrity, cross-namespace consistency).  
**Where:** `ToolFamilyPostAssemblyValidator` (Step 4 only), `ToolFamilyValidator.Tests`  
**Current state:** Step 4 has partial coverage. Merge output has none.

**What should be validated post-assembly:**
- Frontmatter fields: `title`, `description`, `ms.topic`, `ms.date`, `ms.service` (per #155)
- Link format: no `~/` DocFX paths (per AD-017)
- `@mcpcli` marker presence and positioning
- MCP acronym expansion on first body mention
- No duplicate `## Examples` sections (per #153, #140)
- Merge group integrity: primary has frontmatter, secondary contributes tool H2s only (per AD-011)
- Contraction consistency (per #145)
- Brand name normalization (CosmosDB → Azure Cosmos DB, per #141)

---

## 4. Test Data Strategy

### 4.1 Principle: Real Data, Not Synthetic

Per AD-010, tests must use **realistic inputs sourced from actual pipeline output**. Not `"hello world"`, not `"test-tool"` — actual namespace data.

### 4.2 Reference Namespaces

Each test tier uses reference namespaces chosen for specific characteristics:

| Tier | Namespace | Why |
|------|-----------|-----|
| **Small** | `advisor` | 3-4 tools, simple parameters, fast feedback loop |
| **Medium** | `storage` | ~15 tools, variety of parameter types, good middle ground |
| **Large** | `compute` | 30+ tools, complex parameters, VM creation/management |
| **Complex** | `cosmos` | Missing tools (#148), database-specific parameters, branding normalization needed |
| **AI-heavy** | `wellarchitectedframework` | Token budget issues (#158), long AI-generated content |
| **Merge group** | `monitor` + `workbooks` | Multi-namespace merge (AD-011), primary/secondary roles |
| **Edge case** | `get_azure_bestpractices` | Underscore in name, unusual command structure |
| **Edge case** | `foundryextensions` | Step 2 checker too strict (#161), complex parameter patterns |

### 4.3 Test Data Sources

| Test Category | Data Source |
|---------------|------------|
| Unit tests | Hardcoded realistic strings from `generated-validated-*/` files |
| Template tests | JSON fixtures derived from actual `tools/`, `annotations/`, `parameters/` outputs |
| Step contract tests | Snapshot directories from `generated-validated-advisor/` (small, fast) |
| Cross-namespace tests | Full `generated/` output from a clean pipeline run |
| Regression baselines | `generated-validated-*` folders (23 namespaces currently) |

### 4.4 Test Data Anti-Patterns

- ❌ `"test-description"`, `"sample tool"`, `"foo bar"` — meaningless
- ❌ Empty parameter lists — most tools have 3-10 parameters
- ❌ Single-tool families — most families have 2-8 tools
- ❌ ASCII-only content — real content has emoji (annotation flags), pipes, YAML frontmatter

---

## 5. Regression Detection

### 5.1 Current Approach: verify-quantity

`verify-quantity/index.js` checks **file existence** across 5 output categories for all tools parsed from `generated/cli/cli-output.json`. This catches:
- ✅ Missing files (tool was skipped entirely)
- ✅ New tools added to CLI but not generated
- ❌ Content regressions (file exists but content is wrong)
- ❌ Structural regressions (sections missing, formatting broken)
- ❌ Cross-namespace inconsistencies

### 5.2 Proposed Approach: Baseline Fingerprinting

**Concept:** For each `generated-validated-*` namespace, compute a fingerprint of key structural properties. Compare against the fingerprint after each pipeline run.

**Fingerprint schema:**
```json
{
  "namespace": "advisor",
  "timestamp": "2026-03-25T00:00:00Z",
  "toolCount": 4,
  "fileChecks": {
    "annotations": { "count": 4, "totalSizeBytes": 12340 },
    "parameters": { "count": 4, "totalSizeBytes": 8900 },
    "examplePrompts": { "count": 4, "totalSizeBytes": 6200 },
    "tools": { "count": 4, "totalSizeBytes": 45000 },
    "toolFamily": { "count": 1, "totalSizeBytes": 52000 }
  },
  "structuralChecks": {
    "allToolsHaveFrontmatter": true,
    "allToolsHaveMcpcliMarker": true,
    "noLeakedTemplateTokens": true,
    "noDocfxPaths": true,
    "mcpAcronymExpanded": true
  }
}
```

**Benefits over current approach:**
- Detects content regressions (file size changed significantly)
- Detects structural regressions (missing frontmatter, leaked tokens)
- Runs in seconds (no AI calls needed)
- Version-controlled baselines in `generated-validated-*/fingerprint.json`

### 5.3 Migration Path

1. **Phase 1:** Add structural checks to `verify-quantity/index.js` (file content validation, not just existence)
2. **Phase 2:** Generate fingerprints for all 23 validated namespaces
3. **Phase 3:** Integrate fingerprint comparison into `dotnet test` via a dedicated `RegressionFingerprint.Tests` project
4. **Phase 4:** Expand to all 49 namespaces as validated snapshots are created

---

## 6. AD-010 Compliance: Behavioral Tests That Fail on Revert

### 6.1 The Rule

Per AD-010: **Every fix must include tests that would definitively catch the bug if it regressed.** Tests that pass regardless of whether the fix is present are blocked in review.

### 6.2 Enforcement Mechanism

**PR Review Checklist (Parker validates):**

- [ ] Identify the behavior change in the PR
- [ ] Locate the test(s) that cover this behavior
- [ ] Mentally (or actually) revert the code change — would any test fail?
- [ ] If no test would fail → **REJECT** with specific guidance
- [ ] Verify test uses realistic inputs (not trivial data)
- [ ] Verify test asserts on observable behavior (not implementation details)

**The Revert Test:**
```bash
# For any PR, this sequence must produce at least 1 test failure:
git stash          # Stash the fix
dotnet test        # Run tests — at least one MUST fail
git stash pop      # Restore the fix
dotnet test        # All tests pass again
```

### 6.3 Anti-Patterns Caught by AD-010

| Anti-Pattern | Example | Why It Fails AD-010 |
|-------------|---------|---------------------|
| Reflection-only test | `Assert.NotNull(typeof(Foo).GetMethod("Bar"))` | Passes whether bug is fixed or not |
| Smoke test | `var result = service.Process(); Assert.True(result.Success)` | Doesn't verify the specific behavior change |
| Wrong-level test | Testing `WrapExampleValues()` but not the template that calls it | Template revert goes undetected (PRs #200/#201 lesson) |
| Synthetic data | `"test input"` instead of actual pipeline content | Doesn't exercise the real failure path |

---

## 7. AD-019 Compliance: Template Regression Tests

### 7.1 The Rule

Per AD-019: **Any PR that modifies a `.hbs` template file must include at least one test that loads the actual template, renders it, and asserts on the specific output change.**

### 7.2 Required Test Pattern

```csharp
[Fact]
public void ToolFamilyPage_McpcliMarker_HasAtPrefix()
{
    // Load ACTUAL template from disk
    var templatePath = Path.Combine(TestContext.TemplatesRoot, "tool-family-page.hbs");
    var templateContent = File.ReadAllText(templatePath);
    
    // Create engine with all registered helpers
    var engine = HandlebarsTemplateEngine.CreateEngine();
    
    // Use realistic data from pipeline output
    var data = new
    {
        areaName = "Azure Kubernetes Service",
        mcpcliPrefix = "@mcpcli",
        tools = new[] { /* realistic tool data */ }
    };
    
    // Render
    var result = engine.ProcessTemplateString(templateContent, data);
    var normalized = result.Replace("\r\n", "\n");
    
    // Assert on the specific change — this FAILS if template is reverted
    Assert.Contains("@mcpcli", normalized);
    Assert.DoesNotContain("\nazmcp ", normalized); // Old format must not appear
}
```

### 7.3 Template Test Location

All template regression tests live in `DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests/` (where `ToolFamilyPageTemplateRegressionTests.cs` and `AnnotationTemplateRegressionTests.cs` already exist).

**Convention:** `{TemplateName}TemplateRegressionTests.cs`

### 7.4 Cross-Platform Normalization

Template test output must normalize line endings:
```csharp
private static string Normalize(string s) => s.Replace("\r\n", "\n");
```
This prevents false failures on Windows (lesson from PR #201).

---

## 8. Test Execution

### 8.1 Quality Gate

```bash
dotnet test mcp-doc-generation.sln
```

This is the **single command** that must pass before any PR merges (per AD-005). It runs all 17 active test projects.

### 8.2 Build + Test Pipeline

```bash
# Full quality gate
dotnet build mcp-doc-generation.sln --no-restore
dotnet test mcp-doc-generation.sln --no-build

# Targeted test run (single project)
dotnet test mcp-tools/DocGeneration.Steps.ToolFamilyCleanup.Tests/

# With verbosity for debugging
dotnet test mcp-doc-generation.sln --verbosity normal --logger "console;verbosity=detailed"
```

### 8.3 Parallel Test Execution

xUnit runs tests in parallel by default within each assembly. Cross-assembly parallelism is handled by `dotnet test` launching multiple test hosts.

**Constraint:** Tests that read/write to shared `generated-validated-*` directories must use `[Collection("NamespaceGeneration")]` to prevent file system conflicts.

### 8.4 CI Integration

**Expected CI pipeline:**
1. `dotnet restore mcp-doc-generation.sln`
2. `dotnet build mcp-doc-generation.sln --no-restore`
3. `dotnet test mcp-doc-generation.sln --no-build --results-directory ./test-results --logger trx`
4. (Optional) `node verify-quantity/index.js` — cross-namespace file existence check

**PR merge blockers:**
- Any test failure in `dotnet test`
- Missing documentation (AD-004)
- Missing behavioral test for code changes (AD-010)
- Missing template test for `.hbs` changes (AD-019)

### 8.5 Test Execution Times

Tests should complete in under 60 seconds total (no AI calls, no disk I/O to generated folders in unit tests). Integration tests that touch disk should be in separate test classes marked with `[Trait("Category", "Integration")]`.

---

## 9. Gap Analysis

### 9.1 Production Code With Zero Test Projects

These production code projects have **no corresponding test project at all:**

| Project | Pipeline Step | Risk Level |
|---------|---------------|------------|
| `DocGeneration.Steps.Bootstrap.CliAnalyzer` | Step 0 | 🟠 Medium — parses CLI output, errors here corrupt all downstream steps |
| `DocGeneration.Steps.AnnotationsParametersRaw.RawTools` | Step 1 | 🟠 Medium — generates raw tool markdown |
| `DocGeneration.Steps.ToolGeneration.Composition` | Step 3 | 🔴 High — composes tools from multiple sources, template token leakage risk |
| `DocGeneration.Core.NaturalLanguage` | Shared | 🟠 Medium — `TextCleanup` class has zero coverage |

### 9.2 Pipeline Steps Without Post-Validators

Per Morgan's review, only Step 4 has a `PostValidator` (`ToolFamilyPostAssemblyValidator`). The other 6 steps have none:

| Step | Missing Validator | Risk (from AD-002) |
|------|-------------------|---------------------|
| **Step 0** (Bootstrap) | No validation of CLI parse output | Medium — corrupted metadata propagates |
| **Step 1** (AnnotationsParametersRaw) | No validation of annotation/parameter files | Medium — malformed files feed Steps 2-3 |
| **Step 2** (ExamplePrompts) | Internal validation only (not `IPostValidator`) | Low — has `PromptValidator` but not wired as post-validator |
| **Step 3** (ToolGeneration) | **No validator at all** | 🔴 **Critical** — template token leakage (`<<<TPL_LABEL_N>>>`) goes undetected |
| **Step 5** (SkillsRelevance) | No validation of skills output | Low — isolated step with no downstream dependencies |
| **Step 6** (HorizontalArticles) | **No validator at all** | 🔴 **Critical** — AI JSON parse failures go undetected (AD-002) |

### 9.3 Template Coverage Gaps

Only 1 of 10 templates has regression tests (`tool-family-page.hbs`). The remaining 9 are untested.

### 9.4 Cross-Namespace Coverage

- 23 of 49 namespaces have `generated-validated-*` snapshots (~47%)
- 26 namespaces have zero validated output — regressions in those namespaces are invisible
- `verify-quantity/` checks file existence but not content quality

### 9.5 Step Contract Tests

Zero step contract tests exist. No test verifies that Step N's output is valid input for Step N+1. This means:
- Step 2 could produce malformed example prompts that silently corrupt Step 3
- Step 3 could produce tools with leaked template tokens that Step 4 doesn't catch
- Bootstrap could produce corrupted metadata that all 6 steps silently accept

### 9.6 Merge Infrastructure Tests

`NamespaceMerger` has tests in `ToolFamilyCleanup.Tests/NamespaceMergerTests.cs`, but:
- Only 1 merge group exists (`monitor` + `workbooks`)
- Edge cases untested: 3+ namespace merge, conflicting frontmatter, missing primary

---

## 10. Priority Roadmap

### Phase 1: Critical Gaps (Week 1-2)

**Goal:** Cover the riskiest blind spots — steps with no validators, the composition step with no tests.

| # | Task | Risk Mitigated | Effort |
|---|------|----------------|--------|
| 1 | Create `DocGeneration.Steps.ToolGeneration.Composition.Tests` | Step 3 template token leakage (AD-002) | Medium |
| 2 | Add Step 3 `IPostValidator` — check for `<<<TPL_LABEL_N>>>` in output | Template token leakage in final output | Small |
| 3 | Add Step 6 `IPostValidator` — validate horizontal article JSON structure | AI JSON parse failures (AD-002) | Small |
| 4 | Add template regression tests for `tool-complete-template.hbs` | Main tool page regressions | Medium |
| 5 | Add template regression tests for `parameter-template.hbs` | Parameter table regressions | Medium |

### Phase 2: Step Contracts (Week 3-4)

**Goal:** Validate the data flow between pipeline steps.

| # | Task | Risk Mitigated | Effort |
|---|------|----------------|--------|
| 6 | Step 1→2 contract test: annotation/parameter files valid for prompt generation | Silent data corruption | Medium |
| 7 | Step 2→3 contract test: example prompts feed correctly into composition | Malformed prompts corrupt tools | Medium |
| 8 | Step 3→4 contract test: composed tools valid for family cleanup | Missing tools, structural issues | Medium |
| 9 | Bootstrap→All contract test: metadata schema validation | Corrupted metadata propagates everywhere | Medium |

### Phase 3: Cross-Namespace Expansion (Week 5-6)

**Goal:** Extend validated snapshots to all 49 namespaces.

| # | Task | Risk Mitigated | Effort |
|---|------|----------------|--------|
| 10 | Generate `generated-validated-*` for remaining 26 namespaces | Invisible regressions in untested namespaces | Large |
| 11 | Add content validation to `verify-quantity/` (not just file existence) | Content regressions | Medium |
| 12 | Implement baseline fingerprinting (Section 5.2) | Structural regressions | Medium |

### Phase 4: Hardening (Week 7-8)

**Goal:** Fill remaining gaps and establish ongoing maintenance.

| # | Task | Risk Mitigated | Effort |
|---|------|----------------|--------|
| 13 | Create `DocGeneration.Steps.Bootstrap.CliAnalyzer.Tests` | CLI parse errors | Small |
| 14 | Create `DocGeneration.Steps.AnnotationsParametersRaw.RawTools.Tests` | Raw tool generation errors | Small |
| 15 | Add `TextCleanup` tests in `DocGeneration.Core.NaturalLanguage.Tests` | Text cleanup regressions | Small |
| 16 | Template tests for remaining 7 `.hbs` files | Template regressions | Medium |
| 17 | Audit deprecated test projects for unmigrated test logic | Lost coverage from rename | Small |
| 18 | Add `[Trait("Category", "Integration")]` to all disk-touching tests | Test isolation | Small |

---

## Appendix A: Test File Naming Conventions

| Category | Pattern | Example |
|----------|---------|---------|
| Unit test | `{ClassName}Tests.cs` | `ParameterCoverageCheckerTests.cs` |
| Template regression | `{TemplateName}TemplateRegressionTests.cs` | `ToolFamilyPageTemplateRegressionTests.cs` |
| Step contract | `{StepName}ContractTests.cs` | `ToolGenerationContractTests.cs` |
| Integration | `{Feature}IntegrationTests.cs` | `DryRunIntegrationTests.cs` |
| P1 regression | `P1BugRegressionTests.cs` | (catch-all for critical bug regression tests) |

## Appendix B: Decision References

| Decision | Summary | Impact on Testing |
|----------|---------|-------------------|
| AD-007 | TDD: tests first, then code | All new code starts with failing tests |
| AD-010 | Tests must catch bug on revert | Behavioral tests required, not structural |
| AD-019 | Template changes need template-level tests | `.hbs` changes need rendering tests |
| AD-011 | Post-assembly merge pattern | Merge output needs dedicated validation |
| AD-018 | Consolidate StripFrontmatter | Tests should cover all 3 implementations until consolidated |
| AD-020 | Pipeline architecture assessment | Risk priorities align with test priority roadmap |

## Appendix C: Quick Reference — Running Tests

```bash
# Full quality gate
dotnet test mcp-doc-generation.sln

# Single project
dotnet test mcp-tools/DocGeneration.Steps.ToolFamilyCleanup.Tests/

# With filter
dotnet test mcp-doc-generation.sln --filter "FullyQualifiedName~ParameterCoverage"

# List all tests
dotnet test mcp-doc-generation.sln --list-tests

# Cross-namespace file check
cd verify-quantity && node index.js
```
