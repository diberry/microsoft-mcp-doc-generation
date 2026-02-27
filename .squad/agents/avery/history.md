# Avery's Project History

## What I Know About This Project

### Solution Structure (as of Feb 2026)

The solution has 15+ projects in `docs-generation/`:

**Core generators:**
- `CSharpGenerator/` — Main documentation generator, entry point for most generation tasks
- `TemplateEngine/` — Shared Handlebars.Net wrapper (used by 3+ generators)
- `NaturalLanguageGenerator/` — Text cleanup and NL parameter naming
- `TextTransformation/` — Text replacements (Azure AD → Entra ID, etc.)

**Specialized generators:**
- `ExamplePromptGeneratorStandalone/` — AI-powered example prompt generation
- `HorizontalArticleGenerator/` — Per-namespace overview articles
- `ToolFamilyCleanup/` — AI-based cleanup of tool family metadata
- `ToolGeneration_Raw/`, `ToolGeneration_Composed/`, `ToolGeneration_Improved/` — Tool doc generation pipeline

**Support:**
- `Shared/` — `DataFileLoader`, `LogFileHelper`, `CliVersionReader` (shared utilities)
- `GenerativeAI/` — Azure OpenAI client with retry logic
- `AzmcpCommandParser/` — Parses MCP CLI output
- `BrandMapperValidator/` — Validates brand mapping JSON
- `ToolMetadataExtractor/` — Extracts tool metadata

### Key Architectural Patterns I've Observed

1. **Orchestrator/Worker split**: `start.sh` (orchestrator) calls `start-only.sh` (worker) per namespace
2. **Data file centralization**: All JSON configs in `docs-generation/data/`, loaded via `Shared.DataFileLoader`
3. **Log file pattern**: `Shared.LogFileHelper` for verbose output, minimal console output
4. **Three-tier filename resolution**: Brand mapping → Compound words → Original name

### Data Files I Govern

- `data/brand-to-server-mapping.json` — 44+ brand name mappings (e.g., "acr" → "Azure Container Registry")
- `data/common-parameters.json` — 9 parameters filtered from all tools unless required
- `data/compound-words.json` — Word splits for filename generation
- `data/config.json` — Generator configuration
- `data/stop-words.json` — Words removed from filenames
- `data/transformation-config.json` — Text replacement rules (has overlap with brand-to-server-mapping.json — see branding consolidation plan)

### Known Issues I'm Tracking

- `transformation-config.json` and `brand-to-server-mapping.json` have duplicate brand mappings — consolidation is planned but not yet done
- New .NET 10 SDK needed for MCP server build (separate from the .NET 9 generators)

### My Approach When Asked to Review

1. Check if existing behavior is preserved
2. Verify new projects are added to `docs-generation.sln`
3. Verify tests exist for any new functionality
4. Check for hardcoded service names (violation of universal design principle)
5. Check README is updated
6. Approve or request changes with specific guidance
