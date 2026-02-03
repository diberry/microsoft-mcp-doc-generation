# Separated Tool Generation System

## Overview

This directory contains three independent .NET packages that implement a separated, modular approach to generating Azure MCP tool documentation. Each package handles one stage of the generation process.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Separated Tool Generation                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌───────────────────┐      ┌────────────────────┐             │
│  │ CLI Output JSON   │ ────▶│ RawToolGenerator   │             │
│  │ (cli-output.json) │      │  - Creates raw     │             │
│  └───────────────────┘      │    files with      │             │
│                              │    placeholders    │             │
│                              └─────────┬──────────┘             │
│                                        │                        │
│                                        ▼                        │
│  ┌───────────────────┐      ┌────────────────────┐             │
│  │ Generated Content │      │ ComposedTool       │             │
│  │ - Annotations     │ ───▶ │ Generator          │             │
│  │ - Parameters      │      │  - Replaces        │             │
│  │ - Example Prompts │      │    placeholders    │             │
│  └───────────────────┘      └─────────┬──────────┘             │
│                                        │                        │
│                                        ▼                        │
│  ┌───────────────────┐      ┌────────────────────┐             │
│  │ Azure OpenAI      │      │ ImprovedTool       │             │
│  │ (Optional)        │ ───▶ │ Generator          │             │
│  └───────────────────┘      │  - AI improvements │             │
│                              │  - Style guide     │             │
│                              └────────────────────┘             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Packages

### 1. RawToolGenerator

**Purpose**: Creates raw tool documentation files with placeholders from CLI output.

**Input**: 
- `./generated/cli/cli-output.json` (MCP CLI output)

**Output**:
- `./generated/tools-raw/*.md` (Raw files with placeholders)

**Key Features**:
- Uses brand-to-server-mapping.json for consistent filenames
- Generates proper frontmatter
- Inserts placeholders for content to be filled later

**README**: [RawToolGenerator/README.md](RawToolGenerator/README.md)

### 2. ComposedToolGenerator

**Purpose**: Composes complete tool documentation by replacing placeholders with actual content.

**Input**:
- `./generated/tools-raw/*.md` (Raw files from RawToolGenerator)
- `./generated/multi-page/annotations/*.md` (Annotation files)
- `./generated/multi-page/parameters/*.md` (Parameter files)
- `./generated/multi-page/example-prompts/*.md` (Example prompt files)

**Output**:
- `./generated/tools-composed/*.md` (Complete files with all content)

**Key Features**:
- Intelligent file matching
- Frontmatter stripping for embedded content
- Missing content handling
- Reports statistics on missing files

**README**: [ComposedToolGenerator/README.md](ComposedToolGenerator/README.md)

### 3. ImprovedToolGenerator

**Purpose**: Applies AI-based improvements to tool documentation using Azure OpenAI.

**Input**:
- `./generated/tools-composed/*.md` (Composed files)
- Azure OpenAI credentials (environment variables)

**Output**:
- `./generated/tools-ai-improved/*.md` (AI-improved files)

**Key Features**:
- Microsoft Style Guide enforcement
- Technical accuracy improvements
- Enhanced example prompts
- Configurable through prompt files
- Handles truncation gracefully

**README**: [ImprovedToolGenerator/README.md](ImprovedToolGenerator/README.md)

## Quick Start

### Prerequisites

1. **Build all packages**:
   ```bash
   cd docs-generation
   dotnet build RawToolGenerator/RawToolGenerator.csproj
   dotnet build ComposedToolGenerator/ComposedToolGenerator.csproj
   dotnet build ImprovedToolGenerator/ImprovedToolGenerator.csproj
   ```

2. **Ensure prerequisites exist**:
   - CLI output: `./generated/cli/cli-output.json`
   - Annotations: `./generated/multi-page/annotations/`
   - Parameters: `./generated/multi-page/parameters/`
   - Example prompts: `./generated/multi-page/example-prompts/`

3. **Configure Azure OpenAI** (optional, for phase 3):
   ```bash
   export FOUNDRY_API_KEY="your-api-key"
   export FOUNDRY_ENDPOINT="https://your-resource.openai.azure.com/"
   export FOUNDRY_MODEL_NAME="your-deployment-name"
   ```

### Run All Generators

Use the orchestration script:

```bash
cd docs-generation
pwsh ./Generate-SeparateTools.ps1
```

Or run individually:

