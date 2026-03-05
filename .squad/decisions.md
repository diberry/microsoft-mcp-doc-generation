# Shared Decisions — Azure MCP Documentation Generator

This file is the team's shared brain. Every agent reads this before starting work. Append new decisions here when making architectural choices.

## Numbering Convention

- Decision IDs (`AD-NNN`) are **permanent** — they are never renumbered or reused
- Always append new decisions at the end with the next sequential number
- Outdated decisions are moved to the **Archived Decisions** section (never deleted)
- References to `AD-NNN` in charter and history files remain stable

---

## Architecture Decisions

### AD-001: Never Edit Generated Output Files — Fix at the Right Layer
**Date**: Project inception (updated March 2026)  
**Decision**: Files under `generated/` and `generated-*/` are programmatic output. They must NEVER be edited directly. Fix the source generators instead. When fixing generators, prefer fixing the **prompt or template** over hardcoding string fixes in C# code. If the AI is producing wrong content, fix the prompt. If the layout is wrong, fix the template. Only use C# string manipulation for structural concerns (parsing, file I/O, data plumbing) — not for content corrections that belong in the AI or template layer.  
**Rationale**: Generated files are overwritten on every run. Hardcoded C# string fixes become brittle and don't scale across 52 namespaces — prompts and templates are the right abstraction for content fixes.  
**Enforced by**: All agents

---

### AD-002: Three-Tier Generation Architecture
**Date**: Project inception  
**Decision**: The generation pipeline has three tiers: (1) Orchestration via PowerShell, (2) Generation via C#/.NET 9, (3) Templates via Handlebars.Net.  
**Rationale**: Separation of concerns — PowerShell handles environment/CLI, C# handles logic, Handlebars handles formatting.  
**Files**: `docs-generation/scripts/preflight.ps1`, `docs-generation/CSharpGenerator/`, `docs-generation/templates/`

---

### AD-003: Central Package Management (CPM)
**Date**: Early project setup  
**Decision**: All NuGet package versions are defined in `docs-generation/Directory.Packages.props`. Individual `.csproj` files must NOT specify versions.  
**Rationale**: Prevents version drift across 15+ projects.

---

### AD-004: Namespace-Specific Output Directories
**Date**: 2026  
**Decision**: When generating for a single namespace (e.g., `./start.sh advisor`), output goes to `./generated-advisor/`. Full catalog goes to `./generated/`.  
**Rationale**: Allows working on one service without affecting the full documentation set.  
**Files**: `start.sh` lines 55-60, `docs-generation/scripts/start-only.sh`

---

### AD-005: All Scripts Consolidated in docs-generation/scripts/
**Date**: 2026  
**Decision**: All `.ps1` and `.sh` scripts live in `docs-generation/scripts/`. Only `start.sh` stays in repo root as the entry point.  
**Rationale**: Single location reduces confusion about where scripts live.

---

### AD-006: Sequential AI Generation with Exponential Backoff
**Date**: 2026  
**Decision**: AI generation calls (Azure OpenAI) process tools sequentially (not in parallel batches) with 5 retries and exponential backoff (1s→2s→4s→8s→16s).  
**Rationale**: Rate limiting on Azure OpenAI requires sequential processing to ensure all 208 tools complete.  
**Files**: `docs-generation/GenerativeAI/GenerativeAIClient.cs`

---

### AD-007: Parameter Count Shows Only Non-Common Parameters
**Date**: 2026  
**Decision**: Console output and `generation-summary.md` show counts of non-common parameters only (those appearing in parameter tables). Common params (`--tenant`, `--subscription`, etc.) are filtered unless required.  
**Files**: `docs-generation/data/common-parameters.json`, `CSharpGenerator/Generators/ParameterGenerator.cs`

---

### AD-008: Universal/Service-Agnostic Design
**Date**: 2026  
**Decision**: All generators, validators, prompts, and tests MUST work correctly for ALL 52 namespaces without service-specific logic. Use pattern-based detection instead of hardcoded service names.  
**Rationale**: Prevents breaking other services when fixing one.

---

### AD-009: Zero Warnings in Release Build
**Date**: 2026  
**Decision**: The CI build uses `--configuration Release` which treats warnings as errors. All compiler warnings must be resolved before pushing.  
**Command**: `dotnet build docs-generation.sln --configuration Release`

