# Example Prompt Validator

The Example Prompt Validator is a standalone package that uses LLM (Large Language Model) to validate generated example prompts with rich context awareness.

## Purpose

When generating example prompts for Azure MCP tools, it's important to ensure that the prompts include all required parameters. This validator:

1. **Uses LLM for intelligent validation** - Leverages Azure OpenAI to understand context and validate prompts
2. **Provides full tool context** - Passes complete tool documentation including description, parameters, and metadata
3. **Handles natural language variations** - LLM understands different ways parameters can be expressed
4. **Excludes infrastructure parameters** - Automatically filters out subscription, tenant, auth, and retry parameters
5. **Generates detailed reports** - Provides per-prompt validation with specific missing parameter information

## Architecture

### LLM-Based Validation

Unlike simple text-matching approaches, this validator uses the GenerativeAI package to:
- Read complete tool files from `generated/tools/` directory
- Pass full context to Azure OpenAI for intelligent validation
- Get structured validation results with specific feedback
- Handle natural language variations in how parameters are expressed

### Required Components

- **GenerativeAI package** - For Azure OpenAI integration
- **Complete tool files** - Generated with `--complete-tools` flag
- **Validation prompts** - System and user prompts in `docs-generation/prompts/`
- **Azure OpenAI configuration** - API key, endpoint, and deployment name

## Usage

### Command Line

Enable validation when running the documentation generator:

```bash
# Run with validation enabled (requires --complete-tools)
./start.sh --validate

# Or directly with dotnet
cd docs-generation
dotnet run --project CSharpGenerator/CSharpGenerator.csproj --configuration Release -- \
    generate-docs \
    ../generated/cli/cli-output.json \
    ../generated/tools \
    --annotations \
    --example-prompts \
    --complete-tools \
    --validate-prompts
```

**Note:** Validation requires both `--example-prompts` and `--complete-tools` flags to be set.

### Azure OpenAI Configuration

Set the following environment variables or create a `.env` file:

```bash
FOUNDRY_API_KEY=your-api-key
FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com/
FOUNDRY_MODEL_NAME=your-deployment-name
```

### Output

The validator provides two types of output:

1. **Console Output**: Real-time validation status for each tool
```
=== Validating Example Prompts with LLM ===
  ✅ azure storage account create        (5 prompts, all valid)
  ❌ azure vm create                      (2/5 prompts invalid)
  ✅ azure cosmosdb create                (5 prompts, all valid)

=== Validation Summary ===
Total tools: 150
Tools with example prompts: 125
Validated: 122
Valid tools: 118 (96.7%)
Invalid tools: 4 (3.3%)
Skipped: 3
```

2. **Validation Report**: Detailed markdown file at `generated/logs/validation-report.md`
```markdown
# Example Prompt Validation Report (LLM-Based)

**Generated:** 2026-01-12 23:35:00 UTC

## Summary

- **Total tools:** 150
- **Tools with example prompts:** 125
- **Validated:** 122
- **Valid tools:** 118 (96.7%)
- **Invalid tools:** 4 (3.3%)
- **Skipped:** 3

## Tools with Issues

### `azure vm create`

**Summary:** 2 of 5 prompts are missing required parameters

**Required Parameters:** vm-name, location, resource-group, image

**Recommendations:**
- Prompt 2 is missing: image
- Prompt 4 is missing: vm-name, image

**Prompt Details:**

| Prompt | Valid | Missing Parameters |
|--------|-------|-------------------|
| Create a VM named 'myvm' in 'westus' | ✅ | - |
| Create a VM in resource group 'rg-prod' | ❌ | vm-name, image |
```

## Excluded Parameters

The following common infrastructure parameters are automatically excluded from validation (defined in the system prompt):

- `subscription-id`, `subscription`
- `tenant-id`, `tenant`
- `auth-method`, `auth`, `authentication`
- `retry-max-attempts`, `retry-delay`, `retry`
- `output`, `output-format`
- `verbose`, `debug`, `help`

