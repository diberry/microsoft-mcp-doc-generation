# Horizontal Article Generator

Generates horizontal "how-to" articles for Azure services using AI-generated content. These articles explain how to use Azure MCP Server with specific Azure services.

## Overview

The Horizontal Article Generator is a standalone C# console application that:

1. **Extracts static data** from MCP CLI output (`cli-output.json`)
2. **Generates AI content** using Azure OpenAI to fill in service-specific details
3. **Renders articles** using Handlebars templates
4. **Saves prompts and responses** for debugging and iteration

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    HorizontalArticleGenerator                    │
├─────────────────────────────────────────────────────────────────┤
│  HorizontalArticleProgram.cs    - Entry point, CLI args         │
│  Generators/                                                     │
│    └── HorizontalArticleGenerator.cs - Core generation logic    │
│  Models/                                                         │
│    ├── StaticArticleData.cs     - Data from CLI output          │
│    ├── AIGeneratedArticleData.cs - AI response model            │
│    └── HorizontalArticleTemplateData.cs - Combined template data│
│  prompts/                                                        │
│    ├── horizontal-article-system-prompt.txt                     │
│    └── horizontal-article-user-prompt.txt                       │
│  templates/                                                      │
│    └── horizontal-article-template.hbs                          │
└─────────────────────────────────────────────────────────────────┘
```

## Usage

### From Repository Root

```bash
# Generate all horizontal articles
./start-horizontal.sh

# Test with a single article
./start-horizontal.sh --single
```

### From docs-generation Directory

```bash
pwsh ./Generate-HorizontalArticles.ps1
```

### Command Line Options

| Flag | Description |
|------|-------------|
| (none) | Generate ALL horizontal articles (default) |
| `--single` | Generate only the first article (for testing) |
| `--transform` | Enable text transformation features |

## Prerequisites

### Environment Variables

Set these in `./docs-generation/.env` or as environment variables:

```env
FOUNDRY_API_KEY=your-api-key
FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com/
FOUNDRY_MODEL_NAME=gpt-4.1-mini
FOUNDRY_MODEL_API_VERSION=2025-01-01-preview
```

### CLI Output

Run `./start.sh` first to generate the required CLI output files:
- `./generated/cli/cli-output.json` - Tool definitions from MCP CLI
- `./generated/cli/cli-version.json` - MCP version string

## Output

### Generated Files

```
./generated/
├── horizontal-articles/           # Generated markdown articles
│   ├── horizontal-article-acr.md
│   ├── horizontal-article-storage.md
│   └── ...
├── horizontal-article-prompts/    # Saved prompts for debugging
│   ├── horizontal-article-acr-prompt.md
│   └── ...
└── logs/
    └── horizontal-articles-*.log  # Generation logs
```

## Token Limit Scaling

The generator dynamically calculates AI token limits based on tool count to optimize response quality:

| Tools | Token Limit | Example Services |
|-------|-------------|------------------|
| 1-2 | 2,500 (min) | Simple services |
| 5 | 4,000 | acr, appconfig |
| 10 | 6,000 | keyvault, sql |
| 15 | 8,000 | storage, monitor |
| 19 | 9,600 | foundry |
| 25+ | 12,000 (max) | Large services |

**Formula**: `2000 + (toolCount × 400)` tokens, clamped between 2,500 and 12,000.

This prevents:
- **Truncated responses** for services with many tools
- **Unnecessarily verbose output** for simple services

## Generation Process

### Phase 1: Extract Static Data
- Reads tool definitions from `cli-output.json`
- Groups tools by service area (first word of command)
- Applies brand name mappings from `transformation-config.json`

### Phase 2: Generate AI Content
- Loads system and user prompts from `./prompts/`
- Injects static data into user prompt template (Handlebars)
- Calls Azure OpenAI with scaled token limit
- Saves full prompt + response to `./generated/horizontal-article-prompts/`

### Phase 3: Merge and Render
- Parses JSON response from AI
- Merges static data with AI-generated content
- Renders final article using Handlebars template

## Error Handling

### Truncated JSON Responses
If AI responses are cut off (JSON parse errors), the generator:
1. Logs the raw response to `error-{service}-airesponse.txt`
2. Continues processing remaining services
3. Shows error count in final summary

### Common Issues

| Error | Cause | Solution |
|-------|-------|----------|
| `Expected end of string` | Token limit too low | Increase base token multiplier |
| `FOUNDRY_API_KEY not set` | Missing env vars | Check `.env` file |
| `CLI output not found` | Missing prerequisites | Run `./start.sh` first |

## Customization

### Modifying Prompts

Edit files in `./prompts/`:
- `horizontal-article-system-prompt.txt` - AI behavior and output format
- `horizontal-article-user-prompt.txt` - Service-specific instructions (Handlebars template)

### Modifying Template

Edit `./templates/horizontal-article-template.hbs` to change article structure.

### Adjusting Token Scaling

In `HorizontalArticleGenerator.cs`, modify:

```csharp
var calculatedTokens = 2000 + (toolCount * 400);  // Adjust multiplier
var maxTokens = Math.Clamp(calculatedTokens, 2500, 12000);  // Adjust bounds
```

## Dependencies

- **.NET 9.0** - Runtime
- **GenerativeAI** (shared project) - Azure OpenAI client wrapper
- **Handlebars.Net** - Template rendering
- **Shared** (shared project) - Configuration utilities

## Related Files

- `./start-horizontal.sh` - Bash entry point
- `./Generate-HorizontalArticles.ps1` - PowerShell orchestration
- `./transformation-config.json` - Service brand name mappings
