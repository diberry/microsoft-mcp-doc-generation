# Verify Quantity

A verification tool to check the completeness of Azure MCP documentation generation.

## Purpose

This tool verifies that all expected documentation files have been generated for each Azure MCP tool by:
- Parsing `generated/cli/toollist.txt` to identify all tools
- Checking for corresponding files in each output directory
- Providing visual reports and detailed breakdowns of missing files

## Usage

```bash
# From the verify-quantity directory
npm run verify

# Or from the root
cd verify-quantity && node index.js
```

## Output

The tool produces:

1. **Console Visualization**: A colorful bar chart showing completion percentage for each file type
2. **Missing Tools Report**: A markdown file (`missing-tools.md` in root) with detailed breakdowns

## What It Checks

For each tool in `toollist.txt`, it verifies the existence of:

- ✅ **Annotations** (`generated/annotations/[tool]-annotations.md`)
- ✅ **Parameters** (`generated/parameters/[tool]-parameters.md`)
- ✅ **Example Prompts** (`generated/example-prompts/[tool]-example-prompts.md`)
- ✅ **Param+Annotation** (`generated/param-and-annotation/[tool]-param-and-annotation.md`)
- ✅ **Complete Tools** (`generated/tools/[tool].complete.md`)

## Filename Resolution

The tool uses the same filename resolution logic as the generator:

1. **Brand Mapping**: `brand-to-server-mapping.json` (highest priority)
2. **Compound Words**: `compound-words.json` for token transformations
3. **Fallback**: `azure-{service}` format

## Example Output

```
================================================================================
  AZURE MCP DOCUMENTATION GENERATION VERIFICATION REPORT
================================================================================

📊 Total Tools: 221

┌──────────────────────────────────────────────────────────────────────────────┐
│ 📝 Annotations       │ ████████████████████████████████████████ │    221/221  100.0% ✅ │
│ ⚙️  Parameters        │ ████████████████████████████████████████ │    221/221  100.0% ✅ │
│ 💡 Example Prompts   │ ██████████████████████████████░░░░░░░░░░ │    190/221   86.0% ⚠️  │
│ 📋 Param+Annotation  │ ████████████████████████████████████████ │    221/221  100.0% ✅ │
│ ✅ Complete Tools    │ ██████████████████████████████░░░░░░░░░░ │    190/221   86.0% ⚠️  │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Troubleshooting

If files are missing:

1. Check if the generation pass completed successfully
2. Verify `brand-to-server-mapping.json` contains the service
3. Check `compound-words.json` for multi-word token mappings
4. Review the detailed `missing-tools.md` report for specific tools

## Requirements

- Node.js 14+ (uses ES modules)
- Generated toollist.txt in `generated/cli/`
- Configuration files in `docs-generation/`
