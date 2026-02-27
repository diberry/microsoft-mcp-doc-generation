# Sage's Project History

## What I Know About AI Generation in This Project

### AI-Powered Generators

**ExamplePromptGeneratorStandalone** (`docs-generation/ExamplePromptGeneratorStandalone/`):
- Generates 5 natural language example prompts per MCP tool
- Output: `./generated/example-prompts/{tool}-example-prompts.md`
- Processing: sequential (208 tools × ~2-4 seconds = 10-15 min total)
- Input prompts saved to `./generated/example-prompts-prompts/{tool}-input-prompt.md` for debugging

**HorizontalArticleGenerator** (`docs-generation/HorizontalArticleGenerator/`):
- Generates one overview article per namespace (52 total)
- Pipeline: AI response → JSON parse → Validate → ApplyTransformations → Render
- `ArticleContentProcessor.cs` handles all validation and transformation (service-agnostic)
- Output: `./generated-{namespace}/horizontal-articles/{family}.md`

**ToolFamilyCleanup** (`docs-generation/ToolFamilyCleanup/`):
- AI-based cleanup of tool family metadata
- Uses higher-quality model (`TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME`)

**ToolGeneration_Improved** (`docs-generation/ToolGeneration_Improved/`):
- AI improvements to raw tool documentation from step 3

### Prompt Files

- `docs-generation/prompts/` — Shared prompt directory
- Project-specific: `HorizontalArticleGenerator/prompts/`, `ExamplePromptGeneratorStandalone/` (embedded resources)
  - `horizontal-article-system-prompt.txt` — JSON schema + content rules
  - `horizontal-article-user-prompt.txt` — Handlebars template with tool data
  - Example prompt: `system-prompt.txt`, `user-prompt.txt` (embedded)

### Common AI Response Issues I've Debugged

1. **Preamble text**: AI returns "STEP 1: ... STEP 2: ... {json}" — parser extracts `{...}` from the response
2. **Smart quotes**: AI returns `"` and `"` — must be cleaned to `"`
3. **HTML entities**: AI returns `&amp;` — must be decoded
4. **Rate limiting**: 429 errors — handled by retry logic in `GenerativeAIClient.cs` (5 retries, exponential backoff)
5. **Fabricated RBAC roles**: AI generates roles like "Azure Advisor Administrator" — caught by `ArticleContentProcessor` via suffix pattern matching

### ArticleContentProcessor Validations (All Service-Agnostic)

- Strip trailing periods from titles, capabilities, short descriptions
- Fix broken sentences (period-before-lowercase char)
- Strip `learn.microsoft.com` prefix from links
- Remove fabricated `/docs` URL patterns
- Deduplicate additional links matching serviceDocLink
- Detect fabricated RBAC roles via suffix pattern matching ("Administrator", generic prefixes)
- Validate capability-to-tool ratio
- Validate best practice count minimums

### Environment Setup

```ini
# docs-generation/.env
FOUNDRY_API_KEY="your-api-key-here"
FOUNDRY_ENDPOINT="https://your-resource.openai.azure.com/"
FOUNDRY_MODEL_NAME="gpt-4o-mini"
FOUNDRY_MODEL_API_VERSION="2025-01-01-preview"
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME="gpt-4o"
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION="2025-01-01-preview"
```

### Testing AI Features (Time Warning)

Never run full generation to test AI changes. Cancel after 2-3 successful outputs:
```bash
dotnet run --project ExamplePromptGeneratorStandalone -- [args]
# Watch for 3 ✅ checkmarks, then Ctrl+C
# Verify output files in ./generated/example-prompts/
```

### Microsoft Foundry Branding (AD-013)

All AI-generated content should use "Microsoft Foundry" not "Azure AI Foundry". The system prompts should specify this if the AI might generate branding-sensitive content.
