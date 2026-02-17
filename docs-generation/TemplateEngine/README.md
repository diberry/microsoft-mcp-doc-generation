# TemplateEngine

Shared Handlebars template rendering library used by multiple documentation generator projects. Wraps [Handlebars.Net](https://github.com/Handlebars-Net/Handlebars.Net) with pre-registered custom helpers.

## Usage

```csharp
using TemplateEngine;

// Render a template file with data
var result = await HandlebarsTemplateEngine.ProcessTemplateAsync("template.hbs", data);

// Render a template string
var result = HandlebarsTemplateEngine.ProcessTemplateString("Hello {{name}}", data);
```

## Helpers

### Core helpers (`CoreHelpers.cs`)

Generic helpers usable by any project:

| Helper | Signature | Description |
|---|---|---|
| `formatDate` | `{{formatDate date}}` | Format DateTime → `yyyy-MM-dd HH:mm:ss UTC` |
| `formatDateShort` | `{{formatDateShort date}}` | Format DateTime → `MM/dd/yyyy` |
| `kebabCase` | `{{kebabCase text}}` | Convert to kebab-case |
| `slugify` | `{{slugify text}}` | URL-safe slug for anchors |
| `concat` | `{{concat a b c}}` | Concatenate strings |
| `eq` | `{{#if (eq a b)}}` | Case-insensitive equality |
| `replace` | `{{replace str old new}}` | String replacement |
| `add` | `{{add a b}}` | Addition |
| `divide` | `{{divide a b}}` | Division |
| `round` | `{{round num precision}}` | Round to precision |
| `requiredIcon` | `{{requiredIcon bool}}` | Boolean → ✅/❌ |

### MCP helpers (`McpHelpers.cs`)

Azure MCP command structure helpers:

| Helper | Signature | Description |
|---|---|---|
| `getAreaCount` | `{{getAreaCount dict}}` | Count entries in a dictionary |
| `subToolFamily` | `{{subToolFamily command}}` | Parse sub-tool (e.g., "Blob" from `azmcp storage blob list`) |
| `hasSubTool` | `{{#if (hasSubTool command)}}` | Check for sub-tool structure (4+ parts) |
| `operationName` | `{{operationName command}}` | Operation for flat commands |
| `subOperation` | `{{subOperation command}}` | Operation parts after sub-tool |
| `getSimpleToolName` | `{{getSimpleToolName command}}` | Clean tool name for headings |
| `formatNaturalLanguage` | `{{formatNaturalLanguage param}}` | CLI param → human text with acronym handling |
| `groupBy` | `{{groupBy collection property}}` | Group collection by property (reflection-based) |

## Projects using TemplateEngine

- **CSharpGenerator** — annotation and parameter documentation
- **HorizontalArticleGenerator** — horizontal how-to articles
- **ExamplePromptGeneratorStandalone** — AI-generated example prompts
