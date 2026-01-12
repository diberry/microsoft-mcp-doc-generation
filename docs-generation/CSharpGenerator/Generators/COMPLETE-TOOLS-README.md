# Complete Tool Documentation Generator

## Overview

The Complete Tool Documentation Generator creates consolidated documentation files (`.complete.md`) that combine all documentation elements for each MCP tool into a single, easy-to-reference file. Each complete file includes:

- Tool description
- Example prompts (via [!INCLUDE] reference)
- Parameters table (via [!INCLUDE] reference)
- Tool annotations (via [!INCLUDE] reference)

**Output Location:** `./generated/tools/{tool-name}.complete.md`  
**Total Files Generated:** 208 (one per tool)

## Architecture

### Execution Flow

The Complete Tool Generator follows a **read-after-write** pattern:

```
┌─────────────────────────────────────────────────────────────┐
│  GENERATION SEQUENCE (in DocumentationGenerator.cs)        │
└─────────────────────────────────────────────────────────────┘

1. AnnotationGenerator          → ./generated/annotations/
   └─ Creates: {tool}-annotations.md

2. ParameterGenerator           → ./generated/parameters/
   └─ Creates: {tool}-parameters.md

3. ParamAnnotationGenerator     → ./generated/param-and-annotation/
   └─ Creates: {tool}-param-and-annotation.md

4. CompleteToolGenerator        → ./generated/tools/       ← NEW
   └─ Reads from steps 1-2
   └─ Creates: {tool}.complete.md
```

### Key Design Principles

✅ **No Duplication** - Reads from existing generated files, doesn't regenerate content  
✅ **Separation of Concerns** - Completely separate from other generators  
✅ **Optional Feature** - Only runs when `--complete-tools` flag is provided  
✅ **Minimal Integration** - Only 20 lines of code added to existing files

## File Locations

### New Files (Complete Feature)

```
docs-generation/
├── CSharpGenerator/
│   └── Generators/
│       └── CompleteToolGenerator.cs           ← Generator class (200 lines)
└── templates/
    └── tool-complete-template.hbs             ← Handlebars template (30 lines)
```

### Integration Points (Existing Files Modified)

```
docs-generation/
└── CSharpGenerator/
    ├── DocumentationGenerator.cs              ← +17 lines
    │   ├── Added generateCompleteTools parameter
    │   ├── Initialized CompleteToolGenerator
    │   └── Added generation call (lines 277-289)
    └── Program.cs                             ← +3 lines
        ├── Added --complete-tools flag parsing
        └── Passed flag to GenerateAsync
```

### Output Files

```
generated/
├── annotations/                               ← Read by CompleteToolGenerator
│   └── {tool}-annotations.md
├── parameters/                                ← Read by CompleteToolGenerator
│   └── {tool}-parameters.md
├── example-prompts/                           ← Read by CompleteToolGenerator
│   └── {tool}-example-prompts.md
└── tools/                                     ← Created by CompleteToolGenerator
    └── {tool}.complete.md                     ← 208 files
```

## How It Works

### Step 1: CLI Flag Detection

**File:** `Program.cs` (line ~146)

```csharp
var generateCompleteTools = args.Contains("--complete-tools");
```

### Step 2: Generator Initialization

**File:** `DocumentationGenerator.cs` (line ~249)

```csharp
var completeToolGenerator = new CompleteToolGenerator(
    LoadBrandMappingsAsync,
    CleanFileNameAsync);
```

### Step 3: Conditional Generation

**File:** `DocumentationGenerator.cs` (lines 277-289)

```csharp
// Generate complete tool files if requested (at parent level)
if (generateCompleteTools)
{
    var toolsDir = Path.Combine(parentDir, "tools");
    Directory.CreateDirectory(toolsDir);
    var completeToolTemplate = Path.Combine(templatesDir, "tool-complete-template.hbs");
    await completeToolGenerator.GenerateCompleteToolFilesAsync(
        transformedData,
        toolsDir,
        completeToolTemplate,
        annotationsDir,
        parametersDir,
        examplePromptsDir ?? Path.Combine(parentDir, "example-prompts"));
}
```

### Step 4: File Generation Logic

**File:** `CompleteToolGenerator.cs` (lines 50-180)

