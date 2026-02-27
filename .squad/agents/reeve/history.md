# Reeve's Project History

## What I Know About Documentation in This Project

### Documentation Structure

**Root-level docs:**
- `README.md` — Main entry point, quick start, generation steps table
- `docs/ARCHITECTURE.md` — Architecture diagrams and overview
- `docs/GENERATION-SCRIPTS.md` — Script reference
- `docs/START-SCRIPTS.md` — Start scripts documentation
- `docs/QUICK-START.md` — 5-minute guide

**Subsystem docs:**
- `docs-generation/README.md` — Generator system overview
- `docs-generation/data/README.md` — Data files documentation
- `docs-generation/scripts/README.md` — Scripts directory overview
- Per-project `README.md` in each `docs-generation/<project>/` directory

**AI project docs:**
- `docs-generation/ExamplePromptGeneratorStandalone/README.md`
- `docs-generation/HorizontalArticleGenerator/README.md`

**Copilot instructions:**
- `.github/copilot-instructions.md` — Project instructions for GitHub Copilot

### Decisions I've Recorded

All 15 architectural decisions are in `.squad/decisions.md` (AD-001 through AD-015).

Key decisions to know:
- **AD-001**: Never edit generated files
- **AD-003**: Central Package Management
- **AD-008**: Universal/service-agnostic design
- **AD-009**: Zero warnings in Release build
- **AD-010**: Every bug fix needs a test
- **AD-015**: Cross-platform script interop (pwsh -File)

### README Update Patterns I Follow

When a script is added or modified:
```markdown
### New Script Name
**Purpose**: One sentence  
**Usage**: `./script.sh [args]`  
**Steps**: Table of steps with duration
```

When a new .NET project is added:
```markdown
## ProjectName
Purpose, key classes, entry point command, environment variables
```

When a CLI argument changes:
```
| Old flag | New flag | Notes |
```

### Copilot Instructions Updates

I update `.github/copilot-instructions.md` when:
- New project added to solution → Add to "Key Components" section
- Data file location changes → Update "Data Files" section
- New convention established → Add to "Code Conventions" section
- New important pattern → Add dedicated section

### Session Summary Format

After a multi-step task:

```markdown
## Session: [Date] — [Task Name]

**What was done:**
- [Agent] did X
- [Agent] did Y

**Files changed:**
- `path/to/file.cs` — Purpose
- `path/to/test.cs` — Tests for above

**New decisions logged:**
- AD-NNN: Description

**Remaining work (if any):**
- [ ] Item 1
```

### When I Review READMEs

My checklist:
1. Does it match the actual code (not an outdated description)?
2. Are all CLI flags documented?
3. Are all required environment variables listed?
4. Is there a "how to run tests" section for source projects?
5. Is the purpose statement accurate?
