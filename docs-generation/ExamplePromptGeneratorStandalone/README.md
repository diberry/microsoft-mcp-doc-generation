# ExamplePromptGeneratorStandalone

A standalone .NET console application that generates example prompts for Azure MCP tools using Azure OpenAI.

## Purpose

Pulls the example prompt generation functionality out of CSharpGenerator into its own independent .NET package. This allows for:
- **Modularity**: Standalone tool that doesn't depend on the larger CSharpGenerator project
- **Reusability**: Can be invoked independently or integrated into other workflows
- **Maintainability**: Cleaner separation of concerns

## Inputs

1. **CLI Output JSON** (`cli-output.json`)
   - Path to the MCP CLI tool definitions
   - Contains tool commands, descriptions, and required/optional parameters

2. **Output Directory**
   - Target directory for generated files
   - Creates subdirectories as needed

3. **CLI Version** (optional)
   - Version string for metadata in generated files

4. **Templates Directory** (default: `./templates`)
   - Location of Handlebars templates:
     - `system-prompt-example-prompt.txt` - System prompt for AI
     - `user-prompt-example-prompt.txt` - User prompt template
     - `example-prompts-template.hbs` - Markdown template

## Outputs

1. **Example Prompts Files** (`{command}-example-prompts.md`)
   - Generated markdown files with 5 AI-generated example prompts per tool
   - Located in `example-prompts/` subdirectory
   - Includes frontmatter metadata, command comment, and required parameters comment

3. **Input Prompt Files** (`{command}-input-prompt.md`)
   - User prompts (with parameters) that were sent to Azure OpenAI
   - Located in `example-prompts-prompts/` subdirectory
   - For debugging and validation purposes

4. **Raw AI Output Files** (`{command}-raw-output.txt`)
   - Pure JSON response extracted from Azure OpenAI
   - Located in `example-prompts-raw-output/` subdirectory
   - Shows what the LLM returned (with extraction strategy logged)
   - Useful for debugging JSON parse failures

## Processing Flow

The tool processes each tool **sequentially** (not in batch):
1. Generate custom user prompt from template with tool-specific parameters
2. Call Azure OpenAI with system + user prompts
3. Extract JSON from LLM response (handles preamble text, code blocks, reasoning)
4. Parse JSON response (5 example prompts)
5. Save input prompt, raw output (JSON only), and example prompts
6. Move to next tool

**JSON Extraction Strategies (in order):**
1. Look for `\`\`\`json` code block (most explicit)
2. Find the LAST `\`\`\`` code block (LLM often puts final answer at end)
3. Find last complete JSON object using brace matching

This sequential approach ensures progress is saved incrementally and each tool gets a customized prompt.

See `RESPONSE-FORMAT-LOGIC.md` for detailed documentation of the extraction logic.

## Configuration

Requires Azure OpenAI credentials via environment variables:
- `FOUNDRY_API_KEY` - Azure OpenAI API key
- `FOUNDRY_ENDPOINT` - Azure OpenAI endpoint URL
- `FOUNDRY_MODEL_NAME` - Model deployment name (e.g., "gpt-4o-mini")
- `FOUNDRY_MODEL_API_VERSION` - API version (optional)

Can be set via `.env` file or environment variables.

## Usage

### Generate All Tools

```bash
cd docs-generation
pwsh ./3-Generate-ExamplePrompts.ps1 -OutputPath ../generated
```

This will:
1. Build all .NET packages
2. Generate example prompts for all 231 tools (15-30 minutes)
3. Validate the generated prompts

### Generate a Single Tool 

```bash
cd docs-generation
pwsh ./GenerateExamplePrompt-One.ps1 -ToolCommand "keyvault secret create" -OutputPath "../generated"
```

This will:
1. Filter CLI output to just the specified tool
2. Generate example prompts for that tool only (~3-5 seconds)
3. Validate the prompts
4. Show all generated files with content preview

**Options:**
- `-SkipValidation` - Skip the validation step (only generate)
- `-ToolCommand` - Tool command to test (e.g., "storage account list", "acr registry list")

**Output includes:**
- Input prompt sent to Azure OpenAI
- Raw AI response (with extraction strategy logged)
- Generated example prompts file
- Validation report

### Direct .NET Usage

```bash
dotnet run --project ExamplePromptGeneratorStandalone -- \
  /path/to/cli-output.json \
  /path/to/output \
  "2.0.0-beta.17"
```

## Architecture

- **Models**: Tool, Option, CliOutput, ExamplePromptsResponse
- **Generators**: ExamplePromptGenerator (AI generation + JSON parsing)
- **Utilities**: FrontmatterUtility, TemplateEngine
- **Program**: Command-line entry point with argument parsing

## Notes

- Does NOT depend on CSharpGenerator project
- Uses GenerativeAI project for Azure OpenAI integration
- Handles JSON parsing from LLM responses (code blocks, preamble text, reasoning)
- Cleans AI-generated text (smart quotes → straight quotes, HTML entities → plain text)
- Raw output files contain only extracted JSON (no LLM reasoning/verification steps)

## Debugging

### JSON Parse Failures

If a tool fails with "JSON parse failed", check:
1. **Raw output file**: `generated/example-prompts-raw-output/{tool}-raw-output.txt`
   - Should contain ONLY JSON
   - Console shows which extraction strategy was used
2. **Input prompt file**: `generated/example-prompts-prompts/{tool}-input-prompt.md`
   - Shows what was sent to Azure OpenAI
3. **LLM Response Format**: The system prompt tells LLM to return ONLY JSON, but sometimes includes reasoning/verification text
   - Extraction logic handles this automatically
   - See `RESPONSE-FORMAT-LOGIC.md` for details

### Testing Changes

Use `GenerateExamplePrompt-One.ps1` to quickly test a single tool without running all 231 tools:

```bash
cd docs-generation
pwsh ./GenerateExamplePrompt-One.ps1 -ToolCommand "storage account list" -OutputPath "../generated"
```

This shows:
- ✅ Extraction strategy used (`📝 JSON extracted using brace matching`)
- ✅ Raw AI output preview
- ✅ Generated prompts
- ✅ Validation results
