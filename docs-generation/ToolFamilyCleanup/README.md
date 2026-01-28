# Tool Family Cleanup

Independent .NET package for cleaning up Azure MCP tool family documentation files using LLM-based processing with Microsoft style guide standards.

## Overview

This tool takes generated tool family markdown files and applies AI-powered cleanup to ensure they meet Microsoft style guide standards and Azure MCP-specific documentation conventions.

**Two Processing Modes:**

1. **Single-Phase Mode** (Default): Processes complete pre-assembled tool family files in one LLM call per file
2. **Multi-Phase Mode** (Advanced): Assembles tool families from individual tool files, generating metadata and related content separately to avoid token limits

### Features

- **Independent Operation**: Works independently of other documentation generation processes
- **Two Workflows**: Choose between single-phase (fast) or multi-phase (handles large families)
- **Configurable Paths**: All input/output directories are configurable with sensible defaults
- **Prompt Preservation**: Saves individual prompts for each file for review and iteration
- **LLM-Powered**: Uses Azure OpenAI to apply style guide standards
- **Markdown-Only Output**: Validates and extracts only markdown output from LLM responses
- **Token Limit Handling**: Multi-phase mode solves 16K token limit by processing tools individually

## Quick Start

### Single-Phase Mode (Default)

**Use When**: Processing pre-assembled tool family files (e.g., from `./generated/tool-family/`)

```bash
cd docs-generation
pwsh ./Generate-ToolFamilyCleanup.ps1
```

**How It Works:**
1. Reads complete tool family markdown files
2. Sends entire file to LLM for cleanup
3. Saves cleaned output
4. **Limitation**: Files with 15+ tools may hit 16K token limit

**Output Structure:**
```
generated/
├── tool-family-cleanup-prompts/    # Prompts sent to LLM
└── tool-family-cleaned/            # Cleaned markdown files
```

### Multi-Phase Mode (Advanced)

**Use When**: Processing tool families with 15+ tools that exceed token limits

```bash
cd docs-generation
pwsh ./GenerateToolFamilyCleanup-multifile.ps1
```

**How It Works:**
1. **Phase 1**: Reads individual tool files from `./generated/tools/` and groups by family
2. **Phase 2**: Generates metadata (frontmatter + H1 + intro) using AI per family
3. **Phase 3**: Generates "Related content" section using AI per family
4. **Phase 4**: Stitches tools together with AI-generated metadata/related content (no AI)

**Advantages:**
- ✓ No 16K token limit (processes tools individually)
- ✓ 95% cost reduction (~115K tokens vs. 2M tokens for large files)
- ✓ All tools included (no truncation)
- ✓ Intermediate files saved for debugging

**Output Structure:**
```
generated/
├── tool-family-metadata/      # AI-generated metadata per family
├── tool-family-related/       # AI-generated related content per family
└── tool-family-multifile/     # Final stitched files
```

## PowerShell Scripts

### Generate-ToolFamilyCleanup.ps1 (Single-Phase)

**Purpose**: Process complete tool family files with full cleanup

**Usage:**
```bash
pwsh ./Generate-ToolFamilyCleanup.ps1 [options]
```

**Options:**
- `-InputDir <path>` - Input directory with tool family markdown files (default: `../generated/tool-family`)
- `-PromptsDir <path>` - Directory for generated prompts (default: `../generated/tool-family-cleanup-prompts`)
- `-OutputDir <path>` - Directory for cleaned markdown files (default: `../generated/tool-family-cleaned`)
- `-Help` - Display help message

**Example - Custom Paths:**
```bash
pwsh ./Generate-ToolFamilyCleanup.ps1 -InputDir "./custom/input" -OutputDir "./custom/output"
```

### GenerateToolFamilyCleanup-multifile.ps1 (Multi-Phase)

**Purpose**: Assemble and clean tool families from individual tool files, avoiding token limits

**Usage:**
```bash
pwsh ./GenerateToolFamilyCleanup-multifile.ps1 [options]
```

**Options:**
- `-ToolsInputDir <path>` - Input directory with complete tool files (default: `../generated/tools`)
- `-MetadataOutputDir <path>` - Output directory for AI-generated metadata (default: `../generated/tool-family-metadata`)
- `-RelatedOutputDir <path>` - Output directory for AI-generated related content (default: `../generated/tool-family-related`)
- `-FinalOutputDir <path>` - Output directory for final stitched files (default: `../generated/tool-family-multifile`)
- `-Help` - Display help message

**Example - Custom Paths:**
```bash
pwsh ./GenerateToolFamilyCleanup-multifile.ps1 `
  -ToolsInputDir "./generated/tools" `
  -FinalOutputDir "./generated/final"
```

## Command-Line Options (Direct .NET Execution)

Run the tool directly for more control:

```bash
cd docs-generation/ToolFamilyCleanup
dotnet run --configuration Release [options]
```

**Options:**
- `-m, --multi-phase` - Enable multi-phase mode (tool-level assembly)
- `-i, --input-dir <path>` - Input directory (single-phase mode only)
- `-p, --prompts-dir <path>` - Directory to save prompts (single-phase mode only)
- `-o, --output-dir <path>` - Directory to save cleaned files (single-phase mode only)
- `-h, --help` - Display help message

**Examples:**
```bash
# Single-phase with defaults
dotnet run --configuration Release

# Multi-phase mode
dotnet run --configuration Release --multi-phase

# Single-phase with custom paths
dotnet run --configuration Release --input-dir "./custom/input"
```

## Choosing Between Modes

### Use Single-Phase Mode When:
- ✓ Tool families have <15 tools each
- ✓ Pre-assembled tool family files already exist
- ✓ Fast processing is priority
- ✓ Files are under 10KB each
- ✓ Testing or iterating on prompts
- ✓ Simple cleanup without assembly needed

