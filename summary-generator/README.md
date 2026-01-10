# Azure MCP Documentation Summary Generator (JavaScript)

JavaScript port of the summary generation logic from `Generate-MultiPageDocs.ps1`.

## Purpose

This standalone Node.js tool reproduces the summary output section of the PowerShell documentation generator. It:

- Reads CLI output files (`cli-output.json`, `cli-namespace.json`)
- Scans generated documentation files
- Calculates tool statistics by service area
- Generates a comprehensive `generation-summary.md` file
- Displays color-coded console output

## Why JavaScript?

This is a **port** of the PowerShell summary logic for scenarios where:
- PowerShell is not available (some CI/CD systems)
- Cross-platform Node.js execution is preferred
- Integration with JavaScript-based tooling is needed
- You want to generate summaries independently from the main PowerShell flow

## Prerequisites

- Node.js 14.0.0 or higher
- Generated CLI output files in `../generated/cli/`
- Generated documentation files in `../generated/tools/`

## Installation

```bash
cd summary-generator
npm install
```

## Usage

### Basic Usage

From the `summary-generator` directory:

```bash
node generate-summary.js
```

Or using npm script:

```bash
npm run summary
```

### Custom Paths

Override default paths using environment variables:

```bash
CLI_OUTPUT_PATH="../generated/cli/cli-output.json" \
OUTPUT_DIR="../generated/tools" \
PARENT_OUTPUT_DIR="../generated" \
node generate-summary.js
```

### From Root Directory

```bash
node summary-generator/generate-summary.js
```

### Make Executable (Linux/macOS)

```bash
chmod +x generate-summary.js
./generate-summary.js
```

## Configuration

The tool uses these default paths (relative to the script location):

```javascript
{
    cliOutputPath: '../generated/cli/cli-output.json',
    outputDir: '../generated/tools',
    parentOutputDir: '../generated',
    summaryFileName: 'generation-summary.md'
}
```

Override any path with environment variables:
- `CLI_OUTPUT_PATH`
- `OUTPUT_DIR`
- `PARENT_OUTPUT_DIR`

## Output

### Console Output

Color-coded console output showing:
- Tool statistics (total tools, service areas)
- Generated documentation files with sizes
- Data files with sizes
- Complete tool list by service area

### Generated File

Creates `generation-summary.md` in the parent output directory with:
- Generation timestamp
- Complete file listing
- Tool statistics
- Tools by service area
- Complete tool list with parameters and descriptions

## Output Example

```
PROGRESS: Starting Azure MCP Documentation Generation Summary...
PROGRESS: Step 1: Loading CLI output data...
SUCCESS: Loaded CLI output with 181 tools

INFO: Tool Statistics:
INFO:   📊 Total tools: 181
INFO:   📊 Total service areas: 30
INFO:   📊 Tools by service area:
INFO:      • acr: 5 tools
INFO:      • aks: 8 tools
INFO:      • appconfig: 12 tools
...

SUCCESS: ========================================
SUCCESS: ✅ Summary generation complete!
SUCCESS: 📄 34 documentation pages
SUCCESS: 🔧 181 tools across 30 service areas
SUCCESS: ========================================
```

## Comparison with PowerShell Version

This JavaScript port reproduces the **Step 5: Summary** section from `Generate-MultiPageDocs.ps1` (lines 370-682).

### What's Included
✅ Tool statistics parsing  
✅ File listing with sizes  
✅ Tool counts by service area  
✅ Complete tool list generation  
✅ Summary markdown file creation  
✅ Color-coded console output  

### What's Different
❌ Not integrated with C# generator (standalone)  
❌ No ToolDescriptionEvaluator comparison (can be added)  
❌ Simplified parameter counting (reads from CLI output)  

## Integration with Main Workflow

This tool is **independent** from the main documentation generation workflow. It can be:

1. **Run standalone** after documentation generation
2. **Called from shell scripts** for post-processing
3. **Integrated into CI/CD** for summary reporting
4. **Used for debugging** to verify tool counts

## Development

### Project Structure

```
summary-generator/
├── generate-summary.js     # Main script
├── package.json           # Node.js configuration
└── README.md             # This file
```

### Adding Features

To add the ToolDescriptionEvaluator comparison (as in PowerShell version):

```javascript
// Read both files
const cliData = readJsonFile(config.cliOutputPath);
const toolDescData = readJsonFile('../generated/ToolDescriptionEvaluator.json');

// Compare counts
const cliToolCount = cliData.results.length;
const toolDescToolCount = toolDescData.results.length;

// Generate comparison report...
```

## Error Handling

The script validates:
- CLI output file exists and is valid JSON
- Generated output directory exists
- Files are readable

Exits with code 1 on errors with descriptive messages.

## Troubleshooting

### "File not found" Error

**Problem:** CLI output files don't exist

**Solution:**
```bash
# Generate CLI output first
cd ..
./run-mcp-cli-output.sh
# Then run summary
cd summary-generator
node generate-summary.js
```

### Empty Tool Count

**Problem:** CLI output has no results

**Solution:** Verify `cli-output.json` structure:
```bash
cat ../generated/cli/cli-output.json | jq '.results | length'
```

### Path Issues

**Problem:** Script can't find files

**Solution:** Run from correct directory or set env vars:
```bash
cd /workspaces/microsoft-mcp-doc-generation/summary-generator
node generate-summary.js
```

## License

Follows the same license as the parent project.

## Related Files

- **Source PowerShell:** `../docs-generation/Generate-MultiPageDocs.ps1` (lines 370-682)
- **CLI Output:** `../generated/cli/cli-output.json`
- **Generated Docs:** `../generated/tools/*.md`
- **Summary Output:** `../generated/generation-summary.md`
