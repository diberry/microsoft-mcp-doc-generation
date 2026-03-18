# Product Requirements Document: Typed .NET PipelineRunner

- **Status:** Proposed
- **Date:** 2026-03-14
- **Requested by:** Dina Berry
- **Product owner:** Holden (Content Strategy Lead)
- **Primary implementer:** Amos (Pipeline Dev)
- **Repos in scope:**
  - `C:\Users\diberry\repos\project-azure-mcp-server\microsoft-mcp-doc-generation`
  - `C:\Users\diberry\repos\project-azure-mcp-server\squad\.squad\decisions\amos-pipeline-redesign-proposals.md`
  - `C:\Users\diberry\repos\project-azure-mcp-server\squad\.squad\decisions\holden-pipeline-redesign-options.md`

## 1. Executive Summary

Build a new typed `.NET 9` orchestration project named `PipelineRunner` to replace the current Bash + PowerShell control plane for documentation generation. The new runner must keep the existing C# generators intact, preserve current outputs, and make pipeline behavior explicit, testable, and extensible.

This is the PRD for **Option C** from the redesign proposals: a long-term typed end-state delivered through a phased migration. The first release does **not** big-bang rewrite the pipeline. It introduces a typed host and step registry first, then ports step logic incrementally until the PowerShell step wrappers are no longer needed for standard execution.

## 2. Problem Statement

The current documentation pipeline works, but the orchestration layer is the least maintainable part of the system.

### Current evidence from the codebase

- The solution is already centered on `.NET 9` generators and shared libraries, but orchestration still hops across `start.sh`, `start-only.sh`, `Generate-ToolFamily.ps1`, `Shared-Functions.ps1`, and multiple `*-One.ps1` step wrappers.
- Steps 1, 2, 3, 4, and 6 repeat the same wrapper pattern: resolve output path, load CLI metadata, normalize target, build, create filtered CLI files, run generators, validate outputs, and clean temp directories.
- `Shared-Functions.ps1` already contains the core reusable primitives, but the primitives are not formalized into a typed contract or enforced consistently.
- Step contracts are implicit. Dependencies, failure policies, required inputs, temp-workspace behavior, and post-validation hooks are scattered across scripts instead of declared in one place.
- The control plane is mixed-language: Bash, PowerShell, and C#. That increases debugging overhead, path/exit-code complexity, and contributor onboarding cost.
- Testing is strong in the generator layer and validator layer, but weak in the orchestration layer. Current glue logic is harder to unit test than it should be.
- Step identity is confusing today. The pipeline still carries a hidden validator with a `5-` prefix and duplicate horizontal article scripts.
- Step 4 requires a special isolated workspace because the cleanup assembly expects a particular file layout. That behavior is real product logic, but it currently lives inside filesystem-heavy PowerShell glue.

### Why this matters now

The pipeline already has **588 passing tests** across the solution. That is a strong baseline worth protecting. The problem is not that the generator ecosystem needs to be rewritten; the problem is that the orchestration layer makes change expensive, hides failure behavior, and makes future step additions more likely to involve copy/paste wrappers instead of reusable infrastructure.

## 3. Goals

1. **Unify orchestration in .NET**
   - Move pipeline control flow into a typed `.NET` project aligned with the existing generator ecosystem.
2. **Make steps explicitly testable**
   - Every step must declare scope, dependencies, failure policy, inputs, outputs, and post-validation behavior in code.
3. **Introduce an extensible step registry**
   - Adding a new step must require one C# class plus one registration change, not a new PowerShell wrapper copied from an existing script.
4. **Preserve backward-compatible CLI entrypoints**
   - Existing `start.sh` usage must continue to work, including legacy positional syntax.
5. **Preserve current generated output**
   - The typed runner must reproduce the same generated content for existing flows, starting with `compute` as the parity namespace.
6. **Make failure behavior explicit**
   - Fatal vs warning-only behavior must live with the step definition, not in scattered wrapper logic.