**Example Use Cases:**
- Cleaning up manually created tool family files
- Quick style guide enforcement
- Prompt iteration and testing

### Use Multi-Phase Mode When:
- ✓ Tool families have 15+ tools (e.g., foundry with 19 tools)
- ✓ Files exceed 16K token limit in single-phase
- ✓ Starting from individual tool files
- ✓ Need to minimize AI costs (95% reduction)
- ✓ Want intermediate outputs for review
- ✓ Troubleshooting truncation issues

**Example Use Cases:**
- Processing large service families (foundry, storage, sql)
- Cost-sensitive batch processing
- Building tool families from scratch
- Debugging token limit errors

### Quick Decision Guide

| Scenario | Recommended Mode | Reason |
|----------|-----------------|---------|
| ACR family (6 tools, 3KB) | Single-phase | Small, fast |
| Storage family (25 tools, 15KB) | Multi-phase | Large, exceeds token limit |
| Quick cleanup test | Single-phase | Fast iteration |
| Full production run | Multi-phase | Cost-effective, reliable |
| First-time setup | Single-phase | Simpler workflow |
| Token limit errors | Multi-phase | Solves 16K limit |

## Configuration

### Environment Variables

Required via `.env` file or environment:

```bash
FOUNDRY_API_KEY=your-api-key-here
FOUNDRY_ENDPOINT=https://your-resource.openai.azure.com/
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME=gpt-4o
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION=2025-01-01-preview
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

### Single-Phase Mode Output

```
generated/
├── tool-family/                         # Original files (input)
├── tool-family-cleanup-prompts/         # Saved prompts for each file
│   └── {filename}-prompt.txt
└── tool-family-cleaned/                 # Cleaned files (output)
    └── *.md
```

### Multi-Phase Mode Output

```
generated/
├── tools/                               # Individual tool files (input)
│   ├── mcp-azure-mcp-acr-list-registries.complete.md
│   └── ...
├── tool-family-metadata/                # AI-generated metadata
│   ├── acr-metadata.md
│   ├── storage-metadata.md
│   └── ...
├── tool-family-related/                 # AI-generated related content
│   ├── acr-related.md
│   ├── storage-related.md
│   └── ...
└── tool-family-multifile/               # Final stitched files
    ├── acr.md
    ├── storage.md
    └── ...
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
├── Program.cs                          # CLI entry point, mode selection
├── Services/
│   ├── CleanupConfiguration.cs        # Configuration
│   ├── CleanupGenerator.cs            # Core logic (both modes)
│   ├── FamilyMetadataGenerator.cs     # Multi-phase: metadata generation
│   ├── RelatedContentGenerator.cs     # Multi-phase: related content
│   ├── FamilyFileStitcher.cs          # Multi-phase: file assembly
│   └── ToolReader.cs                  # Multi-phase: tool file reader
├── Models/
│   ├── FamilyContent.cs               # Multi-phase: family data model
│   └── ToolContent.cs                 # Multi-phase: tool data model
├── prompts/                            # Prompt files
│   ├── tool-family-cleanup-system-prompt.txt      # Single-phase
│   ├── tool-family-cleanup-user-prompt.txt        # Single-phase
│   ├── family-metadata-system-prompt.txt          # Multi-phase
│   ├── family-metadata-user-prompt.txt            # Multi-phase
│   ├── related-content-system-prompt.txt          # Multi-phase
│   └── related-content-user-prompt.txt            # Multi-phase
└── ToolFamilyCleanup.csproj           # Project file
```

## Workflow Integration

### Single-Phase Workflow

**Use Case**: Cleaning pre-assembled tool family files with <15 tools per family

```
Step 1: Generate Base Documentation
  → pwsh ./Generate-MultiPageDocs.ps1
  → Output: ../generated/tool-family/*.md (30+ files)

Step 2: Run Single-Phase Cleanup
  → pwsh ./Generate-ToolFamilyCleanup.ps1
  → Output: ../generated/tool-family-cleaned/*.md
  → Prompts: ../generated/tool-family-cleanup-prompts/*.txt

Step 3: Review and Integrate
  → Compare original vs cleaned files
  → Manually merge approved changes
  → Update prompts as needed
```

**Processing Time**: ~5-15 minutes for 30 files

### Multi-Phase Workflow

**Use Case**: Handling large tool families (15+ tools) or assembling from individual tool files

```
Step 1: Generate Complete Tool Files
  → pwsh ./Generate-MultiPageDocs.ps1 --complete-tools
  → Output: ../generated/tools/*.complete.md (208 files)

Step 2: Run Multi-Phase Assembly & Cleanup
  → pwsh ./GenerateToolFamilyCleanup-multifile.ps1
  
  Phase 1: Read and group tools by family
    → Groups 221 tools into 39 families
  
  Phase 2: Generate metadata per family
    → Output: ../generated/tool-family-metadata/*.md
    → AI generates: frontmatter + H1 + intro
  
  Phase 3: Generate related content per family
    → Output: ../generated/tool-family-related/*.md
    → AI generates: related links and resources
  
  Phase 4: Stitch together (no AI)
    → Output: ../generated/tool-family-multifile/*.md
    → Combines: metadata + tools + related content

Step 3: Review and Integrate
  → Review intermediate files (metadata, related content)
  → Verify final stitched files
  → Integrate approved outputs
```

**Processing Time**: ~10-20 minutes for 39 families

**Token Savings**: ~95% reduction (115K vs 2M tokens for large files)

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
   - Update `.env`: `TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME=gpt-4-turbo`
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
