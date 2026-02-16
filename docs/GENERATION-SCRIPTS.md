# Generation Scripts - Execution Order

This document explains the numbered generation scripts and their execution order.

## Script Naming Convention

All generation scripts are now numbered to indicate their execution order:

```
docs-generation/scripts/
├── Generate.ps1                                    # Main orchestrator (runs all steps)
├── 1-Generate-AnnotationsParametersRaw.ps1        # Step 1: Base content
├── 2-Generate-ToolGenerationAndAIImprovements.ps1 # Step 2: Tool pages with AI
├── 3-Generate-ExamplePrompts.ps1                   # Step 3: Example prompts
├── 4-Generate-ToolFamilyFiles.ps1                  # Step 4: Tool families
├── 5-Validate-total-tool-count-and-family.ps1     # Step 5: Validation & reports
└── 2-Generate-ExamplePrompts-One.ps1              # Utility: Test single tool
```

## Execution Order

### 1. Base Content Generation (`scripts/1-Generate-AnnotationsParametersRaw.ps1`)

**Purpose**: Generates foundational include files and raw tool documentation

**Outputs**:
- `generated/annotations/*.md` - Tool annotation include files
- `generated/parameters/*.md` - Parameter documentation include files
- `generated/tools-raw/*.md` - Raw tool pages with placeholders
- `generated/common-general/commands.md` - Commands overview page
- `generated/common-general/common.md` - Common tools page

**Usage**:
```bash
pwsh ./scripts/1-Generate-AnnotationsParametersRaw.ps1 -OutputPath ../generated
```

### 2. Tool Generation with AI Improvements (`scripts/2-Generate-ToolGenerationAndAIImprovements.ps1`)

**Purpose**: 3-phase tool page generation with AI enhancements

**Phases**:
1. **Raw**: Create tool pages with placeholders
2. **Composed**: Replace placeholders with actual content (annotations, parameters, examples)
3. **Improved**: Apply AI-based improvements to descriptions and content

**Outputs**:
- `generated/tools-raw/*.md` - Raw tool pages
- `generated/tools-composed/*.md` - Composed tool pages
- `generated/tools-ai-improved/*.md` - AI-improved tool pages

**Usage**:
```bash
pwsh ./scripts/2-Generate-ToolGenerationAndAIImprovements.ps1 -OutputPath ../generated
pwsh ./scripts/2-Generate-ToolGenerationAndAIImprovements.ps1 -SkipRaw  # Use existing raw files
pwsh ./scripts/2-Generate-ToolGenerationAndAIImprovements.ps1 -SkipImproved  # Skip AI improvements
```

### 3. Example Prompts Generation (`scripts/3-Generate-ExamplePrompts.ps1`)

**Purpose**: Generate and validate natural language example prompts using Azure OpenAI

**Outputs**:
- `generated/example-prompts/*.md` - Example prompt files (5 prompts per tool)
- `generated/example-prompts-prompts/*.md` - Input prompts sent to AI
- `generated/example-prompts-raw-output/*.txt` - Raw JSON from AI responses
- `generated/example-prompts-validation/*.md` - Validation reports

**Usage**:
```bash
pwsh ./scripts/3-Generate-ExamplePrompts.ps1 -OutputPath ../generated
```

**Testing Single Tool**:
```bash
pwsh ./scripts/2-Generate-ExamplePrompts-One.ps1 -ToolCommand "keyvault secret create" -OutputPath "../generated"
```

### 4. Tool Family Generation (`scripts/4-Generate-ToolFamilyFiles.ps1`)

**Purpose**: Generate complete tool documentation and tool family cleanup

**Outputs**:
- `generated/tools/*.complete.md` - Complete tool pages (all-in-one documentation)
- Cleaned tool family files with AI-generated improvements

**Usage**:
```bash
pwsh ./scripts/4-Generate-ToolFamilyFiles.ps1 -OutputPath ../generated
```

### 5. Validation and Reporting (`scripts/5-Validate-total-tool-count-and-family.ps1`)

**Purpose**: Validate tool counts between sources and generate comparison reports

**Outputs**:
- `generated/ToolDescriptionEvaluator.json` - ToolDescriptionEvaluator output copy
- `generated/tool-count-comparison.json` - Detailed comparison report
- Console output with tool count statistics and any discrepancies

**Usage**:
```bash
pwsh ./scripts/5-Validate-total-tool-count-and-family.ps1 -OutputPath ../generated
```