7. **Formalize post-hooks and validators**
   - Single-tool validation in Step 2 and post-assembly validation in Step 4 must be modeled as first-class execution concepts.
8. **Reduce shell-specific maintenance**
   - Standard pipeline execution should no longer depend on PowerShell step wrappers after migration is complete.

## 4. Non-Goals

1. **Do not rewrite the existing C# generators**
   - `ToolGeneration_Raw`, `ExamplePromptGeneratorStandalone`, `ToolGeneration_Composed`, `ToolGeneration_Improved`, `ToolFamilyCleanup`, `SkillsRelevance`, and `HorizontalArticleGenerator` remain the business-logic layer.
2. **Do not change AI prompt logic as part of this effort**
   - Prompt quality, model behavior, and generator prompt content stay as-is unless a later task explicitly changes them.
3. **Do not change published output format**
   - Article structure, tool markdown layout, parameter manifests, validator report expectations, and generated file shapes must remain compatible.
4. **Do not redesign the content-generation architecture beyond orchestration**
   - The PRD is for the runner and its shared services, not a broader regeneration of article templates or output semantics.
5. **Do not reduce existing test coverage**
   - Existing tests stay in place. New orchestration tests are additive.

## 5. Users and Stakeholders

- **Dina Berry:** sponsor, quality gate owner, final architectural approver
- **Amos:** implementer of the typed runner and migration phases
- **Alex:** validator owner; must be able to test orchestration outcomes and quality gates reliably
- **Naomi:** downstream content consumer; expects output parity and stable file locations
- **Future maintainers:** should be able to reason about the pipeline without reading multiple shell languages

## 6. Current-State Findings to Preserve

### Solution and project patterns

The generation repo already has the shape needed for a typed orchestration layer:

- Solution: `docs-generation.sln`
- Runtime center of gravity: `.NET 9`
- Existing generator projects are executable console apps
- Existing test projects use **xUnit** and follow consistent `.Tests` conventions
- Shared infrastructure already exists in projects such as `Shared`, `GenerativeAI`, `TemplateEngine`, and `TextTransformation`

### Current shell flow

Current execution is effectively:

1. `start.sh`
2. `preflight.ps1`
3. `start-only.sh`
4. `Generate-ToolFamily.ps1`
5. Step scripts 1-6
6. Post-validator hook inside Step 4

That high-level sequence is worth preserving conceptually, but the orchestration should move to typed code.

### Shared functionality that already exists conceptually

`Shared-Functions.ps1` already expresses the right primitives:

- output path resolution
- CLI version loading
- CLI metadata loading
- target normalization and matching
- filtered CLI file creation
- build invocation
- temp-directory cleanup
- logging helpers

The typed runner should preserve these primitives while moving them into explicit services.

## 7. Proposed Architecture

## 7.1 Project structure

Add two new projects under `docs-generation`:

```text
PipelineRunner/
  PipelineRunner.csproj
  Program.cs
  Cli/
    PipelineRequest.cs
    PipelineOptionsBinder.cs
  Contracts/
    IPipelineStep.cs
    IPostValidator.cs
    FailurePolicy.cs
    StepScope.cs
    StepDefinition.cs
    StepResult.cs
  Context/
    PipelineContext.cs
    PipelineContextFactory.cs
  Registry/
    StepRegistry.cs
  Services/
    ICliMetadataLoader.cs
    ITargetMatcher.cs
    IFilteredCliWriter.cs
    IProcessRunner.cs
    IWorkspaceManager.cs
    IReportWriter.cs
    IBuildCoordinator.cs
    IAiCapabilityProbe.cs
  Steps/
    Bootstrap/
      PreflightStep.cs
      BrandMappingValidationStep.cs
    Namespace/
      GenerateAnnotationsParametersRawStep.cs
      GenerateExamplePromptsStep.cs
      GenerateToolCompositionStep.cs
      GenerateToolFamilyCleanupStep.cs
      GenerateSkillsRelevanceStep.cs
      GenerateHorizontalArticlesStep.cs
    Validation/
      ExamplePromptRequiredParamsValidator.cs
      ToolFamilyPostAssemblyValidator.cs
  Reporting/
    PipelineRunSummary.cs
    ConsoleReporter.cs

PipelineRunner.Tests/
  PipelineRunner.Tests.csproj
  Unit/
  Integration/
  Fixtures/
```

