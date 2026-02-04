# ComposedToolGenerator

## Overview

ComposedToolGenerator takes raw tool documentation files (with placeholders) and replaces them with actual content from annotations, parameters, and example prompts files. This forms the second stage of the separated tool generation process.

## Purpose

This package reads raw tool files created by RawToolGenerator and:
- Replaces `{{EXAMPLE_PROMPTS_CONTENT}}` with content from example prompts files
- Replaces `{{PARAMETERS_CONTENT}}` with content from parameter files
- Replaces `{{ANNOTATIONS_CONTENT}}` with content from annotation files
- Produces complete, composed tool documentation ready for review or AI improvement

## Input Requirements

This generator assumes the following files exist:
1. **Raw tool files** in `./generated/tools-raw/` (from RawToolGenerator)
2. **Annotation files** in `./generated/multi-page/annotations/`
3. **Parameter files** in `./generated/multi-page/parameters/`
4. **Example prompt files** in `./generated/multi-page/example-prompts/`

## Output

Files are generated in the `./generated/tools-composed/` directory with complete content (no placeholders).

## Usage

### Command Line

```bash
dotnet run --project ComposedToolGenerator \
  <raw-tools-dir> \
  <output-dir> \
  <annotations-dir> \
  <parameters-dir> \
  <example-prompts-dir>
```

### Arguments

- `raw-tools-dir` - Directory containing raw tool files with placeholders
- `output-dir` - Output directory for composed tool files (typically `./generated/tools-composed`)
- `annotations-dir` - Directory containing annotation files (typically `./generated/multi-page/annotations`)
- `parameters-dir` - Directory containing parameter files (typically `./generated/multi-page/parameters`)
- `example-prompts-dir` - Directory containing example prompt files (typically `./generated/multi-page/example-prompts`)

### Example

```bash
cd docs-generation
dotnet run --project ComposedToolGenerator \
  ../generated/tools-raw \
  ../generated/tools-composed \
  ../generated/multi-page/annotations \
  ../generated/multi-page/parameters \
  ../generated/multi-page/example-prompts
```

## File Matching

The generator uses intelligent file matching to find content files:

1. First tries: `{base-filename}-{content-type}.md`
2. Falls back to: `{base-filename}.md`

Example: For raw file `azure-storage-account-get.md`:
- Looks for: `azure-storage-account-get-annotations.md`
- Falls back to: `azure-storage-account-get.md`

## Frontmatter Handling

Content files often include frontmatter (metadata between `---` markers). The generator automatically strips frontmatter before embedding content to avoid duplication.

## Missing Content Handling

If a content file is not found:
- A comment is inserted: `<!-- Content not found: {type} -->`
- The missing file is tracked and reported at the end
- Processing continues for other files

## Output Format

Composed tool files maintain the structure from raw files but with actual content:

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

- "Connect all agents available in the current environment"
- "Establish connections to every agent in my Azure Foundry setup"
- "Connect to the agent named 'AnalyticsProcessor' right now"

| Parameter | Required or optional | Description |
|-----------|---------------------|-------------|
| **Agent ID** | Required | The ID of the agent to interact with. |
| **Query** | Required | The query sent to the agent. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

[!INCLUDE [foundry agents connect](../includes/tools/annotations/azure-ai-foundry-agents-connect-annotations.md)]
```

## Dependencies

- **Shared** - Common utilities and models

## Design Notes

- Strips frontmatter from embedded content to avoid duplication
- Handles missing content files gracefully
- Reports statistics on missing content at the end
- Maintains original frontmatter from raw files
- Independent from existing documentation generation

## Next Steps

After ComposedToolGenerator creates the composed files:
1. Review composed files for completeness
2. Use **ImprovedToolGenerator** to apply AI-based improvements for Microsoft content guidelines

## Integration Plan

To integrate into the main documentation generation pipeline:
1. Run RawToolGenerator after CLI extraction
2. Run existing generators (annotations, parameters, example prompts)
3. Run ComposedToolGenerator to create complete files (this package)
4. Optionally run ImprovedToolGenerator for AI enhancements
