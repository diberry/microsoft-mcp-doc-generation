# ImprovedToolGenerator

## Overview

ImprovedToolGenerator applies AI-based improvements to composed tool documentation files using Azure OpenAI. It enforces Microsoft style guide standards and Azure-specific conventions. This forms the third stage of the separated tool generation process.

## Purpose

This package takes composed tool files and:
- Reviews content for clarity and readability
- Ensures technical accuracy
- Applies Microsoft writing style guidelines
- Enhances example prompts to be more realistic
- Improves parameter descriptions

The improvements are powered by Azure OpenAI with custom prompts focused on Microsoft documentation standards.

## Input Requirements

This generator assumes the following files exist:
1. **Composed tool files** in `./generated/tools-composed/` (from ComposedToolGenerator)
2. **Azure OpenAI credentials** in environment variables or `.env` file

## Output

Files are generated in the `./generated/tools-ai-improved/` directory with AI-enhanced content following Microsoft guidelines.

## Usage

### Prerequisites

Set the following environment variables (or create a `.env` file in the `docs-generation` directory):

```bash
FOUNDRY_API_KEY=your-azure-openai-api-key
FOUNDRY_ENDPOINT=https://your-resource.openai.azure.com/
FOUNDRY_MODEL_NAME=your-deployment-name
```

### Command Line

```bash
dotnet run --project ImprovedToolGenerator \
  <composed-tools-dir> \
  <output-dir> \
  [max-tokens]
```

### Arguments

- `composed-tools-dir` - Directory containing composed tool files
- `output-dir` - Output directory for AI-improved files (typically `./generated/tools-ai-improved`)
- `max-tokens` - (Optional) Maximum tokens for AI response (default: 8000)

### Example

```bash
cd docs-generation
dotnet run --project ImprovedToolGenerator \
  ../generated/tools-composed \
  ../generated/tools-ai-improved \
  8000
```

## AI Prompts

The generator uses two prompt files located in the `Prompts/` directory:

### system-prompt.txt

Defines the AI's role and guidelines:
- Microsoft Style Guide standards
- Technical accuracy requirements
- Formatting guidelines
- Azure-specific conventions
- What NOT to change (frontmatter, parameter names, etc.)

### user-prompt-template.txt

Template for the user prompt sent with each file:
- Asks the AI to review and improve the content
- Focuses on specific improvement areas
- Instructs AI to return only markdown content

## Error Handling

### Truncation Errors

If the AI response is truncated due to token limits:
- The original composed file is saved instead
- A warning is displayed
- Processing continues for other files

### Rate Limiting

The generator adds a 100ms delay between AI requests to avoid rate limiting.

### Missing Credentials

If Azure OpenAI credentials are not found:
- An error is displayed with required environment variables
- The program exits with code 1

## Output Format

Improved tool files maintain the same structure as composed files but with enhanced content:
- Clearer, more concise descriptions
- Improved example prompts with realistic Azure names
- Better parameter descriptions
- Consistent with Microsoft style guidelines

## Dependencies

- **Shared** - Common utilities and models
- **GenerativeAI** - Azure OpenAI client integration

## Design Notes

- Uses existing GenerativeAI package for AI integration
- Processes files sequentially to manage rate limits
- Handles token truncation gracefully
- Maintains original file structure and formatting
- Independent from existing documentation generation
- Safe to run multiple times (idempotent)

## Customization

To customize the improvement behavior, edit the prompt files:

- `Prompts/system-prompt.txt` - Modify the guidelines and requirements
- `Prompts/user-prompt-template.txt` - Change the improvement focus areas

## Performance Considerations

- Processing time depends on the number of files and AI response time
- Typical processing: ~2-5 seconds per file
- For 208 tools: ~7-17 minutes total
- Rate limiting delays add ~20 seconds total

## Integration Plan

To integrate into the main documentation generation pipeline:
1. Run RawToolGenerator after CLI extraction
2. Run existing generators (annotations, parameters, example prompts)
3. Run ComposedToolGenerator to create complete files
4. Run ImprovedToolGenerator for AI enhancements (this package)
5. Use improved files as final output

## Troubleshooting

### "Azure OpenAI configuration incomplete"

Ensure all required environment variables are set:
- FOUNDRY_API_KEY
- FOUNDRY_ENDPOINT
- FOUNDRY_MODEL_NAME

### "Prompt file not found"

The generator looks for prompt files in multiple locations. Ensure:
- Files exist in `ImprovedToolGenerator/Prompts/`
- Working directory is set correctly

### "Response was truncated"

If many files are truncated:
- Increase `max-tokens` parameter (try 12000 or 16000)
- Consider breaking up very large files
- Check if composed files have excessive content
