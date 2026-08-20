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
│                    DocGeneration.Steps.HorizontalArticles                    │
├─────────────────────────────────────────────────────────────────┤
│  HorizontalArticleProgram.cs    - Entry point, CLI args         │
│  Generators/                                                     │
│    └── DocGeneration.Steps.HorizontalArticles.cs - Core generation logic    │
│  Models/                                                         │
│    ├── StaticArticleData.cs     - Data from CLI output          │
│    ├── AIGeneratedArticleData.cs - AI response model            │
│    └── HorizontalArticleTemplateData.cs - Combined template data│
│  prompts/                                                        │
│    ├── horizontal-article-tool-{system,user}-prompt.txt          │
│    ├── horizontal-article-namespace-overview-{system,user}-*.txt │
│    ├── horizontal-article-namespace-access-{system,user}-*.txt   │
│    ├── horizontal-article-namespace-best-practices-*.txt         │
│    └── horizontal-article-namespace-links-{system,user}-*.txt    │
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

Set these in `./mcp-tools/.env` or as environment variables:

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
├── horizontal-article-prompts/    # Saved prompts, responses, and call failures
│   ├── horizontal-article-acr-tool-00-prompt.md
│   ├── horizontal-article-acr-namespace-overview-prompt.md
│   └── ...
└── logs/
    └── horizontal-articles-*.log  # Generation logs
```

## Token limits

The current per-tool + namespace-fragment generation path uses separate output-token budgets per
AI call type. Step 6 replaced the single broad namespace-summary call (which requested all seven
namespace-level fields in one response and was prone to truncation on gpt-5-mini) with four small,
focused namespace-fragment calls, each with a deliberately tiny budget:

| Call | Token limit |
|------|-------------|
| Per-tool content | 8,000 |
| Namespace fragment: overview (short description + overview) | 500 |
| Namespace fragment: access (prerequisites + required roles) | 1,500 |
| Namespace fragment: best practices | 1,500 |
| Namespace fragment: links (doc link + additional links) | 750 |

All Step 6 calls request low reasoning effort and JSON response format. This keeps the bounded
output budget focused on visible structured content; otherwise reasoning models can consume the
entire budget internally and return `finish_reason=length` with an empty visible response.

This prevents:
- **Truncated per-tool responses** when reasoning models consume output tokens internally
- **Truncated/oversized namespace responses** — each fragment asks for only 1–2 short JSON
  fields/arrays, never the full seven-field namespace payload that used to overflow a single
  response's output budget on reasoning models

The four fragment results are deterministically stitched back into the same
`NamespaceSummaryAIData` shape (`StitchNamespaceSummary`) that the rest of the pipeline
(`AggregateAIData`, template rendering) already expects, so no downstream data flow changed.

## Generation Process

### Phase 1: Extract Static Data
- Reads tool definitions from `cli-output.json`
- Groups tools by service area (first word of command)
- Applies brand name mappings from `transformation-config.json`

### Phase 2: Generate AI Content
- Loads system and user prompts from `./prompts/` — one per-tool prompt pair, plus one prompt
  pair per namespace fragment (overview/access/best-practices/links)
- Injects static data into user prompt template (Handlebars)
- Calls Azure OpenAI once per tool and once per namespace fragment (five calls total for a
  one-tool service), each with its own compact prompt pair and small output-token budget
- Saves full prompt + response to `./generated/horizontal-article-prompts/`
  (component filenames, e.g. `horizontal-article-{service}-namespace-overview-prompt.md`)
- Preserves partial response content and error details in that same component file when a call is
  truncated; if the endpoint returns no body, the file explicitly records that no response was
  received. Failure semantics and static/empty fallbacks remain unchanged.

### Phase 3: Merge and Render
- Parses JSON response from AI
- Merges static data with AI-generated content
- Renders final article using Handlebars template

## Error Handling

### Truncated JSON Responses
If an AI response is cut off by the output-token limit, the shared client throws a typed
`AiResponseTruncatedException` containing the partial body and token metadata. Step 6 appends that
partial body under `## AI Response (truncated)` and the failure details under `## AI Error` in the
same component prompt file. The generator then preserves its existing fallback/failure behavior.

### Common Issues

| Error | Cause | Solution |
|-------|-------|----------|
| `Expected end of string` | Token limit too low | Increase base token multiplier |
| `FOUNDRY_API_KEY not set` | Missing env vars | Check `.env` file |
| `CLI output not found` | Missing prerequisites | Run `./start.sh` first |

## Customization

### Modifying Prompts

Edit files in `./prompts/`:
- `horizontal-article-tool-{system,user}-prompt.txt` - per-tool AI call
- `horizontal-article-namespace-overview-{system,user}-prompt.txt` - service short description + overview
- `horizontal-article-namespace-access-{system,user}-prompt.txt` - prerequisites + required RBAC roles
- `horizontal-article-namespace-best-practices-{system,user}-prompt.txt` - best practices
- `horizontal-article-namespace-links-{system,user}-prompt.txt` - doc link + additional links

Each namespace-fragment prompt pair is intentionally small, compact, and service-agnostic — do not
add service-specific logic/examples concentrated on one service. The legacy
`horizontal-article-system-prompt.txt` (~33 KB) and `horizontal-article-namespace-user-prompt.txt`
are still present for the `[Obsolete]` single-call fallback path but are no longer used by the
per-tool + namespace-fragment path described above.

### Modifying Template

Edit `./DocGeneration.Steps.HorizontalArticles/templates/horizontal-article-template.hbs` to change article structure.

### Adjusting token limits

Per-tool budget, in `Generators/HorizontalArticleGenerator.cs`:

```csharp
if (isPerToolCall) return 8000;
```

Namespace-fragment budgets (`CalculateMaxTokens(NamespaceFragment)`):

```csharp
internal static int CalculateMaxTokens(NamespaceFragment fragment) => fragment switch
{
    NamespaceFragment.Overview => 500,
    NamespaceFragment.Access => 1500,
    NamespaceFragment.BestPractices => 1500,
    NamespaceFragment.Links => 750,
    _ => throw new ArgumentOutOfRangeException(nameof(fragment), fragment, "Unknown namespace fragment.")
};
```

## Dependencies

- **.NET 9.0** - Runtime
- **DocGeneration.Core.GenerativeAI** (shared project) - Azure OpenAI client wrapper
- **DocGeneration.Core.TemplateEngine** (shared project) - Handlebars template rendering
- **DocGeneration.Core.Shared** (shared project) - Configuration utilities

## Related Files

- `./start-horizontal.sh` - Bash entry point
- `./Generate-HorizontalArticles.ps1` - PowerShell orchestration
- `./transformation-config.json` - Service brand name mappings
