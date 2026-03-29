# CI Integration Guide

How continuous integration works in the Azure MCP Documentation Generator, including local development commands, CI pipeline structure, test inventory, and debugging guidance.

---

## 1. Local Development Commands

### Build

```bash
dotnet build docs-generation.sln --configuration Release
```

### Test (full suite)

```bash
dotnet test docs-generation.sln
```

The full suite runs ~1,935 tests across 20 test projects. All projects use **xUnit** on **.NET 9**.

### Test a single project

```bash
dotnet test docs-generation/DocGeneration.Tools.Fingerprint.Tests/
```

### Run the documentation pipeline

```bash
# All 52 namespaces, all steps
./start.sh

# Single namespace, all steps
./start.sh advisor

# Single namespace, specific steps
./start.sh advisor 1

# Multiple steps
./start.sh advisor 1,2,3

# Skip dependency validation for fast iteration
./start.sh advisor 4 --skip-deps
```

> **Note**: Steps 2, 3, 4, and 6 require Azure OpenAI credentials in `docs-generation/.env`. Step 1 is fast and requires no AI.

---

## 2. CI Pipeline Structure

The project uses GitHub Actions with workflows in `.github/workflows/`. There are two categories: **core CI workflows** that gate code quality, and **operational workflows** that automate project management.

### Core CI Workflows

| Workflow | File | Trigger | What it does |
|----------|------|---------|--------------|
| **Build and Test** | `build-and-test.yml` | PR to `main`, push to `main` (when `docs-generation/**` changes) | Restores, builds, and runs all tests in `docs-generation.sln` using .NET 9 on `ubuntu-latest` |
| **Squad CI** | `squad-ci.yml` | PR to `dev`/`preview`/`main`/`insider`, push to `dev`/`insider` | Placeholder for additional build/test commands (currently echoes a TODO) |
| **Generate MCP Documentation** | `generate-docs.yml` | Manual (`workflow_dispatch`) only | Three-job pipeline: (1) generate CLI output via Docker, (2) generate documentation, (3) optionally generate AI example prompts. Uploads artifacts with 10-day retention |
| **Test @azure/mcp Update** | `test-azure-mcp-update.yml` | PR changing `test-npm-azure-mcp/package.json` or `package-lock.json` | Installs `@azure/mcp` npm package and runs `azmcp --version` and `azmcp --help` to validate the CLI still works |
| **Update @azure/mcp Version** | `update-azure-mcp.yml` | Nightly schedule (9:00 AM UTC), manual | Checks for new `@azure/mcp` versions, creates a PR with the update, snapshots tool list, diffs against previous version, runs npm audit, enables auto-merge for non-breaking changes |

### Operational Workflows (Squad Management)

These automate issue triage, labeling, and release promotion. They don't gate code quality but keep the project organized:

| Workflow | File | Trigger | Purpose |
|----------|------|---------|---------|
| **Squad Triage** | `squad-triage.yml` | Issue labeled `squad` | Auto-routes issues to team members based on keywords |
| **Squad Issue Assign** | `squad-issue-assign.yml` | Issue labeled `squad:{member}` | Posts assignment acknowledgment, assigns @copilot if applicable |
| **Squad Label Enforce** | `squad-label-enforce.yml` | Issue labeled | Enforces mutual exclusivity on `go:`, `release:`, `type:`, `priority:` labels |
| **Squad Heartbeat** | `squad-heartbeat.yml` | Issue closed/labeled, PR closed, manual | Monitors untriaged/unstarted issues, auto-triages unassigned work |
| **Sync Squad Labels** | `sync-squad-labels.yml` | Push to `.squad/team.md`, manual | Creates/updates GitHub labels from the team roster |
| **Squad Preview** | `squad-preview.yml` | Push to `preview` | Placeholder for preview branch validation |
| **Squad Promote** | `squad-promote.yml` | Manual | Promotes `dev` → `preview` → `main` with forbidden-path stripping |
| **Squad Release** | `squad-release.yml` | Push to `main` | Placeholder for release creation |
| **Squad Insider Release** | `squad-insider-release.yml` | Push to `insider` | Placeholder for insider pre-releases |
| **Squad Docs** | `squad-docs.yml` | Push to `preview` (when `docs/**` changes), manual | Placeholder for documentation site build |

