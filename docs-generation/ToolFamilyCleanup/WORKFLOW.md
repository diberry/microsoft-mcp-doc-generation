# Tool Family Cleanup - Complete Workflow Guide

This guide shows how to use the Tool Family Cleanup tool in your documentation generation workflow.

## Overview

The Tool Family Cleanup is an **independent post-processing step** that applies Microsoft style guide standards to generated tool family documentation using LLM-based processing.

```
┌─────────────────────────────────────────────────────────────┐
│  Documentation Generation Workflow                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. Generate Base Documentation                            │
│     └─ Output: ./generated/multi-page/*.md                 │
│        (30+ tool family files like acr.md, storage.md)     │
│                                                             │
│  2. Run Tool Family Cleanup (NEW)                          │
│     └─ Input:  ./generated/multi-page/*.md                 │
│     └─ Output: ./generated/tool-family-cleanup/*.md        │
│     └─ Prompts: ./generated/tool-family-cleanup-prompts/   │
│                                                             │
│  3. Review & Integrate                                     │
│     └─ Compare original vs cleaned                         │
│     └─ Merge changes back to original files (manual)       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Prerequisites

### Required
- .NET 9.0 SDK
- PowerShell 7+
- Azure OpenAI credentials

### Azure OpenAI Setup

Create a `.env` file in `docs-generation/` directory:

```bash
FOUNDRY_API_KEY=your-api-key-here
FOUNDRY_ENDPOINT=https://your-resource.openai.azure.com/
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME=your-deployment-name
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION=2024-08-01-preview
```

**Important**: Never commit the `.env` file. It's already in `.gitignore`.

## Step-by-Step Workflow

### Step 1: Generate Base Documentation

Run the standard documentation generation:

```bash
cd docs-generation
pwsh ./Generate-MultiPageDocs.ps1
```

This creates tool family files in `./generated/multi-page/`:
- `acr.md`
- `aks.md`
- `storage.md`
- `keyvault.md`
- ... (30+ files)

### Step 2: Run Tool Family Cleanup

#### Option A: Using PowerShell Script (Recommended)

```bash
cd docs-generation
pwsh ./Generate-ToolFamilyCleanup.ps1
```

#### Option B: Direct .NET Execution

```bash
cd docs-generation/ToolFamilyCleanup
dotnet run --configuration Release
```

#### Option C: With Custom Paths

```bash
cd docs-generation
pwsh ./Generate-ToolFamilyCleanup.ps1 -InputDir "./generated/multi-page" -OutputDir "./generated/cleaned"
```

### Step 3: Review Generated Prompts

Check the prompts that were sent to the LLM:

```bash
cd generated/tool-family-cleanup-prompts
ls -la
```

Example prompt file: `acr-prompt.txt`
```text
SYSTEM PROMPT:
You are an expert technical writer and editor...

---

USER PROMPT:
Please review and clean up the following Azure MCP tool family documentation file...
```

This allows you to:
- Verify what instructions were given
- Debug unexpected outputs
- Iterate on prompt improvements

### Step 4: Review Cleaned Output

Compare original vs cleaned files:

```bash
# View original
cat ../generated/multi-page/acr.md

# View cleaned
cat ../generated/tool-family-cleanup/acr.md

# Or use diff
diff ../generated/multi-page/acr.md ../generated/tool-family-cleanup/acr.md
```

### Step 5: Integration

**Manual Integration** (Recommended):
1. Review each cleaned file
2. Verify technical accuracy
3. Check that all links and includes are preserved
4. Manually copy approved changes to original files

**Automated Integration** (Advanced):
```bash
# Backup originals first!
cp -r generated/multi-page generated/multi-page-backup