### Project requirements

- Target framework: `net9.0`
- Test framework: xUnit
- Prefer the same CLI parsing approach already used in existing console apps (for example, `System.CommandLine` where appropriate)
- Reference existing shared libraries where useful, but keep orchestration concerns in `PipelineRunner`

## 7.2 Execution model

`PipelineRunner` becomes the single orchestration host.

### End-state flow

1. `start.sh` becomes a thin shell wrapper.
2. `start.sh` delegates to `dotnet run --project docs-generation/PipelineRunner -- ...`.
3. `PipelineRunner` parses arguments, creates a `PipelineContext`, resolves selected steps, validates dependencies, and executes steps in order.
4. Steps call existing generators primarily through a shared `IProcessRunner` abstraction.
5. Post-validators run through explicit validator hooks instead of being hidden inside step scripts.
6. Console output remains human-friendly, but the runner also tracks typed execution results for test assertions and structured reporting.

## 7.3 `IPipelineStep` contract

Each step must implement a typed contract equivalent to:

```csharp
public interface IPipelineStep
{
    int Id { get; }
    string Name { get; }
    StepScope Scope { get; }
    FailurePolicy FailurePolicy { get; }
    IReadOnlyList<int> DependsOn { get; }
    IReadOnlyList<IPostValidator> PostValidators { get; }
    ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken);
}
```

### Contract requirements

Every step definition must explicitly declare:

- `Id`
- `Name`
- `Scope`
- `FailurePolicy`
- `DependsOn`
- whether it needs CLI output
- whether it needs CLI version
- whether it needs AI configuration
- whether it creates a filtered CLI view
- whether it uses an isolated workspace
- expected output directories and output assertions
- post-validators, if any

`StepResult` should capture:

- success/failure status
- warnings
- produced output paths
- duration
- process invocations made
- validator results

## 7.4 `PipelineContext` shared state

`PipelineContext` is the shared state container for one pipeline run.

Minimum fields:

```csharp
public sealed class PipelineContext
{
    public required PipelineRequest Request { get; init; }
    public required string RepoRoot { get; init; }
    public required string DocsGenerationRoot { get; init; }
    public required string OutputPath { get; init; }
    public required IProcessRunner ProcessRunner { get; init; }
    public required IWorkspaceManager Workspaces { get; init; }
    public required ICliMetadataLoader CliMetadataLoader { get; init; }
    public required ITargetMatcher TargetMatcher { get; init; }
    public required IReportWriter Reports { get; init; }
    public string? CliVersion { get; set; }
    public object? CliOutput { get; set; }
    public bool AiConfigured { get; set; }
    public IReadOnlyList<string> SelectedNamespaces { get; set; } = [];
    public Dictionary<string, object> Items { get; } = new();
}
```

### Context rules

- Created once per run, then reused across steps
- Stores shared artifacts such as CLI metadata and version to avoid repeated reloads
- Tracks run-scoped temp workspaces
- Carries output root and namespace selection state
- Exposes a typed cache (`Items`) only for runner-specific intermediate state, not for generator business logic

## 7.5 `PipelineRunner` main orchestrator

`PipelineRunner` is responsible for:

- parsing CLI arguments into `PipelineRequest`
- resolving output defaults and repo paths
- creating `PipelineContext`
- bootstrapping global prerequisites
- resolving the selected steps from the step registry
- validating dependencies before execution
- executing global and namespace-scoped steps in order
- honoring `FailurePolicy`
- executing post-validators unless `--skip-validation` is set
- producing a final run summary and exit code

### Dependency behavior

The runner must **validate dependencies explicitly**.

- It must not silently invent missing steps.
- If a selected step requires prior outputs that are neither present nor scheduled, the runner fails fast with a clear message.
- `--dry-run` must print the resolved plan, scopes, and dependency checks without executing generators.