## Package Structure

```
ExamplePromptValidator/
├── ExamplePromptValidator.csproj   # Project file (references GenerativeAI)
├── PromptValidator.cs              # LLM-based validation logic
└── README.md                       # This file

docs-generation/prompts/
├── system-prompt-validation.txt    # System prompt for LLM validator
└── user-prompt-validation.txt      # User prompt template
```

## Key Classes

- **PromptValidator**: Main validation class
  - `ValidateWithLLMAsync()`: Validates prompts using LLM with full tool context
  - `IsInitialized()`: Checks if Azure OpenAI is configured
  
- **ValidationResult**: LLM response structure
  - `ToolName`: Name of the validated tool
  - `RequiredParameters`: List of required parameters for the tool
  - `TotalPrompts`: Total number of prompts validated
  - `ValidPrompts`: Count of valid prompts
  - `InvalidPrompts`: Count of invalid prompts
  - `IsValid`: Overall validation status
  - `Validation`: Per-prompt validation details
  - `Summary`: LLM-generated summary
  - `Recommendations`: LLM-generated recommendations

- **PromptValidation**: Per-prompt validation details
  - `Prompt`: The prompt text
  - `IsValid`: Whether this specific prompt is valid
  - `MissingParameters`: Parameters missing from this prompt
  - `FoundParameters`: Parameters found in this prompt

## Integration Points

1. **DocumentationGenerator.cs** (line ~285-305):
   ```csharp
   // Validate after complete tools are generated
   if (validatePrompts && generateExamplePrompts && examplePromptsDir != null && generateCompleteTools)
   {
       await ValidateExamplePromptsAsync(transformedData, examplePromptsDir, toolsDir);
   }
   ```

2. **Program.cs** (line ~145):
   ```csharp
   var validatePrompts = args.Contains("--validate-prompts");
   ```

3. **start.sh** (line ~10-20):
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

- ✅ Constructor and initialization
- ✅ IsInitialized() returns appropriate value based on configuration
- ✅ ValidationResult model instantiation and properties
- ✅ PromptValidation model instantiation and properties

**Note:** Full validation tests require Azure OpenAI configuration and are better suited for integration testing.

## Design Decisions

### Why Use LLM Instead of Text Matching?

1. **Context Awareness**: LLM understands the tool's purpose and parameters in context
2. **Natural Language Understanding**: Handles variations like "storage account 'myacct'" vs "account name: myacct"
3. **Semantic Matching**: Recognizes when a value corresponds to a parameter even if not explicitly named
4. **Rich Feedback**: Provides specific, actionable recommendations
5. **Evolving Validation**: Can be improved by updating prompts without code changes

### Why Read Complete Tool Files?

Complete tool files (`*.complete.md`) contain:
- Full tool description and purpose
- All parameters with descriptions and requirements
- Tool metadata and annotations
- Already generated example prompts for reference

This rich context enables the LLM to make informed validation decisions.

### Why Require --complete-tools Flag?

- Validation needs complete tool files for full context
- Ensures consistency: validation uses the same files users see
- Prevents validation from running without necessary data

## Validation Prompt Structure

### System Prompt (`system-prompt-validation.txt`)

Defines the validator's role and instructions:
- Expert technical validator specializing in Azure MCP
- Strict validation rules (all required parameters must be present)
- Flexible parameter recognition (handles natural language)
- Structured JSON output format

### User Prompt Template (`user-prompt-validation.txt`)

Provides tool context to LLM:
- Tool name, command, and description
- Complete parameter list with requirements
- Tool metadata
- Generated example prompts to validate

## Future Enhancements

Potential improvements:

1. **Batch validation** - Validate multiple tools in a single LLM call
2. **Auto-fix suggestions** - LLM generates corrected prompts
3. **Confidence scoring** - Probability scores for parameter detection
4. **Custom validation rules** - Per-service or per-tool validation criteria
5. **Validation history tracking** - Track improvements over time
6. **CI/CD integration** - Fail builds on validation errors