**Validates**:
- CLI output tool count vs ToolDescriptionEvaluator tool count
- Identifies missing or extra tools between sources
- Generates comparison reports for analysis

## Main Orchestrator (`scripts/Generate.ps1`)

The main entry point that runs all generation steps in order.

**Usage**:
```bash
cd docs-generation
pwsh ./scripts/Generate.ps1 -OutputPath ../generated
```

**Options**:
- `-Format` - Output format: 'json', 'yaml', or 'both' (default: json)
- `-CreateIndex` - Create index page (default: true)
- `-CreateCommon` - Create common tools page (default: true)
- `-CreateCommands` - Create commands page (default: true)
- `-CreateToolPages` - Create per-service tool pages (default: false)
- `-CreateServiceOptions` - Create service start options page (default: true)
- `-ExamplePrompts` - Generate example prompts with AI (default: true)

## Quick Reference

| Script | Purpose | Duration | Dependencies |
|--------|---------|----------|--------------|
| 1 | Base content (annotations, parameters, raw tools) | 2-5 min | CLI output |
| 2 | Tool pages (raw → composed → improved) | 5-15 min | Step 1 |
| 3 | Example prompts with AI | 15-30 min | CLI output, Azure OpenAI |
| 4 | Tool families and complete tools | 10-20 min | Steps 1-3 |
| 5 | Validate tool counts and generate reports | 2-5 min | Steps 1-4 |

## Development Workflow

### Full Generation
```bash
cd docs-generation
pwsh ./scripts/Generate.ps1 -OutputPath ../generated
```

### Partial Generation (Skip Steps)
```bash
# Skip example prompts (faster for testing)
pwsh ./scripts/Generate.ps1 -ExamplePrompts $false

# Run individual steps
pwsh ./scripts/1-Generate-AnnotationsParametersRaw.ps1 -OutputPath ../generated
pwsh ./scripts/2-Generate-ToolGenerationAndAIImprovements.ps1 -OutputPath ../generated
pwsh ./scripts/3-Generate-ExamplePrompts.ps1 -OutputPath ../generated
pwsh ./scripts/4-Generate-ToolFamilyFiles.ps1 -OutputPath ../generated
```

### Testing Changes
```bash
# Test a single tool's example prompt generation
pwsh ./scripts/2-Generate-ExamplePrompts-One.ps1 -ToolCommand "storage account list"

# Test without validation
pwsh ./scripts/2-Generate-ExamplePrompts-One.ps1 -ToolCommand "storage account list" -SkipValidation
```

## Validation Scripts

Additional validation scripts are available in `docs-generation/scripts/standalone/` for verifying annotation files:

### Annotation Hints Verification (`verify-annotation-hints.js`)

Verifies that annotation INCLUDE statements have the required "Tool annotation hints" line immediately before them.

**Usage**:
```bash
cd docs-generation/scripts/standalone
node verify-annotation-hints.js
```

**Outputs**:
- `annotation-hints-report.md` - Detailed report of all annotation INCLUDE statements
- Console summary with pass/fail status

### Annotation References Verification (`verify-annotation-references.js`)

Verifies annotation files are properly referenced in tool files (no orphans, no duplicates, no missing files).

**Usage**:
```bash
cd docs-generation/scripts/standalone
node verify-annotation-references.js
```

**Outputs**:
- `annotation-reference-report.md` - Detailed report of annotation file references
- Console summary with statistics

Both scripts exit with status code 1 if issues are found, making them suitable for CI/CD pipelines.

## Migration Notes

**Old Names → New Names:**
- `Generate-MultiPageDocs.ps1` → `Generate.ps1`
- `Generate-AnnotationsParametersRaw.ps1` → `1-Generate-AnnotationsParametersRaw.ps1`
- `Generate-ToolGenerationAndAIImprovements.ps1` → `2-Generate-ToolGenerationAndAIImprovements.ps1`
- `Generate-ExamplePrompts.ps1` → `3-Generate-ExamplePrompts.ps1`
- `scripts/Generate-ToolFamilyFiles.ps1` → `4-Generate-ToolFamilyFiles.ps1` (moved to root)
- `Test-SingleToolPrompt.ps1` → `GenerateExamplePrompt-One.ps1`

**All references updated in:**
- PowerShell scripts (`.ps1`)
- Shell scripts (`.sh`)
- Documentation files (`README.md`, `ARCHITECTURE.md`, etc.)
- Instruction files (`.github/copilot-instructions.md`)
- Docker files (`Dockerfile`)

## Last Updated

February 7, 2026