---

### AD-010: Every Bug Fix Requires a Test
**Date**: 2026  
**Decision**: Every bug fix must include one or more unit tests that reproduce the bug and verify the fix. Tests go in a `.Tests` project in `docs-generation.sln`.  
**Rationale**: Prevents regressions. If no `.Tests` project exists, create one.

---

### AD-011: Console Output Must Be Minimal
**Date**: 2026  
**Decision**: Console output shows only ✓ success indicators, progress messages, Generation Summary, and ⚠️ warnings. All debug/verbose output goes to log files via `LogFileHelper`.  
**Files**: `docs-generation/Shared/LogFileHelper.cs`

---

### AD-012: Data Files Live in docs-generation/data/
**Date**: 2026  
**Decision**: All JSON configuration files (`brand-to-server-mapping.json`, `common-parameters.json`, `compound-words.json`, `config.json`, `nl-parameters.json`, `static-text-replacement.json`, `stop-words.json`, `transformation-config.json`) are in `docs-generation/data/`.  
**Files**: `docs-generation/data/README.md`

---

### AD-013: Microsoft Foundry Branding (from Jan 1, 2026)
**Date**: January 2026  
**Decision**: Use "Microsoft Foundry" (not "Azure AI Foundry") in all content. "Azure AI Foundry Agents" → "Microsoft Foundry Agents".  
**Files**: `docs-generation/data/brand-to-server-mapping.json`, `docs-generation/data/transformation-config.json`

---

### AD-014: New .NET Projects Must Be Added to Solution
**Date**: 2026  
**Decision**: Every new .NET project must be added to `docs-generation.sln` via `dotnet sln add`. Every source project that needs testing must have a corresponding `.Tests` project also added to the solution.  
**Rationale**: CI build (`build-and-test.yml`) runs `dotnet build` and `dotnet test` on the solution.

---

### AD-015: Cross-Platform Script Interop: pwsh -File, Not -Command
**Date**: 2026  
**Decision**: When calling PowerShell scripts from bash, always use `pwsh -File "path/to/script.ps1" -Param value`, never `pwsh -Command`. Use `[switch]` not `[bool]` for flag parameters.  
**Rationale**: `pwsh -Command` fails on Windows Git Bash because MSYS paths (`/c/Users/...`) aren't translated inside string arguments.

---

### AD-016: Centralized Frontmatter Generation in Shared
**Date**: March 2026  
**Decision**: All YAML frontmatter generation is centralized in `Shared/FrontmatterUtility.cs`. CSharpGenerator and ExamplePromptGeneratorStandalone retain thin forwarding wrappers for backward compatibility. ToolGeneration_Raw calls `Shared.FrontmatterUtility` directly.  
**Rationale**: Three independent implementations had inconsistent date formats and duplicated logic. Centralizing in Shared follows the established pattern (DataFileLoader, LogFileHelper, CliVersionReader) and ensures consistency.  
**Files**: `Shared/FrontmatterUtility.cs`, `CSharpGenerator/Generators/FrontmatterUtility.cs` (wrapper), `ExamplePromptGeneratorStandalone/Utilities/FrontmatterUtility.cs` (wrapper), `ToolGeneration_Raw/Services/RawToolGeneratorService.cs`

---

### AD-017: Agent Skills Integration as Step 6
**Date**: March 2026  
**Decision**: Azure Agent Skills from `MicrosoftDocs/Agent-Skills` are ingested via a sync script, mapped to MCP namespaces via a curated JSON, and generate a "Related Skills" markdown file per namespace as Step 6 in the pipeline. Skills with no MCP namespace match are placed in an "Other" category. No AI is used for v1 — content is parsed directly from SKILL.md files.  
**Rationale**: Agent Skills provide curated Microsoft Learn documentation (troubleshooting, best practices, architecture patterns) that enriches MCP tool documentation. The sync→map→generate pipeline mirrors the existing CLI→parse→generate pattern.  
**Files**: `scripts/sync-agent-skills.sh`, `scripts/generate-skills-mapping.sh`, `scripts/6-Generate-RelatedSkills-One.ps1`, `RelatedSkillsGenerator/`, `data/skills-to-namespace-mapping.json`

---

## Open Questions

*(None currently)*

---

## Archived Decisions

*(Move outdated decisions here to keep the active list lean)*