### Primary Quality Gate

The **Build and Test** workflow (`build-and-test.yml`) is the main CI gate. It runs on every PR and push to `main` when source code changes:

```yaml
on:
  push:
    branches: [main]
    paths:
      - 'docs-generation/**'
      - 'docs-generation.sln'
      - '.github/workflows/build-and-test.yml'
  pull_request:
    branches: [main]
    paths:
      - 'docs-generation/**'
      - 'docs-generation.sln'
      - '.github/workflows/build-and-test.yml'
```

The job performs three steps:
1. `dotnet restore docs-generation.sln`
2. `dotnet build docs-generation.sln --no-restore --configuration Release`
3. `dotnet test docs-generation.sln --no-build --configuration Release --verbosity normal`

---

## 3. Test Projects Inventory

All 20 test projects in `docs-generation.sln`, using xUnit on .NET 9:

| Test Project | What it tests | Approx. Tests |
|-------------|---------------|:-------------:|
| `DocGeneration.Steps.HorizontalArticles.Tests` | Step 6: horizontal article generation, AI content validation, transformations | ~410 |
| `DocGeneration.Core.TextTransformation.Tests` | Text cleanup, static replacements, trailing period management | ~305 |
| `DocGeneration.PipelineRunner.Tests` | Pipeline orchestration, step registry, validators, integration | ~210 |
| `DocGeneration.Steps.Bootstrap.BrandMappings.Tests` | Brand mapping validation, merge groups, filename resolution | ~95 |
| `DocGeneration.Tools.Fingerprint.Tests` | Baseline fingerprinting tool — snapshot and diff modes | ~58 |
| `DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests` | Annotations, parameters, template regression | ~42 |
| `DocGeneration.Steps.SkillsRelevance.Tests` | Step 5: skills relevance scoring | ~13 |
| `DocGeneration.Steps.ToolFamilyCleanup.Validation.Tests` | Step 4: post-assembly validation | ~10 |
| `DocGeneration.Steps.ToolFamilyCleanup.Tests` | Stitcher, contractions, acronyms, P1 regression | varies |
| `DocGeneration.Steps.ExamplePrompts.Validation.Tests` | Prompt validation rules | ~3 |
| `DocGeneration.Steps.ExamplePrompts.Generation.Tests` | Deterministic prompt generation | varies |
| `DocGeneration.Steps.Bootstrap.CommandParser.Tests` | CLI command parsing | varies |
| `DocGeneration.Steps.Bootstrap.E2eTestPromptParser.Tests` | E2E test prompt parsing | varies |
| `DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Tests` | Tool matching, parameter extraction | varies |
| `DocGeneration.Steps.ToolGeneration.Improvements.Tests` | Template label protection, mcpcli markers | varies |
| `DocGeneration.Core.TemplateEngine.Tests` | Handlebars engine basics | ~2 |
| `DocGeneration.Core.Shared.Tests` | ParameterCoverageChecker, headings, frontmatter | varies |
| `DocGeneration.Core.GenerativeAI.Tests` | AI prompt generation, log files | varies |
| `DocGeneration.Core.NaturalLanguage.Tests` | Natural language parameter mappings | varies |
| `DocGeneration.PromptRegression.Tests` | Prompt regression detection | varies |

> **"varies"** means the count changes as features are added. Run `dotnet test docs-generation.sln --verbosity normal` to get current exact counts.

---

## 4. Adding New Tests to CI

### Step 1: Create the test project

Follow the existing naming convention — `DocGeneration.{Layer}.{Feature}.Tests`:

```bash
cd docs-generation
dotnet new xunit -n DocGeneration.Steps.MyFeature.Tests
```

### Step 2: Add project references

Reference the project under test:

```bash
cd DocGeneration.Steps.MyFeature.Tests
dotnet add reference ../DocGeneration.Steps.MyFeature/DocGeneration.Steps.MyFeature.csproj
```

### Step 3: Add to the solution

