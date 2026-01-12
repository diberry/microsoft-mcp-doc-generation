# Example Prompt Validator

The Example Prompt Validator is a standalone package that validates generated example prompts to ensure they contain all required tool parameters.

## Purpose

When generating example prompts for Azure MCP tools, it's important to ensure that the prompts include all required parameters. This validator:

1. **Checks example prompts** for the presence of required tool parameters
2. **Excludes common parameters** like `subscription-id`, `tenant-id`, `auth-method`, and `retry-*` parameters
3. **Generates validation reports** showing which prompts are missing parameters
4. **Provides console output** with colored status indicators (✅ valid, ❌ invalid)

## Usage

### Command Line

Enable validation when running the documentation generator:

```bash
# Run with validation enabled
./start.sh --validate

# Or directly with dotnet
cd docs-generation
dotnet run --project CSharpGenerator/CSharpGenerator.csproj --configuration Release -- \
    generate-docs \
    ../generated/cli/cli-output.json \
    ../generated/tools \
    --annotations \
    --example-prompts \
    --validate-prompts
```

### Output

The validator provides two types of output:

1. **Console Output**: Real-time validation status for each tool
```
=== Validating Example Prompts ===
  ✅ azure storage account create        (5 prompts, all valid)
  ❌ azure vm create                      (2/5 prompts missing params: vm-name, location)
  ✅ azure cosmosdb create                (5 prompts, all valid)

=== Validation Summary ===
Total tools: 150
Tools with example prompts: 125
Valid tools: 120 (96.0%)
Invalid tools: 5 (4.0%)
```

2. **Validation Report**: Markdown file at `generated/logs/validation-report.md`
```markdown
# Example Prompt Validation Report

**Generated:** 2026-01-12 19:35:00 UTC

## Summary

- **Total tools:** 150
- **Tools with example prompts:** 125
- **Valid tools:** 120 (96.0%)
- **Invalid tools:** 5 (4.0%)

## Tools with Missing Parameters

| Tool Command | Missing Parameters | Invalid Prompts |
|--------------|-------------------|-----------------|
| `azure vm create` | vm-name, location | 2/5 |
| `azure network vnet create` | vnet-name | 1/5 |
```

## Excluded Parameters

The following common parameters are automatically excluded from validation:

- `subscription-id`, `subscription`
- `tenant-id`, `tenant`
- `auth-method`, `auth`, `authentication`
- `retry-max-attempts`, `retry-delay`, `retry`
- `output`, `output-format`
- `verbose`, `debug`, `help`

These parameters are infrastructure-level and not specific to individual tool operations.

## Architecture

### Package Structure

```
ExamplePromptValidator/
├── ExamplePromptValidator.csproj   # Project file
└── PromptValidator.cs              # Core validation logic
```

### Key Classes

- **PromptValidator**: Static class with validation methods
  - `ValidatePrompt()`: Validates a single prompt
  - `ValidatePrompts()`: Validates multiple prompts with aggregation
  
- **ValidationResult**: Result for a single prompt
  - `IsValid`: Whether the prompt contains all required parameters
  - `MissingParameters`: List of parameters not found in the prompt
  - `ErrorMessage`: Error message if validation failed

- **AggregatedValidationResult**: Aggregated results for multiple prompts
  - `TotalPrompts`: Total number of prompts validated
  - `ValidPrompts`: Count of valid prompts
  - `InvalidPrompts`: Count of invalid prompts
  - `AllMissingParameters`: Unique list of all missing parameters

### Integration Points

1. **DocumentationGenerator.cs** (line ~270):
   ```csharp
   if (validatePrompts && generateExamplePrompts && examplePromptsDir != null)
   {
       await ValidateExamplePromptsAsync(transformedData, examplePromptsDir);
   }
   ```

2. **Program.cs** (line ~145):
   ```csharp
   var validatePrompts = args.Contains("--validate-prompts");
   ```

3. **start.sh** (line ~10):
   ```bash
   case $1 in
       --validate)
           VALIDATE_PROMPTS="--validate-prompts"
           ;;
   esac
   ```

## Testing

Run the test suite:

```bash
cd docs-generation
dotnet test ExamplePromptValidator.Tests/ExamplePromptValidator.Tests.csproj
```

### Test Coverage

- ✅ Valid prompts with all required parameters
- ✅ Invalid prompts missing required parameters
- ✅ Exclusion of common parameters
- ✅ Parameter name format variations (dashes vs spaces)
- ✅ Multiple prompt aggregation
- ✅ Empty prompt handling
- ✅ No required parameters scenario

## Design Decisions

### Why Exclude Common Parameters?

Common parameters like `subscription-id` and `auth-method` are:
- Required for Azure authentication and infrastructure
- Not specific to individual tool operations
- Often implicit in the user's context
- Would create noise if validated for every prompt

### Why Validate Only Required Parameters?

- Optional parameters don't need to be in every example prompt
- Required parameters are essential for tool operation
- Keeps validation focused on critical elements

### Why Generate a Report?

- Enables batch review of validation issues
- Provides documentation for maintainers
- Allows tracking validation quality over time
- Can be integrated into CI/CD pipelines

## Future Enhancements

Potential improvements:

1. **Custom exclusion lists** per service or tool
2. **Parameter synonym detection** (e.g., "rg" for "resource-group")
3. **Confidence scoring** based on partial matches
4. **Auto-fix suggestions** for missing parameters
5. **Integration with CI/CD** to fail builds on validation errors
