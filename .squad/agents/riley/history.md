# Riley's History

## Learnings

### 2026-03-30: .NET Project Consolidation Architecture Review — APPROVED WITH CHANGES

**Verdict:** APPROVE WITH CHANGES (Actions 1-6 approved; Action 7 rejected on architectural grounds)

**Key Conditions Riley Required:**
1. **Action 7 (Bootstrap consolidation) REJECTED indefinitely** — violates subprocess isolation contract. Subprocess failure containment, memory isolation, exit code semantics, and retry granularity all depend on 4 separate executables.
2. **Action 2 (PostProcessVerifier merge)** — exit codes must be preserved (scripts may parse them)
3. **Action 3 (Core.NaturalLanguage merge)** — namespace MUST stay `DocGeneration.Core.NaturalLanguage` even in Core.Shared to prevent cascading `using` statement changes in 3 step projects + 6 test files
4. **Data file loading** — Core.Shared.csproj must have `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` for JSON files

**Architectural Assessment Findings:**
- Dependency graph: CLEAN DAG (zero circular dependencies detected)
- Pipeline stage contracts: NO BREAKAGE (all 7 steps' I/O contracts preserved)
- Project count reconciliation: 42 projects in inventory; 44 in solution file; post-consolidation 40 expected
- Subprocess isolation: **CRITICAL RESILIENCE CONTRACT** — cannot be sacrificed for consolidation

**Decision filed as:** AD-027 (main), AD-030 (exit code), AD-031 (namespace preservation)

**Next:** Await implementation; will block any PR attempting Bootstrap consolidation (subprocess isolation violation).

---

### 2026-03-30: .NET Project Consolidation Architecture Review

**Context:** Reviewed Avery's consolidation plan proposing to reduce project count from 42→34 (or 32 with Bootstrap merge).

**Key architectural insights:**

1. **Subprocess isolation is a resilience contract, not an implementation detail**
   - PipelineRunner invokes each step via `dotnet run --project` for failure containment
   - Process crashes are isolated (Step 3 OOM doesn't kill entire batch)
   - Memory isolation prevents cross-namespace state leakage
   - Exit codes provide clean failure signaling
   - **Implication:** Cannot consolidate step executables without breaking this contract

2. **Dependency graphs must remain acyclic**
   - Validated Core.NaturalLanguage → Core.Shared merge: zero circular dependency risk
   - TextCleanup.cs only depends on Core.Shared (one-way) + BCL types
   - Core.Shared has no project references (foundation layer)
   - **Principle:** Before any library merge, verify no project is both producer and consumer

3. **Namespace preservation reduces consolidation churn**
   - Moving file location ≠ changing namespace
   - Core.NaturalLanguage code can move to Core.Shared while keeping `namespace DocGeneration.Core.NaturalLanguage`
   - Prevents cascading `using` statement changes across 3 step projects + tests
   - **Best practice:** Consolidate project boundaries, preserve logical namespaces

4. **Data file loading is runtime-breaking if mishandled**
   - TextCleanup loads JSON files (`nl-parameters.json`, etc.) at runtime
   - After moving to Core.Shared, must verify file copy rules in .csproj
   - Options: embedded resources OR `<None Include="..." CopyToOutputDirectory="PreserveNewest" />`
   - **Risk mitigation:** Write integration test that loads data files from new location

5. **Exit code contracts matter for tool consolidation**
   - PostProcessVerifier → ToolFamilyCleanup merge must preserve exit codes
   - Scripts may parse exit codes for error handling (exit 0 = success, exit 1 = validation failure)
   - **Requirement:** Document exit code contract before merging tools

6. **Bootstrap sub-step consolidation violates isolation contract**
   - Avery proposed merging 4 Bootstrap executables into one multi-command CLI
   - **Rejection rationale:** Breaks failure containment, parallel execution, retry granularity
   - **Alternative:** Extract shared logic to `DocGeneration.Steps.Bootstrap.Shared` library
   - **Principle:** Consolidate code, not processes (when subprocess isolation is architectural)

7. **Test framework standardization is a quality-of-life win**
   - 16 of 19 projects use xUnit; 3 use NUnit
   - Two frameworks = two sets of conventions, two assertion APIs, two packages
   - Standardizing to xUnit enables shared test utilities, single mental model
   - **Value:** Not just project count reduction — reduces cognitive overhead

8. **Conservative consolidation > aggressive consolidation**
   - Avery correctly avoided over-consolidation:
     - Core.GenerativeAI stays separate (heavy Azure dependencies)
     - Core.TemplateEngine stays separate (Handlebars.Net isolation)
     - Step projects stay separate (subprocess isolation)
   - **Observation:** Most consolidation efforts fail by merging too aggressively
   - **Best practice:** Only consolidate when coupling already exists OR boundaries are artificial

**Decision filed:** `.squad/decisions/inbox/riley-consolidation-review.md`

**Verdict:** APPROVE WITH CHANGES
- Actions 1-6: Approved (CliAnalyzer deletion, PostProcessVerifier merge, Core.NaturalLanguage merge, test standardization, StripFrontmatter consolidation, Validation.Tests documentation)
- Action 7 (Bootstrap consolidation): Rejected — violates subprocess isolation contract
- Recommended execution: 2 sprints, 3 phases

**Key requirements added:**
- Namespace preservation for Core.NaturalLanguage (prevent cascading changes)
- Exit code preservation for PostProcessVerifier merge (maintain tool contract)
- Data file loading validation for TextCleanup move (prevent runtime breakage)
- Script audit requirement before PostProcessVerifier deletion (find hidden dependencies)

**Cross-team coordination:**
- Morgan: Implementation lead (Actions 2, 3, 4, 5)
- Quinn: Script audit + CI verification (Actions 1, 2)
- Cameron: Test strategy review (Actions 3, 4)
- Parker: Test coverage verification (Actions 3, 4, 5)
- Reeve: Documentation (Action 6)

**Architecture principles reinforced:**
- Subprocess isolation is a resilience mechanism
- Dependency graphs must be acyclic
- Consolidation should reduce artificial boundaries, not break intentional isolation
- Tool merges must preserve behavioral contracts (exit codes, file paths, namespaces)

---