```bash
cd ../..   # Back to repo root
dotnet sln docs-generation.sln add docs-generation/DocGeneration.Steps.MyFeature.Tests/DocGeneration.Steps.MyFeature.Tests.csproj
```

### Step 4: Verify locally

```bash
dotnet test docs-generation.sln
```

### That's it — CI picks it up automatically

The `build-and-test.yml` workflow runs `dotnet test docs-generation.sln`, which discovers all test projects in the solution. No workflow file changes are needed.

### Naming conventions

| Layer | Pattern | Example |
|-------|---------|---------|
| Core libraries | `DocGeneration.Core.{Feature}.Tests` | `DocGeneration.Core.TextTransformation.Tests` |
| Pipeline steps | `DocGeneration.Steps.{Step}.{Feature}.Tests` | `DocGeneration.Steps.HorizontalArticles.Tests` |
| Bootstrap sub-steps | `DocGeneration.Steps.Bootstrap.{Feature}.Tests` | `DocGeneration.Steps.Bootstrap.BrandMappings.Tests` |
| Tools / utilities | `DocGeneration.Tools.{Feature}.Tests` | `DocGeneration.Tools.Fingerprint.Tests` |
| Cross-cutting | `DocGeneration.{Feature}.Tests` | `DocGeneration.PromptRegression.Tests` |

### Package management

NuGet package versions are centrally managed in `Directory.Packages.props` at the solution root. Use `<PackageReference Include="..." />` (without `Version`) in `.csproj` files and add the version to `Directory.Packages.props`.

---

## 5. Debugging CI Failures

### Reproduce locally

The CI workflow runs on `ubuntu-latest` with .NET 9. To reproduce locally:

```bash
# Exact commands the CI runs
dotnet restore docs-generation.sln
dotnet build docs-generation.sln --no-restore --configuration Release
dotnet test docs-generation.sln --no-build --configuration Release --verbosity normal
```

On Windows, this is the same — the test suite is cross-platform.

### Common failure patterns

#### 1. Path separator issues (Windows vs. Linux)

**Symptom**: Tests pass locally on Windows but fail in CI (Linux).

**Fix**: Use `Path.Combine()` or `Path.DirectorySeparatorChar` instead of hardcoded `\` or `/` in test code.

#### 2. Missing NuGet restore

**Symptom**: `error CS0246: The type or namespace name '...' could not be found`

**Fix**: Run `dotnet restore docs-generation.sln` before building. The CI does `--no-restore` on build because it runs restore as a separate step.

#### 3. Test project not discovered

**Symptom**: New tests don't appear in CI output.

**Fix**: Ensure the test project is added to `docs-generation.sln` with `dotnet sln add`. Verify with:

```bash
dotnet sln docs-generation.sln list | Select-String "Tests"
```

#### 4. Build configuration mismatch

**Symptom**: Tests pass with `dotnet test` but fail with `dotnet test --configuration Release`.

**Fix**: CI builds and tests in `Release` configuration. Make sure your tests don't depend on `DEBUG` conditional compilation or debug-only behavior.

#### 5. Path-filtered workflow not triggering

**Symptom**: PR changes code in `docs-generation/` but CI doesn't run.

**Fix**: The `build-and-test.yml` workflow only triggers when files under `docs-generation/**`, `docs-generation.sln`, or the workflow file itself change. Documentation-only changes (e.g., `docs/*.md`) intentionally skip CI.

### Reading CI logs

1. Go to the **Actions** tab in the GitHub repository
2. Click the failed workflow run
3. Expand the **Run tests** step to see xUnit output
4. Look for `Failed!` lines — each shows the test name, expected vs. actual values, and stack trace
5. The summary at the bottom shows total passed/failed/skipped counts

---

## Related Documentation

- [docs/test-strategy.md](test-strategy.md) — Full test strategy with coverage targets and TDD workflow
- [docs/FINGERPRINTING.md](FINGERPRINTING.md) — Baseline fingerprinting for regression detection
- [docs/START-SCRIPTS.md](START-SCRIPTS.md) — Complete `start.sh` usage reference
- [docs/ARCHITECTURE.md](ARCHITECTURE.md) — System architecture and pipeline step details
