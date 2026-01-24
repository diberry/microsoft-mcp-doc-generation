# Tool Family Cleanup

Independent .NET package for cleaning up Azure MCP tool family documentation files using LLM-based processing with Microsoft style guide standards.

## Overview

This tool takes generated tool family markdown files and applies AI-powered cleanup to ensure they meet Microsoft style guide standards and Azure MCP-specific documentation conventions.

## Features

- **Independent Operation**: Works independently of other documentation generation processes
- **Configurable Paths**: All input/output directories are configurable with sensible defaults
- **Prompt Preservation**: Saves individual prompts for each file for review and iteration
- **LLM-Powered**: Uses Azure OpenAI to apply style guide standards
- **Markdown-Only Output**: Validates and extracts only markdown output from LLM responses

## Architecture

```
ToolFamilyCleanup/
├── Program.cs                          # CLI entry point with argument parsing
├── Services/
│   ├── CleanupConfiguration.cs        # Configuration with default paths
│   └── CleanupGenerator.cs            # Main cleanup logic
└── ToolFamilyCleanup.csproj           # Project file
```

## Configuration

### Default Paths

- **Input Directory**: `./generated/multi-page` - Tool family markdown files to clean
- **Prompts Output**: `./generated/tool-family-cleanup-prompts` - Individual prompts saved here
- **Cleanup Output**: `./generated/tool-family-cleanup` - Cleaned markdown files saved here

### Environment Variables

The tool requires Azure OpenAI configuration via environment variables or `.env` file:

```bash
FOUNDRY_API_KEY=your-api-key-here
FOUNDRY_ENDPOINT=https://your-resource.openai.azure.com/
FOUNDRY_MODEL_NAME=your-deployment-name
```

Create a `.env` file in the `docs-generation` directory with these values.

## Usage

### Basic Usage (Default Paths)

```bash
cd docs-generation/ToolFamilyCleanup
dotnet run
```

### Custom Paths

```bash
dotnet run -- --input-dir /path/to/input --prompts-dir /path/to/prompts --output-dir /path/to/output
```

### Command-Line Options

- `-i, --input-dir <path>` - Input directory containing tool family markdown files (default: `./generated/multi-page`)
- `-p, --prompts-dir <path>` - Directory to save generated prompts (default: `./generated/tool-family-cleanup-prompts`)
- `-o, --output-dir <path>` - Directory to save cleaned markdown files (default: `./generated/tool-family-cleanup`)
- `-h, --help` - Display help message

## Prompt Customization

The tool uses two prompt files that can be customized:

### System Prompt
**File**: `docs-generation/prompts/tool-family-cleanup-system-prompt.txt`

This defines the overall task and guidelines for the LLM. Edit this file to:
- Add Azure MCP-specific style requirements
- Adjust Microsoft style guide emphasis
- Modify output requirements

### User Prompt Template
**File**: `docs-generation/prompts/tool-family-cleanup-user-prompt.txt`

This is the per-file prompt template. Available placeholders:
- `{{FILENAME}}` - Name of the file being processed
- `{{CONTENT}}` - Content of the file being processed

## Output

The tool generates three types of output:

1. **Prompts** (`tool-family-cleanup-prompts/`): Individual prompt files for each tool family file
   - Format: `{filename}-prompt.txt`
   - Contains both system and user prompts
   - Useful for reviewing what was sent to the LLM

2. **Cleaned Markdown** (`tool-family-cleanup/`): Cleaned markdown files
   - Same filenames as input files
   - Contains LLM-processed, style-guide-compliant markdown
   - Ready for review and integration

3. **Error Logs** (when processing fails): Error details for failed files
   - Format: `{filename}-error.txt`
   - Contains invalid LLM output or error messages

## Example Workflow

1. **Generate base documentation** (existing process creates files in `./generated/multi-page`)

2. **Run cleanup tool**:
   ```bash
   cd docs-generation/ToolFamilyCleanup
   dotnet run
   ```

3. **Review prompts** in `./generated/tool-family-cleanup-prompts/`
   - Check what was sent to the LLM
   - Iterate on prompt files if needed

4. **Review cleaned files** in `./generated/tool-family-cleanup/`
   - Compare with originals
   - Verify style improvements
   - Check for accuracy

5. **Iterate if needed**:
   - Adjust prompts in `docs-generation/prompts/tool-family-cleanup-*.txt`
   - Re-run the tool
   - Review results

## Building

```bash
cd docs-generation/ToolFamilyCleanup
dotnet build
```

## Integration with Build Process

This tool is designed to be independent and can be integrated into the build process as a separate step:

```bash
# Step 1: Generate base documentation (existing process)
pwsh ./Generate-MultiPageDocs.ps1

# Step 2: Run cleanup (new independent step)
cd ToolFamilyCleanup
dotnet run
```

## Notes

- The tool processes only files in the root of the input directory (not subdirectories)
- This targets tool family files like `acr.md`, `storage.md`, etc.
- Subdirectories like `annotations/`, `parameters/`, etc. are not processed
- Each file is processed independently
- LLM token limit is set to 16,000 to handle large files
- Failed files are logged but don't stop the overall process

## Troubleshooting

### "Input directory not found"
- Ensure you've run the base documentation generation first
- Check that the input path is correct
- Use `--input-dir` to specify a custom path

### "Missing required environment variables"
- Create a `.env` file in `docs-generation/` directory
- Or set environment variables for Azure OpenAI credentials
- See Configuration section above

### "Invalid markdown output"
- Check the error log in the output directory
- Review the prompt in the prompts directory
- Consider adjusting the system prompt for better guidance
- Some files may need manual review

## Future Enhancements

Potential improvements:
- Parallel processing for faster execution
- Diff generation to show changes
- Automated validation against style guide rules
- Integration tests with sample files
