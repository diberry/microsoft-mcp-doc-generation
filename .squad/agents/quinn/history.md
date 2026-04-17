# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### #195 — Generation Report Script (2026-03-24)
- **PR:** #217
- CLI metadata lives in `test-npm-azure-mcp/{version}/tools-list.json` (same schema as `cli-output.json`)
- Namespace JSON (`31-namespace.json`) has npm output prefix before JSON — any reader must handle that
- 55 namespaces, 235 tools in beta.31 — up from the 52 referenced in older docs
- Common params (7 total) are defined in `mcp-tools/data/common-parameters.json`
- `node:test` + `node:assert/strict` (built-in to Node 22) works well for zero-dep test suites in this project
- Tool `option[]` array uses `required: true` flag (not `isRequired`) — different from `common-parameters.json` which uses `isRequired`

### 2026-03-30: .NET Project Consolidation DevOps Review — APPROVED

**Verdict:** APPROVED (All Actions 1-6 operationally sound; NO CI/CD changes needed)

**Complete Assessment:**

| Action | Risk | Mitigation | Status |
|--------|------|-----------|--------|
| 1. CliAnalyzer removal | 🟢 LOW | Deprecation notice for optional `Invoke-CliAnalyzer.ps1` | APPROVED |
| 2. PostProcessVerifier merge | 🟢 LOW | Zero script/CI references; grep audit before deletion | APPROVED |
| 3. Core.NaturalLanguage merge | 🟢 LOW | Pure C# refactoring; no script impact | APPROVED |
| 4. NUnit→xUnit | 🟢 LOW | Auto-discovery continues; no workflow changes | APPROVED |
| 5. StripFrontmatter dedup | 🟢 LOW | Code cleanup only | APPROVED |
| 6. Document Validation.Tests | 🟢 LOW | README only | APPROVED |

**CI/CD Impact:**
- ✅ No GitHub Actions changes needed
- ✅ No Docker/devcontainer changes needed
- ✅ Build time: ~5% faster (fewer projects)
- ✅ Test discovery: Automatic (both frameworks supported)

**Scripts:** CliAnalyzer wrapper updated with deprecation; PostProcessVerifier callers audited (zero found).

**Recommended Addition:** `validate-consolidation.ps1` preflight script to verify solution consistency post-merge.

**Full audit:** Solution file will auto-update (Morgan's responsibility); build matrix unaffected; no new CI secrets/configs required.

**Decisions filed:** AD-027 (main), AD-030 (exit codes)

---

### #197 — .NET Consolidation DevOps Review (2026-03-26)
- **Consolidation Plan:** Avery's proposal to reduce 42 → 38 projects via 7 actions (6 approved, 1 deferred)
- **Key Finding:** Consolidation is **CI/CD safe**. No GitHub Actions changes needed; no Docker changes; no script blockers
- **CliAnalyzer usage:** Referenced only by optional `Invoke-CliAnalyzer.ps1` helper (not in core pipeline). Update with deprecation warning instead of full deletion for backward compatibility
- **PostProcessVerifier:** Zero callers in any script or CI workflow — safe to merge into ToolFamilyCleanup as a `--verify-only` flag
- **Core.NaturalLanguage merge:** Pure C# refactoring. Moving TextCleanup.cs to Core.Shared has zero impact on script orchestration (already referenced as Core.Shared dependency)
- **Test framework migration (NUnit→xUnit):** Both frameworks auto-discovered by `dotnet test`. No CI workflow changes needed after migration
- **New preflight check:** Recommend adding `validate-consolidation.ps1` to verify project refs, test counts, and solution file consistency post-merge
- **Build verification:** `dotnet build mcp-doc-generation.sln --configuration Release` will continue to work after all consolidations. No matrix changes needed
- **Docker/devcontainer:** No changes required — consolidation doesn't affect runtime dependencies or container image
