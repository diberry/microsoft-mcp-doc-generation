# Example Prompts Generation

The documentation generator can create AI-generated example prompts for each Azure MCP tool using Azure OpenAI.

## Requirements

### 1. Azure OpenAI Configuration

Create a `.env` file in the `docs-generation` directory with your Azure OpenAI credentials:

```bash
cd docs-generation
cp sample.env .env
```

Then edit `.env` and fill in your values:

```dotenv
FOUNDRY_API_KEY="your-api-key-here"
FOUNDRY_ENDPOINT="https://your-resource.openai.azure.com/"
FOUNDRY_MODEL_NAME="gpt-4.1-mini"
FOUNDRY_MODEL_API_VERSION="2025-01-01-preview"
```

**Required variables:**
- `FOUNDRY_API_KEY` - Your Azure OpenAI API key
- `FOUNDRY_ENDPOINT` - Your Azure OpenAI endpoint URL
- `FOUNDRY_MODEL_NAME` - The deployment name (e.g., "gpt-4.1-mini")

### 2. Enable Example Prompts Generation

The feature is **enabled by default** via the `-ExamplePrompts $true` parameter.

To explicitly disable it:
```bash
pwsh ./docs-generation/Generate-MultiPageDocs.ps1 -ExamplePrompts $false
```

## Edit Prompt Templates

If you need to adjust how example prompts are generated, edit these files:

- System prompt: [docs-generation/prompts/system-prompt-example-prompt.txt](../docs-generation/prompts/system-prompt-example-prompt.txt)
- User prompt template: [docs-generation/prompts/user-prompt-example-prompt.txt](../docs-generation/prompts/user-prompt-example-prompt.txt)
- Output formatting template (Handlebars): [docs-generation/templates/example-prompts-template.hbs](../docs-generation/templates/example-prompts-template.hbs)

The generator (`ExamplePromptGenerator`) loads the system and user prompt templates from the paths above and renders results using the Handlebars template.

## How It Works

When enabled:

1. The generator checks for Azure OpenAI credentials in environment variables or `.env` file
2. **If credentials are missing**: A warning is displayed, example prompts are skipped, and documentation generation continues normally
3. **If credentials are present**: For each tool, it generates 3 example prompts using AI:
   - Beginner-level prompt
   - Intermediate-level prompt  
   - Advanced-level prompt
4. Output files are saved to `generated/example-prompts/{service-area}-{tool-name}.md`

**Note**: Missing credentials will NOT cause generation to fail. The system gracefully skips example prompt generation and continues with all other documentation generation tasks.

## Output Format

Each example prompts file contains:
- Tool name and description
- Three example prompts with different complexity levels
- Each prompt is designed to be realistic and useful

Example output: `generated/example-prompts/keyvault-secret-get.md`

## Troubleshooting

### Empty `example-prompts` Directory

If the directory is created but empty:

1. **Check logs**: Look for warnings in the console output or in `generated/logs/`
   - You should see: `⚠️  WARNING: ExamplePromptGenerator failed to initialize`
2. **Check credentials**: If you want prompts generated, ensure `.env` file exists in `docs-generation/` with valid Azure OpenAI credentials
3. **Verify format**: Ensure `.env` entries match the required format (see Configuration section above)
4. **Re-run generation**: After adding credentials, run the generator again

### Expected Behavior

- **With credentials**: Example prompts are generated, files appear in `generated/example-prompts/`
- **Without credentials**: Warning displayed, example prompts skipped, but all other documentation generates successfully

## Standalone Generation

To generate only example prompts (without regenerating all docs):

```bash
# Set up environment
cd docs-generation
source <(grep -v '^#' .env | xargs -d '\n' -I {} echo export {})

# Run generator
bash Generate-ExamplePrompts.sh
```

## Cost Considerations

Example prompt generation makes API calls to Azure OpenAI:
- ~200 tools × 3 prompts each = ~600 API calls
- Each call uses ~100-200 tokens
- Estimated cost: $0.10-0.50 depending on model and pricing

Consider using a smaller/cheaper model like `gpt-4.1-mini` for cost efficiency.
