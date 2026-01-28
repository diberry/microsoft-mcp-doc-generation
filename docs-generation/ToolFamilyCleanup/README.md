# Tool Family Cleanup

Independent .NET package for cleaning up Azure MCP tool family documentation files using LLM-based processing with Microsoft style guide standards.

## Overview

This tool takes generated tool family markdown files and applies AI-powered cleanup to ensure they meet Microsoft style guide standards and Azure MCP-specific documentation conventions.

### Features

- **Independent Operation**: Works independently of other documentation generation processes
- **Configurable Paths**: All input/output directories are configurable with sensible defaults
- **Prompt Preservation**: Saves individual prompts for each file for review and iteration
- **LLM-Powered**: Uses Azure OpenAI to apply style guide standards
- **Markdown-Only Output**: Validates and extracts only markdown output from LLM responses

## Quick Start

### Basic Usage

```bash
cd docs-generation
pwsh ./Generate-ToolFamilyCleanup.ps1
```

### With Custom Paths

```bash
pwsh ./Generate-ToolFamilyCleanup.ps1 -InputDir "./custom/input" -OutputDir "./custom/output"
```

### Command-Line Options

- `-i, --input-dir <path>` - Input directory with tool family markdown files (default: `../generated/tool-family`)
- `-p, --prompts-dir <path>` - Directory for generated prompts (default: `../generated/tool-family-cleanup-prompts`)
- `-o, --output-dir <path>` - Directory for cleaned markdown files (default: `../generated/tool-family-cleaned`)
- `-h, --help` - Display help message

## Configuration

### Environment Variables

Required via `.env` file or environment:

```bash
FOUNDRY_API_KEY=your-api-key-here
FOUNDRY_ENDPOINT=https://your-resource.openai.azure.com/
FOUNDRY_MODEL_NAME=gpt-4o
FOUNDRY_MODEL_API_VERSION=2025-01-01-preview
```

Create a `.env` file in `docs-generation/` directory with these values.

### Default Paths

- **Input**: `../generated/tool-family` - Tool family markdown files
- **Prompts Output**: `../generated/tool-family-cleanup-prompts` - Individual prompts
- **Cleanup Output**: `../generated/tool-family-cleaned` - Cleaned markdown files

## Customizing Prompts

### System Prompt

**File**: `ToolFamilyCleanup/prompts/tool-family-cleanup-system-prompt.txt`

Add Azure MCP-specific requirements in **Section 4: "Azure MCP-Specific Standards"**:

```text
4. **Azure MCP-Specific Standards**:
   - Ensure tool descriptions clearly explain what each tool does
   - Verify that prerequisites are clearly stated
   - Confirm authentication requirements are mentioned
   
   <!-- ADD YOUR REQUIREMENTS HERE -->
   - Tool names must use lowercase with hyphens
   - Examples must be in natural language (no CLI syntax)
   - RBAC roles must be documented for each operation
```

### User Prompt Template

**File**: `ToolFamilyCleanup/prompts/tool-family-cleanup-user-prompt.txt`

Available placeholders:
- `{{FILENAME}}` - Name of the file being processed
- `{{CONTENT}}` - Content of the file being processed

### Example Customizations

**Enforce Tool Naming Conventions**:
```text
   - Tool names must use lowercase with hyphens (e.g., 'storage account create')
   - Tool family headings must follow pattern: "Azure {Service} Tools"
   - Tool subheadings must match exact tool command names
```

**Require Consistent Example Format**:
```text
   - All example prompts must be in natural language (no CLI syntax)
   - Examples must start with action verbs ("Create", "List", "Delete")
   - Each example must show expected outcome
```

**Enforce Authentication Documentation**:
```text
   - Every tool family file must have an "Authentication" section
   - Authentication sections must list required Azure RBAC roles
   - Prerequisites must be clearly stated before examples
```

## Output Structure

```
generated/
├── tool-family/                         # Original files (input)
├── tool-family-cleanup-prompts/         # Saved prompts for each file
│   └── {filename}-prompt.txt
└── tool-family-cleaned/                 # Cleaned files (output)
    └── *.md
```

## LLM Model Recommendation

**Recommended**: GPT-4o

**Why GPT-4o**:
- **Document Editing**: Excels at stylistic refinement and technical writing
- **Microsoft Standards**: Superior understanding of style guide conventions
- **Cost Efficient**: ~$5/1M input tokens, ~$15/1M output tokens
- **Quality**: Better output than mini models, more cost-effective than Turbo
- **Performance**: For 46 files, ~$2.30 per full run

**Alternatives**:
- **GPT-4.1-mini**: Budget-constrained prototyping
- **GPT-4 Turbo**: Maximum quality, 2x cost

## Architecture

```
ToolFamilyCleanup/
├── Program.cs                          # CLI entry point
├── Services/
│   ├── CleanupConfiguration.cs        # Configuration
│   └── CleanupGenerator.cs            # Core logic
├── prompts/                            # Prompt files
│   ├── tool-family-cleanup-system-prompt.txt
│   └── tool-family-cleanup-user-prompt.txt
└── ToolFamilyCleanup.csproj           # Project file
```

## Workflow Integration

```
Step 1: Generate Base Documentation
  → pwsh ./Generate-MultiPageDocs.ps1
  → Output: ../generated/tool-family/*.md

Step 2: Run Tool Family Cleanup
  → pwsh ./Generate-ToolFamilyCleanup.ps1
  → Output: ../generated/tool-family-cleaned/*.md
  → Prompts: ../generated/tool-family-cleanup-prompts/*.txt

Step 3: Review and Integrate
  → Compare original vs cleaned files
  → Manually merge approved changes
  → Update prompts as needed
```

## Building