## 7.6 Step registry pattern

`StepRegistry` is the central registry of available steps.

### Registry responsibilities

- register all step instances
- guarantee unique step IDs
- return steps ordered by execution ID
- expose metadata for `--dry-run` and test assertions
- support future additions without changes to the runner core

### Registration pattern

The intended developer workflow for a new step is:

1. create one class implementing `IPipelineStep`
2. add any validator classes it needs
3. register the class in `StepRegistry`
4. add one unit test and one integration test

That is the target extensibility contract for future step 7+ work.

## 7.7 How generators are invoked

### Decision: subprocess-first

The typed runner will call existing generators as **subprocesses by default**.

#### Why

- existing generators are already reliable executable entry points
- this avoids invasive refactors of current generator code
- it preserves current environment-variable and process-boundary behavior
- it minimizes migration risk while still moving orchestration into typed code

### Invocation rules

- Use `dotnet run --project ...` while projects are still evolving in active development
- Use `dotnet <dll>` only where current behavior already depends on a built DLL or the generator expects that mode
- Use PowerShell subprocess invocation only for temporary compatibility during migration or for validators not yet ported
- Shared orchestration services are in-process from day one; generators are not rewritten into libraries as part of this PRD

### In-process execution is explicitly deferred

Future in-process execution is allowed only if a generator later exposes a stable library API and parity tests demonstrate no behavior change. That is outside the scope of this implementation.

## 8. Step Definitions

The typed runner must model the **six current user-facing steps** as first-class step definitions. A bootstrap/preflight stage may also exist internally, but the user-facing step surface remains Steps 1-6.

## 8.1 Step definition matrix

| Step | Name | Scope | Inputs | Outputs | Generator project invoked | Failure policy | Post-validators | Special behavior |
|---|---|---|---|---|---|---|---|---|
| 1 | Generate annotations, parameters, and raw tools | Namespace | `Namespace`, `OutputPath`, `SkipBuild`, `SkipValidation` | `annotations/<base>-annotations.md`, `parameters/<base>-parameters.md`, `parameters/<base>-params.json`, `tools-raw/<base>.md` | `generate-docs --annotations`, `generate-docs --parameters`, `ToolGeneration_Raw` | **Fatal overall**; raw-tool generation may warn internally but step failure remains fatal if core outputs are missing | optional output assertions only | One logical step with three generator invocations; uses filtered CLI file |
| 2 | Generate example prompts | Namespace | `Namespace`, `OutputPath`, `SkipBuild`, `SkipValidation` | `example-prompts-prompts/<base>-input-prompt.md`, `example-prompts-raw-output/<base>-raw-output.txt`, `example-prompts/<base>-example-prompts.md`, optional `example-prompts-validation/<base>-validation.md` | `ExamplePromptGeneratorStandalone` | **Fatal** | `Validate-ExamplePrompts-RequiredParams` for single-tool runs only | Injects parameter manifests and optional parsed e2e prompts |
| 3 | Compose and improve tool files | Namespace | `Namespace`, `OutputPath`, `SkipBuild`, `SkipValidation`, `MaxTokens`, skip flags for composed/improved phases | `tools-composed/<base>.md`, `tools/<base>.md` | `ToolGeneration_Composed`, `ToolGeneration_Improved` | **Fatal** when executed | optional output assertions only | Two-stage generator chain inside one step |
| 4 | Generate tool-family article | Namespace | `Namespace`, `OutputPath`, `SkipBuild`, `SkipValidation` | `tool-family-metadata/<family>-metadata.md`, `tool-family-related/<family>-related.md`, `tool-family/<family>.md`, `reports/tool-family-validation-<family>.txt` | `ToolFamilyCleanup` | **Fatal** | post-assembly tool-family validator | Requires isolated family workspace and copy-back flow |
| 5 | Generate skills relevance | Namespace | `Namespace`/`ServiceArea`, `OutputPath`, `SkipBuild`, `MinScore` | `skills-relevance/<service>-skills-relevance.md`, optional `skills-relevance/index.md` | `SkillsRelevance` | **Warn-and-continue** | none | Missing `GITHUB_TOKEN` is warning-only by design |
| 6 | Generate horizontal article | Namespace | `Namespace`/`ServiceArea`, `OutputPath`, `SkipBuild`, `SkipValidation`, optional transform flag | `horizontal-articles/horizontal-article-<service>.md` or `horizontal-articles/error-<service>.txt` | `HorizontalArticleGenerator` | **Fatal** | optional output assertions only | Single-service generation; unifies duplicate script behavior into one typed step |

