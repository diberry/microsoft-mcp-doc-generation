# RawToolGenerator

## Overview

RawToolGenerator creates raw tool documentation files with placeholders from MCP CLI output. These files form the first stage of the separated tool generation process.

## Purpose

This package generates raw tool documentation files that contain:
- Frontmatter (metadata)
- Tool name and description
- Command information
- **Placeholders** for content that will be filled in later:
  - `{{EXAMPLE_PROMPTS_CONTENT}}` - For AI-generated example prompts
  - `{{PARAMETERS_CONTENT}}` - For parameter tables
  - `{{ANNOTATIONS_CONTENT}}` - For tool annotation hints

## Output

Files are generated in the `./generated/tools-raw/` directory with the format:
```
azure-<service>-<operation>.md
```

## Usage

### Command Line

```bash
dotnet run --project RawToolGenerator <cli-output-json> <output-dir> [mcp-cli-version]
```

### Arguments

- `cli-output-json` - Path to the MCP CLI output JSON file (typically `./generated/cli/cli-output.json`)
- `output-dir` - Output directory for raw tool files (typically `./generated/tools-raw`)
- `mcp-cli-version` - (Optional) MCP CLI version string for metadata

### Example

```bash
cd docs-generation
dotnet run --project RawToolGenerator \
  ../generated/cli/cli-output.json \
  ../generated/tools-raw \
  "2.0.0-beta.13"
```

## File Format

Raw tool files follow this structure:

```markdown
---
ms.topic: reference
ms.date: 2026-01-24 00:06:16 UTC
mcp-cli.version: 2.0.0-beta.13
generated: 2026-01-24 00:06:16 UTC
---

# foundry agents connect

<!-- @mcpcli foundry agents connect -->

Query a Microsoft Foundry agent and get the response as is...

Example prompts include:

{{EXAMPLE_PROMPTS_CONTENT}}

{{PARAMETERS_CONTENT}}

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

{{ANNOTATIONS_CONTENT}}

```

## Dependencies

- **Shared** - Common utilities and models
- **NaturalLanguageGenerator** - Text cleanup and filename sanitization
- **Shared** - Common utilities (Handlebars.Net dependency removed — not used by this project)

## Design Notes

- Uses brand-to-server-mapping.json for consistent filename generation
- Applies filename cleaning to ensure valid markdown filenames
- Generates one file per tool from CLI output
- Completely independent from existing documentation generation
- Does not generate actual content - only structure with placeholders

## Next Steps

After RawToolGenerator creates the raw files:
1. Generate example prompts, parameters, and annotations using existing generators
2. Use **ComposedToolGenerator** to replace placeholders with actual content
3. Use **ImprovedToolGenerator** to apply AI-based improvements

## Integration Plan

To integrate into the main documentation generation pipeline:
1. Run RawToolGenerator after CLI extraction
2. Run existing generators (annotations, parameters, example prompts)
3. Run ComposedToolGenerator to create complete files
4. Optionally run ImprovedToolGenerator for AI enhancements
