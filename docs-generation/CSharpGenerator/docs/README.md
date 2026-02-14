# CSharpGenerator - Azure MCP Documentation Generator

.NET 9.0 console application that generates comprehensive documentation for Azure MCP (Model Context Protocol) tools.

## Overview

CSharpGenerator processes CLI output from the Azure MCP server and generates structured markdown documentation using Handlebars templates. It handles annotations, parameters, tool pages, and various output formats.

## Key Components

### Core Files
- **Program.cs** - Entry point and command-line argument parsing
- **DocumentationGenerator.cs** - Main generation orchestration logic
- **HandlebarsTemplateEngine.cs** - Template processing engine
- **Config.cs** - Configuration file loader

### Generators (`Generators/`)
- **AnnotationGenerator.cs** - Tool annotation files
- **ParameterGenerator.cs** - Tool parameter files
- **ParamAnnotationGenerator.cs** - Combined parameter + annotation files
- **PageGenerator.cs** - Area and index pages
- **ToolFamilyPageGenerator.cs** - Tool family documentation pages
- **CompleteToolGenerator.cs** - Complete tool documentation
- **ReportGenerator.cs** - Metadata and security reports

### Models (`Models/`)
- **CliOutput.cs** - CLI output structure
- **Tool.cs** - Tool metadata
- **CommonParameter.cs** - Common parameter definitions
- **BrandMapping.cs** - Brand name mappings

## Parameter Table Filtering

### Common Parameters Configuration

Common parameters are defined in `common-parameters.json` in the `docs-generation/` directory (parent of CSharpGenerator). These parameters are shared across all Azure MCP tools and are automatically filtered out of parameter tables in documentation.

**Common Parameters List:**
```json
[
  "--tenant",
  "--subscription", 
  "--auth-method",
  "--resource-group",
  "--retry-delay",
  "--retry-max-delay",
  "--retry-max-retries",
  "--retry-mode",
  "--retry-network-timeout"
]
```

### Parameter Table Rules

**What appears in parameter tables:**
- All tool-specific parameters (parameters NOT in the common parameters list)
- Common parameters that are **required** for a specific tool

**Filtering Logic:**
1. Load common parameters from `common-parameters.json`
2. For each tool, examine its parameters
3. Filter out common parameters UNLESS `required: true` for that tool
4. Display only the filtered parameters in the table

**Example:**

```csharp
// Tool has parameters: --tenant, --subscription, --resource-group (required), --sku, --region
// Common parameters: --tenant, --subscription, --resource-group (all 9 from JSON)

// Parameter table will show:
// - --resource-group (because required: true for this tool)
// - --sku (tool-specific)
// - --region (tool-specific)

// Filtered out:
// - --tenant (common, not required)
// - --subscription (common, not required)
```

### Implementation

The filtering is implemented in `ParameterGenerator.cs`:

```csharp
// Load common parameters from JSON file
var commonParameters = await LoadCommonParametersFromFile();
var commonParameterNames = new HashSet<string>(commonParameters.Select(p => p.Name));

// Filter parameters: exclude common unless required
var transformedOptions = allOptions
    .Where(opt => !string.IsNullOrEmpty(opt.Name) && 
                  (!commonParameterNames.Contains(opt.Name) || opt.Required == true))
    .Select(opt => new { /* transform */ })
    .ToList();
```

## Configuration Files

### Required Configuration Files (in `docs-generation/data/` directory)

All configuration files have been moved to the `data/` subdirectory:

1. **data/brand-to-server-mapping.json** - Maps brand names to server names and file names
2. **data/compound-words.json** - Word transformations for filename generation
3. **data/stop-words.json** - Words removed from include filenames
4. **data/nl-parameters.json** - Natural language parameter name mappings
5. **data/static-text-replacement.json** - Text replacements for descriptions
6. **data/config.json** - Main configuration file
7. **data/common-parameters.json** - Common parameters definition

## Command-Line Usage

```bash
dotnet run --project CSharpGenerator -- generate-docs <cli-output-json> <output-dir> [options]
```

**Options:**
- `--tool-pages` - Generate tool family pages
- `--index` - Generate index page
- `--common` - Generate common tools page
- `--commands` - Generate commands page
- `--annotations` - Generate annotation files
- `--example-prompts` - Generate example prompts (requires Azure OpenAI)
- `--complete-tools` - Generate complete tool documentation files
- `--validate-prompts` - Validate example prompts with LLM
- `--no-service-options` - Skip service options page generation
- `--version <version>` - Specify CLI version string

## Build and Run

### Build

```bash
cd docs-generation/CSharpGenerator
dotnet build --configuration Release
```

### Run

```bash
# From docs-generation directory
dotnet run --project CSharpGenerator --configuration Release -- generate-docs \
  ../generated/cli/cli-output.json \
  ../generated \
  --annotations \
  --version "2.0.0-beta.17"
```

## Output Structure

```
generated/
├── annotations/              # Tool annotation includes
├── parameters/              # Tool parameter includes (filtered)
├── param-and-annotation/    # Combined includes
├── tools/                   # Complete tool files
├── example-prompts/         # Example prompt files
├── tool-family/            # Tool family pages
├── common-general/         # Common documentation
└── logs/                   # Generation logs
```

## Dependencies

- **.NET 9.0 SDK**
- **Handlebars.Net 2.1.6** - Template engine
- **NaturalLanguageGenerator** - Text processing utilities
- **Shared** - Common utilities
- **GenerativeAI** - Azure OpenAI integration (optional)

## Development Notes

### Adding New Generators

1. Create generator class in `Generators/` directory
2. Implement generation logic with proper filtering
3. Use dependency injection for shared functions (brand mapping, filename cleaning)
4. Follow existing patterns for common parameter filtering
5. Document in dedicated README.md within the generator directory

### Modifying Parameter Filtering

To change which parameters are considered "common":
1. Edit `common-parameters.json` in the `docs-generation/` directory
2. Add or remove parameter definitions
3. Rebuild the project
4. Regenerate documentation

**Note**: The filtering logic automatically respects the `required` flag - required common parameters are always included in tables.

## Testing

```bash
# Test with a single tool
./start-only.sh pricing 1

# Test full generation
./start-only.sh advisor 1,2,3,4
```

## Related Documentation

- **[Main README](../README.md)** - docs-generation overview
- **[Complete Tools Generator](Generators/COMPLETE-TOOLS-README.md)** - Complete tool documentation feature
- **[System Overview](../../docs/SYSTEM-OVERVIEW.md)** - Architecture documentation
- **[Copilot Instructions](../../.github/copilot-instructions.md)** - Development guidelines