For each tool:
1. **Parse command** to build filename using brand mapping
2. **Check source files exist** (annotations, parameters, example-prompts)
3. **Build filename** using same logic as other generators:
   - Format: `{brand-filename}-{operation}.complete.md`
   - Example: `acr-registry-list.complete.md`
4. **Prepare template data** with include file references
5. **Generate markdown file** using Handlebars template

### Step 5: Template Processing

**File:** `tool-complete-template.hbs`

The template creates a complete documentation page with:
- Frontmatter (metadata, dates, version)
- Tool name and description
- [!INCLUDE] references to existing files (no content duplication)

## Key Components Explained

### CompleteToolGenerator.cs

**Purpose:** Main generator class that orchestrates complete tool file creation

**Dependencies:**
- `LoadBrandMappingsAsync` - Gets brand name mappings (acr → "acr", etc.)
- `CleanFileNameAsync` - Removes stop words, handles compound words

**Key Method:**
```csharp
public async Task GenerateCompleteToolFilesAsync(
    TransformedData data,           // All tool data from CLI
    string outputDir,                // ./generated/tools/
    string templateFile,             // tool-complete-template.hbs
    string annotationsDir,           // ./generated/annotations/
    string parametersDir,            // ./generated/parameters/
    string examplePromptsDir)        // ./generated/example-prompts/
```

**Logic Flow:**
1. Loop through all tools in `data.Tools`
2. Build filename using brand mapping + command parsing
3. Verify source files exist (annotations, parameters)
4. Create template data dictionary
5. Process Handlebars template
6. Write output file

### tool-complete-template.hbs

**Purpose:** Handlebars template defining the structure of complete documentation

**Template Variables:**
- `{{command}}` - Full tool command (e.g., "acr registry list")
- `{{description}}` - Tool description (cleaned, period-ended)
- `{{annotationsFileName}}` - Filename for annotations include
- `{{parametersFileName}}` - Filename for parameters include
- `{{examplePromptsFileName}}` - Filename for example prompts include
- `{{generatedAt}}` - UTC timestamp
- `{{version}}` - MCP CLI version

**Output Structure:**
```markdown
---
frontmatter with metadata
---

# {command}

<!-- @mcpcli {command} -->

{description}

Example prompts include:
[!INCLUDE reference to example-prompts file]

[!INCLUDE reference to parameters file]

Tool annotation hints:
[!INCLUDE reference to annotations file]
```

## Filename Resolution

The generator uses a **three-tier resolution system** (same as other generators):

### 1. Brand Mapping (Highest Priority)
**File:** `brand-to-server-mapping.json`

Maps MCP server names to brand filenames:
```json
{
  "McpServerName": "acr",
  "FileName": "acr",
  "BrandName": "Azure Container Registry"
}
```

### 2. Compound Words (Medium Priority)
**File:** `compound-words.json`

Transforms concatenated words:
```json
{
  "storagesync": "storage-sync",
  "eventhubs": "event-hubs"
}
```

### 3. Original Name (Fallback)
If no mapping found, uses lowercase command area name.

### Example Filename Generation

**Command:** `acr registry list`

```
1. Parse: ["acr", "registry", "list"]
2. Brand lookup: "acr" → "acr"
3. Remaining: "registry-list"
4. Clean: "registry-list" (no stop words to remove)
5. Result: acr-registry-list.complete.md
```

## Usage

### Generate Complete Tools (Standalone)

```bash
cd docs-generation
dotnet run --project CSharpGenerator/CSharpGenerator.csproj -- \
  generate-docs \
  ../generated/cli/cli-output.json \
  ../generated/output-dir \
  --complete-tools \
  --version "1.0.0"
```

### Generate with Other Documentation

```bash
dotnet run --project CSharpGenerator/CSharpGenerator.csproj -- \
  generate-docs \
  ../generated/cli/cli-output.json \
  ../generated/output-dir \
  --index \
  --commands \
  --complete-tools \
  --version "1.0.0"
```

### Skip Complete Tools (Default)

Simply omit the `--complete-tools` flag - no files will be generated in `./generated/tools/`.

## Maintenance Guide

### To Change Output Format

**Edit:** `tool-complete-template.hbs`

Modify the Handlebars template structure. Common changes:
- Add/remove sections
- Change heading styles
- Modify frontmatter
- Reorder [!INCLUDE] references