## 8.2 Step-specific requirements

### Step 1

- Must preserve the three-part generation sequence used today.
- Must continue producing parameter manifest JSON because Step 2 consumes it.
- Must treat annotations and parameters as required outputs.
- If raw-tool generation fails but required outputs are still missing, the step is fatal.

### Step 2

- Must pass parameter manifest directory to `ExamplePromptGeneratorStandalone`.
- Must continue discovering optional parsed e2e prompt input when present.
- Must continue the single-tool-only required-params validator behavior unless `--skip-validation` is set.

### Step 3

- Must preserve the existing composed-then-improved sequence.
- Must allow per-run flags that skip either the composed or improved subphase for development/debugging, while preserving default behavior.
- Missing prerequisite directories should be surfaced as a runner error before execution rather than a late shell warning.

### Step 4

- Must preserve the current family inference logic.
- Must continue creating an isolated run workspace containing only:
  - the family’s tool files
  - `cli-version.json`
  - `brand-to-server-mapping.json`
  - any required prompt/config assets
- Must copy generated outputs back into the real output tree.
- Must run post-assembly validation as a first-class validator when validation is enabled.
- Current unused flags from PowerShell wrappers (`SkipMetadata`, `SkipRelated`, `SkipStitch`) must not be carried forward unless explicitly implemented.

### Step 5

- Must preserve non-blocking behavior.
- Missing GitHub authentication or relevance-generation issues must produce warnings and summary output, not fatal pipeline exits.

### Step 6

- Must replace the duplicate script situation with one canonical step implementation.
- Must preserve current single-service generation behavior and optional transform support.

## 8.3 Bootstrap and preflight behavior

The runner should also model bootstrap behavior internally even though it is not part of the 1-6 user-facing step list.

Bootstrap responsibilities:

- environment validation for AI-required steps only
- solution build coordination when `--skip-build` is not set
- CLI metadata generation/loading
- CLI version loading
- brand-mapping validation

### Important rule

**Step 1-only runs must not be blocked by unnecessary AI validation.** AI gating belongs only to steps that require AI-backed generators or validators.

## 9. CLI Interface

## 9.1 Canonical runner command

```bash
dotnet run --project docs-generation/PipelineRunner -- \
  --namespace compute \
  --steps 1,2,3,4 \
  --output ./generated-compute
```

## 9.2 Required CLI arguments and behavior

### `--namespace <name>`

- Optional
- When supplied, run the pipeline for one namespace/service
- When omitted, run for all namespaces available from CLI metadata

### `--steps <csv>`

- Optional
- Default: `1,2,3,4,5,6`
- Accepts comma-separated numeric step IDs
- `--dry-run` must show resolved order and dependency checks

### `--output <path>`

- Optional
- Default behavior must preserve current conventions:
  - single namespace: `./generated-<namespace>`
  - all namespaces: `./generated`

### `--skip-build`

- Optional
- Skips build work and passes `--no-build` where appropriate
- Must not silently disable required artifacts that are missing; missing binaries remain an error

### `--skip-validation`

- Optional
- Skips post-validators and output assertions
- Must not change generator behavior, only validation behavior

### `--dry-run`

- Optional
- Prints plan, selected namespaces, selected steps, dependency checks, failure policies, and intended generator invocations without executing them

## 9.3 Exit codes

