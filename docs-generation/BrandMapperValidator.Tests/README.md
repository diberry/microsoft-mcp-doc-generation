# BrandMapperValidator.Tests

Unit tests for the BrandMapperValidator tool.

## Overview

BrandMapperValidator validates that all MCP CLI namespaces have brand mappings. If new namespaces are found, it uses GenAI to suggest brand mapping entries. This test suite verifies the validator's core functionality.

## Running Tests

```bash
# Run just this project
dotnet test docs-generation/BrandMapperValidator.Tests/BrandMapperValidator.Tests.csproj

# Run all solution tests
dotnet test docs-generation.sln

# Release mode (matches CI — treats warnings as errors)
dotnet test docs-generation.sln --configuration Release
```

## Test Structure

The tests focus on top-level inputs and outputs of the BrandMapperValidator command-line application:

| Test | Purpose |
|------|---------|
| `ValidatorExits_WithSuccess_WhenAllNamespacesAreMapped` | Verifies exit code 0 when all namespaces have brand mappings |
| `ValidatorExits_WithCode2_WhenNewNamespacesAreFound` | Verifies exit code 2 and generates suggestions when unmapped namespaces exist |
| `ValidatorExits_WithError_WhenCliFileNotFound` | Verifies error handling when CLI output file is missing |
| `ValidatorExits_WithError_WhenCliOutputIsEmpty` | Verifies error handling when CLI output contains no tools |
| `Validator_CreatesOutputDirectory_WhenNotExists` | Verifies output directory creation |
| `Validator_ExtractsNamespaces_FromCommandPrefix` | Verifies namespace extraction from command strings |

## Test Data Files

Tests use synthetic fixture data in `TestData/`:

| File | Purpose |
|------|---------|
| `cli-output-test.json` | Sample CLI output with 3 namespaces (advisor, storage, newservice). Contains both mapped and unmapped namespaces. |
| `brand-mapping-test.json` | Sample brand mapping file with 2 entries (advisor, storage). Used to test validation logic. |

## Test Approach

Since `BrandMapperValidator` is a console application with an internal `Main` method, tests:

1. **Run the validator as a separate process** using `Process.Start` with `dotnet` CLI
2. **Pass command-line arguments** (`--cli-output`, `--brand-mapping`, `--output`)
3. **Verify exit codes** (0 = success, 1 = error, 2 = needs review)
4. **Validate output files** by deserializing JSON and checking properties
5. **Use temporary directories** for output to avoid test pollution

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success - All namespaces have brand mappings |
| 1 | Error - Invalid input, missing files, or runtime errors |
| 2 | Needs Review - New brand mappings generated, requires human approval |

## Shared Infrastructure

| File | Purpose |
|------|---------|
| `TestHelpers.cs` | Path resolution (`TestDataPath`), temp directory management (`TempDir`), helper methods |

## Notes

- Tests run the actual validator executable to test end-to-end behavior
- GenAI functionality is tested indirectly (fallback placeholders when credentials missing)
- Focus is on input validation, namespace extraction, and output generation
- Each test uses isolated temp directories for output files
