# Pipeline Smoke Tests

## Purpose

This test project provides end-to-end smoke tests that run the full Azure MCP documentation generation pipeline on small, representative Azure namespaces and verify the output matches expected baseline fixtures. These tests serve as a safety net for refactoring efforts, ensuring that code changes don't alter pipeline behavior or output structure.

## What These Tests Do

The smoke tests:

1. **Run the full pipeline** - Executes all pipeline steps (Bootstrap, Annotations, Tool Generation, etc.) on selected namespaces
2. **Compare against baselines** - Verifies generated output matches committed golden/baseline files
3. **Validate structure** - Checks directory layout, file presence, and key content markers
4. **Fast execution** - Designed to complete in under 5 minutes, suitable for CI

## Architecture

### Key Components

- **PipelineSmokeTests.cs** - Main test class with smoke test methods
- **BaselineManager.cs** - Handles baseline storage, comparison, and update operations
- **Baselines/** - Directory containing committed golden files for each tested namespace

### Baseline Structure

```
Baselines/
├── <namespace>/
│   ├── annotations/
│   │   └── <tool>-annotations.md
│   ├── parameters/
│   │   └── <tool>-parameters.md
│   ├── tools/
│   │   └── <tool>.md
│   └── tool-family/
│       └── <service>.md
└── baseline-manifest.json  # Inventory of all baseline files
```

## Selected Test Namespaces

The smoke tests use **2 small, stable Azure namespaces**:

1. **azure-quota** - Simple namespace with ~3-5 tools, stable structure
2. **azure-redis** - Medium-complexity namespace with ~8-10 tools, representative features

These were chosen because they:
- Are small enough to run quickly (< 2 minutes combined)
- Have stable, well-formed output
- Cover representative pipeline features (annotations, parameters, tool families, etc.)
- Rarely change, minimizing baseline maintenance

## Running the Tests

### Run all smoke tests

```bash
dotnet test DocGeneration.PipelineRunner.SmokeTests
```

### Run specific test

```bash
dotnet test DocGeneration.PipelineRunner.SmokeTests --filter "FullyQualifiedName~PipelineProducesExpectedOutput_Quota"
```

### Skip if no baseline

Tests automatically skip gracefully if baseline fixtures don't exist (e.g., in CI without prior generation). This is by design - the tests validate behavior when baselines are present, but don't block builds when baselines haven't been captured yet.

## Updating Baselines

When intentional changes are made to the pipeline that alter output structure or content, baselines need to be updated:

### 1. Regenerate output for test namespaces

```bash
# From repo root
./start.sh quota      # Generate azure-quota
./start.sh redis      # Generate azure-redis
```

### 2. Update baseline fixtures

```bash
# Run the test with BASELINE_UPDATE=true environment variable
BASELINE_UPDATE=true dotnet test DocGeneration.PipelineRunner.SmokeTests
```

This will:
- Run the pipeline on test namespaces
- Capture new output as baseline fixtures
- Update `baseline-manifest.json`
- Overwrite existing baselines in `Baselines/` directory

### 3. Review and commit changes

```bash
git diff mcp-tools/DocGeneration.PipelineRunner.SmokeTests/Baselines/
# Review the changes carefully
git add mcp-tools/DocGeneration.PipelineRunner.SmokeTests/Baselines/
git commit -m "Update smoke test baselines after [description of change]"
```

## Test Behavior

### When baselines exist

- Tests run the pipeline on each test namespace
- Generated output is compared against committed baselines
- Any difference (missing files, content mismatch, extra files) causes test failure
- Failure messages show file-by-file diffs for easy debugging

### When baselines don't exist

- Tests skip gracefully with informational message
- CI builds pass (tests don't block when baselines missing)
- Explicit message guides developers to capture baselines first

## Dependencies

These tests have minimal dependencies by design:

- **PipelineRunner** - The pipeline orchestrator being tested
- **TestInfrastructure** - Shared test utilities (`ProjectRootFinder`, etc.)
- **No AI/network dependencies** - Uses minimal namespaces that don't require AI steps (or runs with `--skip-ai`)

## CI Integration

The smoke tests are designed for CI:

1. **Fast** - Complete in under 5 minutes (often < 2 minutes)
2. **Deterministic** - No AI or network dependencies when using `--skip-ai`
3. **Skip gracefully** - Don't fail if baselines missing (explicit detection test reports status)
4. **Clear failures** - Show file-by-file diffs when output doesn't match baseline

## Maintenance

### When to update baselines

- After intentional pipeline changes that alter output structure
- After upgrading dependencies that affect rendering (Handlebars, etc.)
- After changes to templates, prompts, or configuration files

### When NOT to update baselines

- For test-only changes that don't affect pipeline output
- For internal refactors with no behavior change
- For changes to unrelated namespaces (quota and redis only)

### Baseline size considerations

Baselines are committed to the repo and should remain small:

- Each namespace baseline: ~50-200 KB
- Total baseline size: < 500 KB
- If baselines grow significantly, consider switching to a smaller test namespace

## Troubleshooting

### Test fails with "Output directory not found"

The pipeline didn't run successfully. Check:
- Is the test namespace valid? (quota, redis)
- Are there build errors preventing pipeline execution?
- Check test output for pipeline error messages

### Test fails with "Baseline directory not found"

Baselines haven't been captured yet. Run with `BASELINE_UPDATE=true` to create them.

### Test fails with file content mismatches

The pipeline output has changed. Either:
1. This is expected (intentional change) → Update baselines
2. This is unexpected (regression) → Investigate the diff and fix the pipeline

### Tests skip in CI

This is normal if baselines haven't been committed yet. The tests will run once baselines are captured and committed.

## Related Documentation

- [Pipeline Architecture](../../docs/ARCHITECTURE.md)
- [Start Scripts](../../docs/START-SCRIPTS.md)
- [E2E Tests](../DocGeneration.E2E.Tests/) - Validates existing generated output (doesn't run pipeline)