| Code | Meaning |
|---|---|
| 0 | Success; warning-only steps may still have reported warnings |
| 1 | Fatal execution failure, blocking validator failure, or missing required dependency/output |
| 2 | Manual intervention required because bootstrap brand mapping validation found unmapped namespaces (preserve current semantics) |
| 64 | Invalid CLI usage (bad arguments, invalid step list, invalid namespace/step combination) |

## 9.4 `start.sh` compatibility

`start.sh` becomes a thin wrapper that maps legacy positional usage into the canonical `PipelineRunner` CLI.

### Required backward-compatible behaviors

- `start.sh compute` → run all default steps for `compute`
- `start.sh compute 1,2,3,4` → run explicit steps for `compute`
- `start.sh 1,2,3,4` → run those steps for all namespaces
- `start.sh` → run all default steps for all namespaces

### Wrapper responsibility

`start.sh` should do only three things:

1. normalize legacy positional arguments into named arguments
2. call `dotnet run --project docs-generation/PipelineRunner -- ...`
3. pass through exit codes unchanged

`start-only.sh` may exist only during migration. It is not part of the end-state architecture.

## 10. Shared Services to Extract from `Shared-Functions.ps1`

The runner must lift shared orchestration behavior into typed services.

## 10.1 CLI metadata loader

**Responsibility:** load and cache `cli-output.json`, `cli-version.json`, and related metadata artifacts.

**Required behavior:**
- fail clearly when CLI metadata is missing
- avoid repeated disk reads across steps
- expose typed accessors for namespace and tool discovery

## 10.2 Target matcher / tool selector

**Responsibility:** normalize target selectors and resolve matching tool families or single tools.

**Required behavior:**
- support namespace-level and single-tool targeting
- preserve current matching rules and file-name conventions
- provide clear error messages when nothing matches

## 10.3 Filtered CLI file writer

**Responsibility:** create temp filtered CLI files scoped to the selected target.

**Required behavior:**
- own temp-path naming
- return strongly typed temp-file handles
- clean up automatically via workspace management

## 10.4 Process runner

**Responsibility:** centralize subprocess execution.

**Required capabilities:**
- `dotnet build`
- `dotnet run --project`
- `dotnet <dll>`
- PowerShell script execution during migration
- stdout/stderr capture
- timing and exit-code reporting
- cancellation support

## 10.5 Temp workspace manager

**Responsibility:** own lifecycle of temp directories and isolated workspaces.

**Required strategies:**
- filtered CLI workspace
- isolated family workspace for Step 4
- automatic cleanup on success or failure
- opt-in retention for debugging if a future debug flag is added

## 10.6 Logging and reporting

**Responsibility:** provide consistent human-readable and machine-readable execution reporting.

**Required behavior:**
- consistent step start/end messages
- duration tracking
- warning aggregation
- final run summary
- no changes to published article output format
- optional internal run summary artifacts may be written under `reports/` if they do not alter downstream content contracts

## 10.7 Build coordination

**Responsibility:** build the solution once when appropriate and avoid repeated per-step build logic.

**Required behavior:**
- honor `--skip-build`
- verify required binaries/projects are runnable when build is skipped
- prevent duplicate build work across steps in the same run

## 10.8 AI capability probe

**Responsibility:** validate environment variables only for AI-dependent steps.

**Required behavior:**
- do not block non-AI step runs unnecessarily
- fail early and clearly when an AI-backed step is selected but required configuration is missing

## 11. Testing Strategy

## 11.1 Guiding principle

The existing **588 tests** remain the regression baseline. New `PipelineRunner` tests are additive and must raise orchestration confidence without weakening existing generator test coverage.

## 11.2 Unit tests for each step

Create unit tests for every step class using a mocked `IProcessRunner` and temp/test file fixtures.

### Must cover

- correct generator invocations and argument construction
- dependency enforcement
- failure-policy behavior
- validator invocation behavior
- output assertion behavior
- skip flags (`--skip-build`, `--skip-validation`, dry-run plan generation)
- Step 4 isolated workspace construction and copy-back logic
- Step 5 warning-only behavior