# Replace with cleaned versions
cp generated/tool-family-cleanup/*.md generated/multi-page/
```

## Common Use Cases

### Use Case 1: Initial Quality Pass

**When**: After generating documentation for the first time

**Steps**:
1. Generate base docs
2. Run cleanup on all files
3. Review 2-3 sample files to verify quality
4. If good, integrate all cleaned files
5. If issues, adjust prompts and re-run

### Use Case 2: Targeted Improvements

**When**: Fixing specific style issues in certain files

**Steps**:
```bash
# Create a temporary directory with just the files you want to fix
mkdir -p /tmp/selected-files
cp generated/multi-page/acr.md /tmp/selected-files/
cp generated/multi-page/aks.md /tmp/selected-files/

# Run cleanup on selected files
cd docs-generation
pwsh ./Generate-ToolFamilyCleanup.ps1 -InputDir "/tmp/selected-files"
```

### Use Case 3: Iterative Prompt Refinement

**When**: Testing new prompt instructions

**Steps**:
1. Edit prompt files in `docs-generation/prompts/tool-family-cleanup-*.txt`
2. Run cleanup on a single test file
3. Review output and prompts
4. Adjust prompts
5. Repeat until satisfied
6. Run on all files

### Use Case 4: CI/CD Integration

**When**: Automating in build pipeline

```yaml
# Example GitHub Actions workflow step
- name: Run Tool Family Cleanup
  run: |
    cd docs-generation
    pwsh ./Generate-ToolFamilyCleanup.ps1
  env:
    FOUNDRY_API_KEY: ${{ secrets.AZURE_OPENAI_KEY }}
    FOUNDRY_ENDPOINT: ${{ secrets.AZURE_OPENAI_ENDPOINT }}
    TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME: ${{ secrets.AZURE_OPENAI_MODEL }}
```

## Output Structure

After running cleanup, you'll have:

```
generated/
├── multi-page/                          # Original files (unchanged)
│   ├── acr.md
│   ├── aks.md
│   └── storage.md
│
├── tool-family-cleanup-prompts/         # Saved prompts for review
│   ├── acr-prompt.txt
│   ├── aks-prompt.txt
│   └── storage-prompt.txt
│
└── tool-family-cleanup/                 # Cleaned files
    ├── acr.md
    ├── aks.md
    └── storage.md
```

## Handling Errors

### Error: "Input directory not found"

**Cause**: No tool family files have been generated yet

**Solution**:
```bash
# Generate base documentation first
cd docs-generation
pwsh ./Generate-MultiPageDocs.ps1
```

### Error: "Missing required environment variables"

**Cause**: Azure OpenAI credentials not configured

**Solution**:
```bash
# Create .env file in docs-generation/
cd docs-generation
cat > .env << EOF
FOUNDRY_API_KEY=your-key
FOUNDRY_ENDPOINT=https://your-resource.openai.azure.com/
TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME=your-model
EOF
```

### Error: "Invalid markdown output"

**Cause**: LLM returned non-markdown content

**What to Check**:
1. Review the error log: `generated/tool-family-cleanup/{filename}-error.txt`
2. Check the prompt: `generated/tool-family-cleanup-prompts/{filename}-prompt.txt`
3. Verify the original file isn't malformed

**Solution**:
- Adjust system prompt to be more explicit about markdown-only output
- Add examples of correct output format
- Try with a simpler file to verify LLM is working

### Partial Processing

If some files succeed and others fail:

```bash
# Check which files were processed
ls generated/tool-family-cleanup/*.md

# Check which files have errors
ls generated/tool-family-cleanup/*-error.txt

# Re-run for failed files only
# (Move successful files out first, or use custom input dir)
```

## Performance Considerations

### Processing Time

- **Per file**: ~10-30 seconds (depending on LLM response time)
- **30 files**: ~5-15 minutes total
- **Large files** (>10KB): May take longer

### Cost Optimization

- Each file uses ~16,000 max tokens (input + output)
- For testing, use a single file first
- Consider running in off-peak hours for large batches

### Parallel Processing

The tool processes files sequentially. For faster processing:

```bash
# Split files into batches
# Run multiple instances in parallel with different input directories
```

## Best Practices

1. **Always Backup First**
   ```bash
   cp -r generated/multi-page generated/multi-page-backup
   ```

2. **Test on One File First**
   ```bash
   mkdir /tmp/test-file
   cp generated/multi-page/acr.md /tmp/test-file/
   # Run cleanup on /tmp/test-file
   ```

3. **Review Prompts Before Mass Processing**
   - Check one saved prompt file
   - Verify instructions are correct
   - Adjust if needed

4. **Verify Output Quality**
   - Manually review 2-3 cleaned files
   - Check for preserved links and includes
   - Verify technical accuracy

5. **Iterate on Prompts**
   - Start conservative
   - Add requirements gradually
   - Test after each change

6. **Document Your Prompt Changes**
   ```bash
   git commit -m "Add requirement for consistent tool naming in cleanup prompts"
   ```

## Troubleshooting Checklist

- [ ] .env file exists in docs-generation/
- [ ] .env file has all required variables
- [ ] Input directory exists and has .md files
- [ ] Prompt files exist in docs-generation/prompts/
- [ ] .NET 9.0 SDK is installed
- [ ] Azure OpenAI endpoint is accessible
- [ ] Azure OpenAI deployment is active

## Advanced Topics

### Custom Processing Rules

See `PROMPT-CUSTOMIZATION.md` for detailed guide on:
- Adding Azure MCP-specific standards
- Modifying style guide enforcement
- Creating custom validation rules

### Selective File Processing

Process only specific file patterns:

```bash
# Create a filtered input directory
mkdir /tmp/storage-files
cp generated/multi-page/storage*.md /tmp/storage-files/

# Process only storage-related files
cd docs-generation
pwsh ./Generate-ToolFamilyCleanup.ps1 -InputDir "/tmp/storage-files"
```

### Diff-Based Review

```bash
# Generate diff for each file
for file in generated/tool-family-cleanup/*.md; do
  filename=$(basename "$file")
  diff generated/multi-page/"$filename" "$file" > /tmp/"$filename".diff
done

# Review diffs
cat /tmp/*.diff
```

## Getting Help

- **README**: `docs-generation/ToolFamilyCleanup/README.md` - Full documentation
- **Prompt Guide**: `docs-generation/ToolFamilyCleanup/PROMPT-CUSTOMIZATION.md` - Customize prompts
- **This Guide**: Complete workflow examples

## Next Steps

1. Run your first cleanup on a test file
2. Review the output and prompts
3. Customize prompts for your needs (see PROMPT-CUSTOMIZATION.md)
4. Process all files
5. Integrate cleaned files into your documentation