```bash
cd docs-generation/ToolFamilyCleanup
dotnet build
```

## Token Calculation and Limits

### Model Output Token Limit

**GPT-4o maximum output tokens: 16,384**

All token calculations are capped at this limit. Files requiring more than 16,384 tokens will be truncated.

### How Output Tokens Are Calculated

The tool uses dynamic token calculation to ensure complete file generation:

**Method 1: Tool Count (Preferred)**
```
maxTokens = min(16384, (toolCount × 1000) + 2000)
```
- Extracts tool count from file metadata: `**Tool Count:** 19`
- Allocates ~1000 tokens per tool (covers description, params, examples, annotations)
- Adds 2000 base tokens for frontmatter, H1, intro, Related content
- **Caps at 16,384 tokens** (gpt-4o model limit)
- Example: 19 tools → (19 × 1000) + 2000 = 21,000 → **capped to 16,384 tokens**

**Method 2: Word Count (Fallback)**
```
maxTokens = min(16384, (wordCount / 0.75) × 2.0)
```
- Used when tool count not found in metadata
- Estimates: 1 word ≈ 1.33 tokens
- Multiplies by 2x for cleanup formatting buffer
- **Caps at 16,384 tokens**
- Example: 3,973 words → (3,973 / 0.75) × 2 = 10,597 tokens → rounds to MIN (12,000)

**Minimum**: 12,000 tokens (ensures small files get adequate output space)

### Token Budget Breakdown

**Example: foundry.md (19 tools, 3,973 words)**

**INPUT Tokens:**
- System prompt: ~1,340 tokens
- User prompt template: ~250 tokens
- File content: ~5,300 tokens (3,973 words × 1.33)
- **Total INPUT: ~6,890 tokens**

**OUTPUT Tokens:**
- Calculated: 21,000 tokens (19 tools × 1000 + 2000)

**Total Context Usage:**
- ~27,890 tokens (< 128K gpt-4o context window) ✅

### Incomplete File Generation

**Symptoms:**
- Tool count in output doesn't match input (e.g., 7 tools → 2 tools)
- File ends abruptly mid-tool or mid-section
- "Related content" section missing
- Console shows: `⚠ Capped at model limit: 21000 → 16384 tokens`

**Cause:**
LLM hit the model's maximum output token limit (16,384 for gpt-4o) or the calculated `maxTokens` was insufficient.

**Detection:**
The tool shows warnings when capping occurs:
```
[19/46] Token calculation: tool-count method (19 tools × 1000 + 2000)
[19/46] ⚠ Capped at model limit: 21000 → 16384 tokens
[19/46] Content: 3973 words, Max tokens: 16384
```

For explicit truncation errors:
```
[19/46] ✗ TRUNCATED: foundry.md
        LLM response was truncated due to token limit.
        Used tokens: 27890, Max output tokens: 16384.
```

**Solutions:**

1. **Files with 15+ tools will always hit the 16K limit**
   - foundry.md (19 tools): 21,000 needed → capped to 16,384
   - eventhubs.md (15 tools): 17,000 needed → capped to 16,384
   - **These files require manual processing or splitting**

2. **Split large files by tool family**:
   ```bash
   # Process foundry agents separately from foundry models
   # Create: foundry-agents.md (9 tools)
   # Create: foundry-models.md (10 tools)
   ```

3. **Reduce tokens per tool** (if tools are simpler than expected):
   - Edit `CleanupGenerator.cs` line ~218
   - Change `toolCount * 1000` to `toolCount * 800`
   - This helps files with 16-20 tools fit under 16,384 limit

4. **Use gpt-4-turbo** (32K output tokens):
   - Update `.env`: `FOUNDRY_MODEL_NAME=gpt-4-turbo`
   - **Cost**: 2x more expensive than gpt-4o
   - Supports up to 32 tools per file

5. **Process incrementally** (most reliable for large files):
   - Manually clean tools 1-10, save
   - Clean tools 11-19, append
   - Combine outputs manually

## Troubleshooting

### "Input directory not found"
- Ensure base documentation was generated first
- Check that the input path is correct
- Use `--input-dir` to specify custom path

### "Missing required environment variables"
- Create `.env` file in `docs-generation/` directory
- Set Azure OpenAI credentials
- See Configuration section above

### "LLM response was truncated due to token limit"
- See **Token Calculation and Limits** section above
- Check console output for calculated maxTokens
- Verify tool count is being detected from metadata
- Consider increasing MIN_MAX_TOKENS or tokens-per-tool multiplier

### "Invalid markdown output"
- Check error log in output directory
- Review the prompt in prompts directory
- Adjust system prompt for better guidance
- Some files may need manual review

## Notes

- Processes only root-level files (not subdirectories like annotations/, parameters/)
- Targets tool family files (acr.md, storage.md, etc.)
- Each file processed independently
- LLM token limit: 16,000 for large files
- Failed files logged but don't stop overall process

## Format Validation

**Known Limitation**: Generated files do not yet match published Microsoft Learn format exactly. Key differences:

- ✗ Missing service overview section
- ✗ Wrong frontmatter format (uses `include` instead of `concept-article`)
- ✗ Includes Quick Navigation section (not in published docs)
- ✗ Incorrect heading levels and structure
- ✗ Missing Related content section at end
- ✗ Example prompts lack scenario descriptors

**Recommendation**: Either modify the base tool family generation to produce correct format initially, or update the cleanup prompts to transform the structure completely.

See `published-format-analysis.md` for detailed comparison with published docs.

## Future Enhancements

- Parallel processing for faster execution
- Diff generation to show changes
- Automated validation against style guide rules
- Format transformation to match Microsoft Learn standards
- Integration tests with sample files