### To Change Filename Pattern

**Edit:** `CompleteToolGenerator.cs` (lines 95-105)

Modify the filename building logic:
```csharp
var completeFileName = $"{baseFileName}.complete.md";
```

Change to:
```csharp
var completeFileName = $"{baseFileName}-full.md";  // or any pattern
```

### To Add New Template Variables

**Steps:**
1. **Update template data** in `CompleteToolGenerator.cs` (line ~145):
   ```csharp
   templateData["newVariable"] = someValue;
   ```

2. **Update template** in `tool-complete-template.hbs`:
   ```handlebars
   {{newVariable}}
   ```

### To Change Generation Order

**Edit:** `DocumentationGenerator.cs` (lines 270-295)

Move the complete tools generation block to a different position. Note:
- Must generate AFTER annotations, parameters, and example-prompts
- Can generate BEFORE or AFTER area pages, index, etc.

### To Add Source File Validation

**Edit:** `CompleteToolGenerator.cs` (lines 128-145)

Add additional file existence checks:
```csharp
var someOtherPath = Path.Combine(someDir, someFileName);
if (!File.Exists(someOtherPath))
{
    filesExist = false;
}
```

## Troubleshooting

### Issue: Files Not Generated

**Check:**
1. Is `--complete-tools` flag present?
2. Do source files exist? (annotations, parameters, example-prompts)
3. Check console output for error messages
4. Verify `brand-to-server-mapping.json` is accessible

### Issue: Wrong Filenames

**Check:**
1. Brand mapping configuration
2. Compound words configuration
3. Stop words list (shouldn't affect complete tools, but could)

### Issue: Template Errors

**Check:**
1. Handlebars syntax in `tool-complete-template.hbs`
2. Variable names match between generator and template
3. Helper functions registered in `HandlebarsTemplateEngine.cs`

### Issue: Missing [!INCLUDE] Files

**Check:**
1. Source files generated correctly in previous steps?
2. Filenames match between generator logic and actual files?
3. Example prompts are optional - check `hasExamplePrompts` flag

## Testing

### Manual Test

```bash
# 1. Build
cd docs-generation/CSharpGenerator
dotnet build --configuration Release

# 2. Run with test output
cd ..
dotnet run --project CSharpGenerator/CSharpGenerator.csproj -- \
  generate-docs \
  ../generated/cli/cli-output.json \
  ../generated/test-output \
  --complete-tools \
  --version "test"

# 3. Verify output
ls -la ../generated/test-output/tools/*.complete.md | wc -l
# Should show 208 files

# 4. Check content
cat ../generated/test-output/tools/acr-registry-list.complete.md
```

### Expected Output

```
┌─────────────────────────────────────────────┐
│  Generating Complete Tool Files            │
└─────────────────────────────────────────────┘
  Loaded X brand mappings
  Progress: 20 files generated...
  Progress: 40 files generated...
  ...
  ✅ Complete Tool Files Generated: 208
  Output directory: /path/to/generated/tools
```

## Performance

- **Generation Time:** ~2-3 seconds for 208 tools
- **Memory:** Minimal (reads one file at a time)
- **Disk I/O:** Read 3 files per tool, write 1 file per tool

## Future Enhancements

Potential improvements:
- [ ] Add progress bar for large tool sets
- [ ] Support filtering by service area
- [ ] Add validation for [!INCLUDE] file existence
- [ ] Generate summary index of all complete tools
- [ ] Support different output formats (HTML, PDF)
- [ ] Add caching for brand mappings

## Related Documentation

- **Parent README:** `../../README.md`
- **Generator Pattern:** See other generators in `Generators/` directory
- **Template Guide:** See other templates in `../../templates/`
- **Configuration Files:** `../../brand-to-server-mapping.json`, `../../compound-words.json`

## Summary

The Complete Tool Documentation Generator is a **non-invasive, optional feature** that reads from existing generated files to create comprehensive tool documentation. It follows the same patterns as other generators, making it easy to understand and maintain. The architecture ensures no duplication and minimal coupling with existing code.

**Key Takeaway:** This generator doesn't duplicate logic or content - it **composes** existing documentation artifacts into a convenient single-file format using [!INCLUDE] references.