## 11.3 Unit tests for shared services

Add focused unit tests for:

- CLI metadata loader
- target matcher
- filtered CLI writer
- process runner command construction
- workspace manager cleanup behavior
- build coordinator behavior
- AI capability probe gating logic
- step registry uniqueness and ordering
- final exit code mapping

## 11.4 Integration tests for end-to-end pipeline execution

Add integration tests that execute `PipelineRunner` against fixture-based temp directories.

### Required integration scenarios

1. **Shim mode**: runner calls legacy scripts successfully in Phase 1
2. **Typed Step 1-3 path**: output directories are created with expected files
3. **Typed Step 4 path**: tool-family assembly + validator succeeds
4. **Warning-only Step 5 path**: failure still returns overall success
5. **Backward-compatible `start.sh` invocation**: legacy positional args resolve correctly
6. **Dry-run**: no generators run, but dependency plan and selected steps are reported
7. **Parity namespace**: `compute` output matches the current pipeline for the selected baseline scenario

## 11.5 Relationship to existing tests

- Existing generator and validator tests remain unchanged.
- Existing validator integration tests remain valid and should continue passing.
- New tests must not replace the current 588-test suite; they supplement it.
- Every migration phase must pass the existing suite before additional typed-runner assertions are accepted.

## 12. Migration Plan

The implementation must be phased. No big-bang rewrite is allowed.

## 12.1 Phase 1 — Runner scaffold + step registry + shim

### Scope

- Create `PipelineRunner` and `PipelineRunner.Tests`
- Implement CLI parsing, `PipelineRequest`, `PipelineContext`, `StepRegistry`, exit-code mapping, and reporting
- Implement runner-owned dependency validation and dry-run planning
- Keep `start.sh` as a thin wrapper that now delegates to `PipelineRunner`
- `PipelineRunner` initially shells out to the existing bootstrap and per-step scripts

### Deliverables

- runnable typed host
- typed step registry
- shim step definitions for existing scripts
- backward-compatible wrapper behavior
- first set of orchestration unit tests and shim integration tests

### Exit criteria

- existing `start.sh` scenarios still work
- no output changes
- all existing tests pass

## 12.2 Phase 2 — Port Steps 1, 2, 3, and 6 to typed C# step classes

### Scope

- Replace PowerShell wrappers for Steps 1, 2, 3, and 6 with typed step implementations
- Reuse shared services for CLI loading, target selection, filtered CLI creation, build handling, and output assertions
- Keep Step 4, validator, and Step 5 script-backed for now

### Deliverables

- typed C# implementations for Steps 1, 2, 3, and 6
- per-step unit tests
- parity integration coverage for the typed steps

### Exit criteria

- generated outputs match current behavior for a parity namespace
- all existing tests pass
- new step-level orchestration tests pass

## 12.3 Phase 3 — Port Step 4 + validator orchestration

### Scope

- Implement typed Step 4 including isolated family workspace setup and copy-back behavior
- Promote post-assembly validation to a first-class validator hook
- Either rename the PowerShell validator for clarity or wrap it cleanly as `ToolFamilyPostAssemblyValidator` until a future C# validator exists

### Deliverables

- typed Step 4 implementation
- typed validator hook model
- integration tests covering validator pass/fail/warning scenarios

### Exit criteria

- Step 4 parity achieved for the target namespace(s)
- blocking validator behavior preserved exactly
- all existing tests pass

## 12.4 Phase 4 — Port Step 5 and retire PowerShell step wrappers

### Scope

- Implement typed Step 5 with warning-only behavior
- remove legacy dependency on per-step PowerShell wrappers for standard pipeline execution
- remove duplicate horizontal article wrapper script from the standard flow
- keep only the thin shell entry wrapper where still useful

### Deliverables

- typed Step 5 implementation
- final typed standard execution path
- cleanup of obsolete wrapper scripts from primary usage path

### Exit criteria

