# Generation Scripts - Current Execution Order

This document explains the current numbered generation scripts and how they are orchestrated.

## Script Naming Convention

The active pipeline uses:

- **Preflight** once per run
- **Core Steps 1-4** for the namespace/family documentation pipeline
- **Step 4 post-assembly validation** via `Validate-ToolFamily-PostAssembly.ps1`
- **Supplementary Step 5** for skills relevance
- **Supplementary Step 6** for horizontal articles

```
start.sh
├── preflight.ps1
│   ├── validate-env.ps1
│   └── 0-Validate-BrandMappings.ps1
└── docs-generation/scripts/start-only.sh
    └── Generate-ToolFamily.ps1
        ├── 1-Generate-AnnotationsParametersRaw-One.ps1
        ├── 2-Generate-ExamplePrompts-One.ps1
        ├── 3-Generate-ToolGenerationAndAIImprovements-One.ps1
        ├── 4-Generate-ToolFamilyCleanup-One.ps1
        ├── Validate-ToolFamily-PostAssembly.ps1
        ├── 5-Generate-SkillsRelevance-One.ps1
        └── 6-Generate-HorizontalArticles-One.ps1
```

## Execution Order

### Preflight (`start.sh` → `docs-generation/scripts/preflight.ps1`)

Runs once before namespace processing begins.

**Purpose**:
- Validate `.env` for Azure OpenAI-backed steps
- Build the .NET solution
- Generate MCP CLI metadata
- Validate brand mappings before numbered generation steps run

### Step 1. Base Content (`1-Generate-AnnotationsParametersRaw-One.ps1`)

**Purpose**: Generate annotations, parameter includes, manifests, and raw tool markdown for one namespace/family.

**Outputs**:
- `generated*/annotations/*.md`
- `generated*/parameters/*.md`
- `generated*/parameters-manifests/*.json`
- `generated*/tools-raw/*.md`

### Step 2. Example Prompts (`2-Generate-ExamplePrompts-One.ps1`)

**Purpose**: Generate example prompts for each tool in the namespace/family and validate them when the target resolves to a specific tool command.

**Outputs**:
- `generated*/example-prompts/*.md`
- `generated*/example-prompts-prompts/*.md`
- `generated*/example-prompts-raw-output/*.txt`
- `generated*/example-prompts-validation/*.md`

### Step 3. Tool Composition and AI Improvements (`3-Generate-ToolGenerationAndAIImprovements-One.ps1`)

**Purpose**: Compose tool files and apply AI improvements for one namespace/family.

**Outputs**:
- `generated*/tools-composed/*.md`
- `generated*/tools-ai-improved/*.md`
- `generated*/tools/*.md`

### Step 4. Tool-Family Assembly (`4-Generate-ToolFamilyCleanup-One.ps1`)

**Purpose**: Assemble the tool-family article, related metadata, and final stitched output for one namespace/family.

**Outputs**:
- `generated*/tool-family/*.md`
- `generated*/tool-family-metadata/*.md`
- `generated*/tool-family-related/*.md`

### Step 4 Sub-Step. Post-Assembly Validation (`Validate-ToolFamily-PostAssembly.ps1`)

Runs automatically inside Step 4.

**Purpose**:
- Fail fast on dropped tools or mismatched tool counts
- Validate tool-file ↔ article-section cross references
- Emit warning-only checks for prompt/header/marker quality

**Outputs**:
- `generated*/reports/tool-family-validation-<namespace>.txt`

### Step 5. Skills Relevance (`5-Generate-SkillsRelevance-One.ps1`)

**Purpose**: Generate a supplementary GitHub Copilot skills relevance report for one namespace/family.

**Outputs**:
- `generated*/skills-relevance/<namespace>-skills-relevance.md`

### Step 6. Horizontal Articles (`6-Generate-HorizontalArticles-One.ps1`)

**Purpose**: Generate supplementary horizontal how-to articles for one namespace/family.

**Outputs**:
- `generated*/horizontal-articles/horizontal-article-<namespace>.md`

## Main Entry Points

### Root Orchestrator (`start.sh`)

**Usage**:
```bash
./start.sh
./start.sh advisor
./start.sh 1,2,3,4
./start.sh advisor 1,2,3,4,5,6
./start.sh advisor 4 --skip-deps    # Skip dependency validation for fast iteration
```

### Worker (`docs-generation/scripts/start-only.sh`)

**Usage**:
```bash
./docs-generation/scripts/start-only.sh advisor
./docs-generation/scripts/start-only.sh advisor 1,2,3,4
```

### Development Shortcut

Use the root helper below when you only want the early pipeline stages during local iteration:

```bash
./start-steps-1-2-only.sh
```

## Quick Reference

| Phase | Purpose | Dependencies |
|------|---------|--------------|
| Preflight | Environment validation, build, CLI metadata, brand validation | None |
| 1 | Annotations, parameters, raw tools | CLI metadata |
| 2 | Example prompts | Step 1 output recommended |
| 3 | Tool composition + AI improvements | Steps 1-2 |
| 4 | Tool-family assembly | Step 3 |
| 4 validation | Post-assembly validator report | Step 4 |
| 5 | Skills relevance (supplementary, non-fatal) | None beyond namespace context |
| 6 | Horizontal articles (supplementary) | Best after Step 4 |