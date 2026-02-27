# Morgan's Project History

## What I Know About This Project's C# Codebase

### Key Generator Files

**CSharpGenerator** (main documentation pipeline):
- `Program.cs` — Entry point, initializes LogFileHelper, reads CLI version, calls DocumentationGenerator
- `DocumentationGenerator.cs` — Core orchestrator, loads data files, calls per-generator classes
- `Config.cs` — Loads `docs-generation/data/config.json`
- `Generators/PageGenerator.cs` — Generates `tool-family/*.md` service pages
- `Generators/ParameterGenerator.cs` — Generates `parameters/*.md` include files
- `Generators/AnnotationGenerator.cs` — Generates `annotations/*.md` include files
- `Generators/FrontmatterUtility.cs` — Shared frontmatter generation

**Shared utilities I use constantly:**
- `Shared.DataFileLoader` — Thread-safe cached loading for all JSON data files
- `Shared.LogFileHelper` — Debug output to log files (use instead of Console.WriteLine for verbose output)
- `Shared.CliVersionReader` — Reads CLI version from `cli-version.json` for frontmatter

### Critical Patterns

**Parameter count logic** (AD-007):
- Show only non-common parameters in counts
- Exception: required parameters are always shown even if "common"
- Filtering in `ParameterGenerator.cs` (~line 110), `PageGenerator.cs` (~line 130), `DocumentationGenerator.cs` (~line 234)

**Filename generation** (three-tier resolution):
1. Check `brand-to-server-mapping.json` (highest priority)
2. Check `compound-words.json` (medium priority)  
3. Fall back to original area name
- Format: `{base}-{operation-parts}-{type}.md`
- Example: `azure-storage-account-get-annotations.md`

**Handlebars helpers** (`TemplateEngine/Helpers/`):
- `CoreHelpers.cs` — Generic (dates, strings, math)
- `McpHelpers.cs` — MCP-specific (`formatNaturalLanguage`, `formatCommand`, etc.)
- `formatNaturalLanguage` preserves ALL words including type qualifiers like "name"

### Common Issues I've Fixed

1. **ParameterCount Property Missing** — Was commented out with TODO in DocumentationGenerator.cs line ~400
2. **Output buffering** — Never use `$var = & dotnet ... 2>&1` — causes frozen output for long-running tasks
3. **Nullable warnings** — Always initialize nullable properties or use `= null!` with a comment

### Build Command
```bash
dotnet build docs-generation.sln --configuration Release
```
Zero warnings expected. Any warning is treated as an error.

### My Typical Workflow

1. Read the issue/task
2. Find the relevant generator file
3. Make the minimal change needed
4. Run `dotnet build docs-generation.sln --configuration Release` to verify zero warnings
5. Signal Parker to add tests
6. Signal Reeve to update README if behavior changed