- standard pipeline execution no longer needs PowerShell step wrappers
- start wrapper still works for legacy callers
- all existing tests pass
- new orchestration test suite passes

## 13. Success Criteria

This work is successful only when all of the following are true:

1. **All 588 existing tests pass** throughout the migration and at final completion.
2. **`start.sh compute` produces identical output** to the current pipeline for the agreed parity baseline.
3. **Adding step 7 requires one new C# class plus registration**, not a new wrapper script.
4. **No PowerShell step wrappers are needed for standard steps** after the final phase.
5. **Pipeline orchestration logic has dedicated test coverage** at the runner, service, step, and integration levels.
6. **Failure policy is explicit in code** for every step.
7. **Post-validation behavior is modeled explicitly** instead of hidden in wrapper scripts.
8. **Legacy CLI entrypoints remain usable** and preserve expected exit codes.

## 14. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Output parity drift during migration | Could block Naomi and downstream content PR work | Use `compute` as parity namespace, compare outputs at each phase, do not merge typed step replacements without parity verification |
| Step 4 is more filesystem-coupled than other steps | Highest implementation risk in the runner | Delay Step 4 until Phase 3, isolate workspace behavior behind `IWorkspaceManager`, add targeted integration tests |
| AI gating remains too broad | Could block valid local runs and slow development | Move AI checks into `IAiCapabilityProbe` and apply them only to AI-dependent steps |
| Mixed execution modes during migration become confusing | Developers may not know whether a step is typed or shim-backed | Report step implementation mode in `--dry-run` and in the final summary during migration |
| Build behavior changes inadvertently | Could break local workflows and CI | Centralize build coordination and preserve `--skip-build` semantics with explicit validation |
| Exit code changes break automation | Existing scripts and callers may fail unexpectedly | Preserve `0`, `1`, and `2` semantics; introduce `64` only for invalid typed-runner usage |
| Test suite growth increases execution time | Slower feedback loop | Keep most new orchestration tests unit-level and fixture-based; reserve a small number of full integration tests for critical flows |
| Future maintainers bypass the registry and add ad hoc process calls | Architecture regresses over time | Require new steps to implement `IPipelineStep`; document registration as the only supported extension path |

## 15. Implementation Decisions Locked by This PRD

The following decisions are intentional and should be treated as resolved unless Dina explicitly changes them:

1. **The end-state is a typed `.NET` orchestration host.**
2. **Generators remain subprocesses by default.**
3. **Migration is phased, not big-bang.**
4. **`start.sh` remains as a thin compatibility wrapper.**
5. **Step registry and failure policies are code-first, not script-first.**
6. **Existing published output format is preserved.**
7. **Existing test suites stay; new runner tests are additive.**

## 16. Definition of Done

The PRD is satisfied when:

- `PipelineRunner` exists and is the canonical orchestration host
- all six current user-facing steps are modeled as typed step definitions
- bootstrap/preflight behavior is explicit and scoped correctly
- shared orchestration services are extracted from PowerShell concepts into typed services
- standard execution no longer depends on per-step PowerShell wrappers
- output parity and regression tests pass
- developer documentation is updated wherever runner behavior changes affect usage

---

## Appendix A — Existing Generator Entry Points Informing This PRD

Representative executable projects already in the repo include:

- `ToolGeneration_Raw`
- `ExamplePromptGeneratorStandalone`
- `ExamplePromptValidator`
- `ToolGeneration_Composed`
- `ToolGeneration_Improved`
- `ToolFamilyCleanup`
- `SkillsRelevance`
- `HorizontalArticleGenerator`
- `BrandMapperValidator`

These projects demonstrate that the repo is already a `.NET`-first system. The `PipelineRunner` is therefore an orchestration alignment effort, not a language pivot for the business logic layer.

## Appendix B — Existing Test Pattern Informing This PRD

The repo already uses xUnit consistently across generator and validator projects. `PipelineRunner.Tests` must follow the same pattern so the orchestration layer feels native to the solution and can be run with the same `dotnet test` workflow that already protects the current 588-test baseline.