```bash
# Phase 1: Raw generation
dotnet run --project RawToolGenerator \
  ../generated/cli/cli-output.json \
  ../generated/tools-raw \
  "2.0.0-beta.13"

# Phase 2: Composition
dotnet run --project ComposedToolGenerator \
  ../generated/tools-raw \
  ../generated/tools-composed \
  ../generated/multi-page/annotations \
  ../generated/multi-page/parameters \
  ../generated/multi-page/example-prompts

# Phase 3: AI Improvement (optional)
dotnet run --project ImprovedToolGenerator \
  ../generated/tools-composed \
  ../generated/tools-ai-improved \
  8000
```

## Orchestration Script

The `Generate-SeparateTools.ps1` script orchestrates all three generators:

```powershell
# Run all phases
./Generate-SeparateTools.ps1

# Skip raw generation (use existing)
./Generate-SeparateTools.ps1 -SkipRaw

# Skip AI improvements
./Generate-SeparateTools.ps1 -SkipImproved

# Increase AI token limit
./Generate-SeparateTools.ps1 -MaxTokens 12000
```

**Features**:
- Validates prerequisites before starting
- Runs generators in sequence
- Proper error handling and logging
- Progress reporting
- Summary statistics

## Output Structure

```
generated/
├── cli/
│   └── cli-output.json              # Input: CLI data
├── multi-page/
│   ├── annotations/                 # Input: Annotation files
│   ├── parameters/                  # Input: Parameter files
│   └── example-prompts/             # Input: Example prompt files
├── tools-raw/                       # Phase 1 output
│   └── *.md (208 files)
├── tools-composed/                  # Phase 2 output
│   └── *.md (208 files)
├── tools-ai-improved/               # Phase 3 output (optional)
│   └── *.md (208 files)
└── logs/
    └── separate-tools-*.log         # Generation logs
```

## Key Benefits

### Modularity
- Each generator has a single responsibility
- Easy to test and debug independently
- Can be run separately or together

### Flexibility
- Can skip stages if not needed
- AI improvements are optional
- Easy to customize each stage

### Quality Control
- Can review output at each stage
- Easier to identify where issues occur
- Clear separation of concerns

### Maintainability
- Smaller, focused codebases
- Independent versioning possible
- Easier to extend with new features

## Integration with Main Pipeline

See [INTEGRATION-PLAN.md](INTEGRATION-PLAN.md) for details on integrating this system into the main documentation generation pipeline.

## Development

### Adding New Features

**To modify raw file format**:
- Edit `RawToolGenerator/Services/RawToolGeneratorService.cs`
- Update `GenerateRawToolContent()` method

**To change content composition**:
- Edit `ComposedToolGenerator/Services/ComposedToolGeneratorService.cs`
- Modify `ComposeContent()` method

**To improve AI prompts**:
- Edit `ImprovedToolGenerator/Prompts/system-prompt.txt`
- Update `ImprovedToolGenerator/Prompts/user-prompt-template.txt`

### Testing

Each generator can be tested independently:

```bash
# Test with a small subset
dotnet run --project RawToolGenerator \
  test-cli-output.json \
  test-output \
  "test"
```

### Debugging

Enable verbose logging by examining the log files in `./generated/logs/`.

## Common Issues

### Missing Content Files

If ComposedToolGenerator reports missing content files:
1. Ensure annotations, parameters, and example prompts are generated
2. Check filename matching patterns
3. Review file counts in source directories

### AI Truncation

If AI improvements are truncated:
1. Increase `-MaxTokens` parameter
2. Check if composed files are too large
3. Consider processing files in batches

### Authentication Errors

If Azure OpenAI authentication fails:
1. Verify environment variables are set
2. Check `.env` file in docs-generation directory
3. Ensure credentials are valid and have proper permissions

## Performance

Typical processing times (208 tools):

- **Phase 1 (Raw)**: ~5-10 seconds
- **Phase 2 (Composed)**: ~10-15 seconds
- **Phase 3 (AI Improved)**: ~7-17 minutes

Total time with AI improvements: ~8-18 minutes

## Future Enhancements

Potential improvements to this system:

1. **Parallel Processing**: Process multiple files concurrently in Phase 3
2. **Caching**: Cache AI responses to avoid re-processing unchanged files
3. **Validation**: Add validation stage to check content quality
4. **Testing**: Add automated content testing stage
5. **Incremental Generation**: Only process changed files
6. **Quality Metrics**: Add metrics collection for content quality

## Contributing

When contributing to these packages:

1. Maintain independence - don't modify existing files
2. Follow existing patterns and conventions
3. Update READMEs when adding features
4. Test changes thoroughly
5. Document configuration changes

## License

Copyright (c) Microsoft Corporation. Licensed under the MIT License.
