<!-- Location: docs/prds/ is correct per this repo's convention. project-dina operating rules apply to the hub repo only. -->

# PRD — Migrate `test-npm-azure-mcp` from NPM to .NET

| Field | Value |
|-------|-------|
| **Issue** | #627 |
| **Author** | nigel-prd-planning |
| **Date** | 2026-05-27T16-12 |
| **Status** | Draft — Blocked on OQ1 |
| **Project** | azure-ai-tools |
| **Post-Ship Owner** | aspira-eng-dotnet |

---

## 1. Problem Statement

The `test-npm-azure-mcp/` folder is the sole NPM package in a pipeline that is otherwise entirely .NET. It depends on `@azure/mcp` (an npm package) to install the Azure MCP CLI binary and extract tool metadata (version, tools JSON, namespace JSON) for every documentation generation run. This NPM footprint requires Node.js setup, `npm install`, and an npm package version tracking workflow — none of which are consistent with the .NET toolchain used everywhere else. The nightly `update-azure-mcp.yml` workflow version-tracks via npm, and `preflight.ps1` `Push-Location`s into this folder to call `npm run` scripts. When the rest of the pipeline runs `.NET 9 / .NET 10`, this Node.js island adds cognitive overhead, CI setup cost, and a separate dependency management surface.

Additionally, the folder name `test-npm-azure-mcp` is a legacy artifact: the folder does not contain only tests — it is the CLI metadata extraction entry point for the entire pipeline. The name misleads contributors about its scope and purpose.

---

## 2. Root Cause Analysis

### 2a. Historical NPM origin

When the pipeline was bootstrapped, the Azure MCP CLI (`azmcp`) was first published as an npm package (`@azure/mcp`). .NET packaging was not yet available, so npm was the only reliable install path. The entire extraction layer was written in Node.js scripts (`generate-cli-examples.js`, `validate-cli-output.js`, etc.) at that time.

### 2b. No migration trigger on .NET availability

As the pipeline matured into a fully typed .NET solution (`DocGeneration.PipelineRunner`, Steps 0–6), no issue was raised to retire the npm island. The structural gap persisted because the CLI metadata extraction was "already working" and not blocking other work.

### 2c. Misleading folder name

`test-npm-azure-mcp` implies the folder is a test harness for npm package updates. In practice it is the **pipeline bootstrap data source** — it stores version snapshots, diff files, and CLI examples that feed every Step 0 run. The name misdirects contributors who read it as "test infra, safe to ignore."

---

## 3. Proposed Solution

Replace the Node.js-based CLI metadata extraction layer with an equivalent .NET-based implementation. The new folder (`mcp-cli-metadata/`) will:

1. **Use the `dotnet tool` ecosystem** — install `azmcp` as a .NET global tool (if published via NuGet) or invoke the locally-built `azmcp` binary from the `.NET 10` MCP server project already compiled during Docker build. The CI workflow and `preflight.ps1` will call `dotnet tool run azmcp` (or the compiled binary path) instead of `npx azmcp`.

2. **Preserve all existing outputs** — same three files (`cli-version.json`, `cli-output.json`, `cli-namespace.json`) written to `generated/cli/`. No downstream C# code changes needed.

3. **Migrate Node.js scripts to C# / PowerShell** — `generate-cli-examples.js`, `validate-cli-output.js`, `generate-report.js`, `compare-2x-3x.js`, `diff-versions.js` become either:
   - C# console apps in `mcp-tools/` (preferred, consistent with pipeline), OR
   - PowerShell scripts (if trivial string/JSON manipulation only)
   - Node.js test files (`test/*.test.js`) are migrated to xUnit in a new `.Tests` project.

4. **Rename the folder** — `test-npm-azure-mcp/` → `mcp-cli-metadata/`. This name describes the actual purpose: storing versioned CLI metadata snapshots and tooling for the pipeline.

5. **Update all references** — workflows, scripts, docs, tests, and `build-and-test.yml` path triggers updated to the new folder name.

6. **Update nightly CI** — `update-azure-mcp.yml` is rewritten to track the .NET tool version (NuGet) rather than the npm version; `test-azure-mcp-update.yml` is updated or replaced with a .NET equivalent.

**Decision point — dotnet tool vs compiled binary:**
- If `azmcp` is published to NuGet as a global tool: use `dotnet tool install -g azmcp` / `dotnet tool run azmcp`.
- If not yet on NuGet: invoke the compiled binary produced by the existing Docker `mcp-builder` stage (`/mcp/servers/Azure.Mcp.Server/src`). A `BootstrapConfig.cs` property `McpBinaryPath` already exists.
- This OQ is tracked as OQ1 below.

---

## 4. Acceptance Criteria

| # | Criterion | How Measured |
|---|-----------|-------------|
| AC1 | Folder `test-npm-azure-mcp/` is renamed to `mcp-cli-metadata/` in the repo | `git ls-files mcp-cli-metadata/ \| wc -l` > 0; `git ls-files test-npm-azure-mcp/ \| wc -l` = 0 |
| AC2 | No `package.json` or `node_modules/` at repo root or in `mcp-cli-metadata/` | `Find-ChildItem mcp-cli-metadata -Name package.json -Recurse` returns empty |
| AC3 | `preflight.ps1` calls the .NET tool/binary instead of `npm run` | `grep -r "npm run" mcp-tools/scripts/preflight.ps1` returns no matches |
| AC4 | `generated/cli/cli-output.json`, `cli-namespace.json`, `cli-version.json` produced correctly | `dotnet test mcp-doc-generation.sln` passes all BootstrapStep tests; output JSON structure byte-for-byte matches existing Node.js output when run against the same azmcp binary (validated by comparison test in McpCliMetadata.Tests) |
| AC5 | `build-and-test.yml` path triggers updated to `mcp-cli-metadata/**` | File content confirms no `test-npm-azure-mcp` path trigger |
| AC6 | `update-azure-mcp.yml` tracks .NET tool/NuGet version | Workflow file contains no `npm install @azure/mcp` or `npm view` commands |
| AC7 | `test-azure-mcp-update.yml` updated or replaced with .NET equivalent | File references `mcp-cli-metadata/` not `test-npm-azure-mcp/` |
| AC8 | All version snapshot JSON files preserved in `mcp-cli-metadata/` (git history retained) | `git log --follow --oneline mcp-cli-metadata/ \| wc -l` shows commits spanning the rename boundary |
| AC9 | All Node.js test files migrated to xUnit with equivalent coverage | `dotnet test mcp-doc-generation.sln` includes new `McpCliMetadata.Tests` project; all tests pass |
| AC10 | `generate-cli-examples` logic produces same output as before | Existing fixture-based test assertions pass with new implementation |
| AC11 | No references to `test-npm-azure-mcp` remain in non-historical files | `grep -r "test-npm-azure-mcp" --include="*.md" --include="*.yml" --include="*.ps1" --include="*.sh" --include="*.cs" .` returns 0 matches (excluding git log and this PRD) |
| AC12 | `dotnet build mcp-doc-generation.sln --configuration Release` succeeds with 0 warnings | CI `build-and-test` job passes |
| AC13 | CHANGELOG.md updated with migration entry | `CHANGELOG.md` contains entry for issue #627 |
| AC14 | Schema validation test asserts JSON structure of all three CLI output files | A schema validation test in McpCliMetadata.Tests asserts the JSON structure of `cli-output.json`, `cli-namespace.json`, and `cli-version.json` matches the documented contract (field names, types, nesting) |

---

## 5. Implementation Plan

### Phase 1 — Research & Decision (Owner: nigel-prd-planning)

**Deliverable:** OQ1 resolved — confirm whether `azmcp` is published to NuGet as a .NET global tool or must be invoked as a compiled binary.

**Steps:**
1. Search NuGet.org for `azmcp` or `Microsoft.Azure.Mcp.Cli` or similar.
2. Check `microsoft/mcp` repo for NuGet packaging configuration.
3. If NuGet tool exists: document install command for Phase 2.
4. If binary-only: document the compiled binary path convention used by `BootstrapStep` and `preflight.ps1`.
5. Record resolution in OQ1 below.

**Acceptance check:** OQ1 closed with a concrete command (`dotnet tool install -g X` or binary invocation string).

> **PRD remains in Draft status until OQ1 is resolved. Self-score reflects structural completeness only, not dispatch readiness.**

---

### Phase 2 — .NET Metadata Extractor (Owner: aspira-eng-dotnet)

**Deliverable:** New C# project `mcp-tools/McpCliMetadata/McpCliMetadata.csproj` that:
- Wraps the `azmcp` binary (tool or compiled) to extract version, tools JSON, namespace JSON
- Writes to `generated/cli/` (same paths as before)
- Replaces `npm run get:version`, `npm run get:tools-json`, `npm run get:tools-namespace`
- Added to `mcp-doc-generation.sln`

**Steps:**
1. Create `mcp-tools/McpCliMetadata/` directory.
2. Create `McpCliMetadata.csproj` referencing .NET 9; add to solution.
3. Implement `AzmcpRunner.cs` — process-based invocation of `azmcp` with `--version`, `tools list`, `tools list --namespace-mode`.
4. Write `README.md` for the new project.
5. Implement `generate-cli-examples` equivalent as `CliExamplesGenerator.cs`.
6. Implement `validate-cli-output` equivalent as `CliOutputValidator.cs`.
7. Implement `diff-versions` equivalent as `VersionDiffer.cs`.

**Acceptance check:** `dotnet build mcp-doc-generation.sln --configuration Release` succeeds; new project compiles.

---

### Phase 3 — Tests (Owner: scooter-quality)

**Deliverable:** `mcp-tools/McpCliMetadata.Tests/McpCliMetadata.Tests.csproj` with xUnit tests that cover the same cases as `test/*.test.js`:

- `CliExamplesGeneratorTests.cs` — equivalent of `generate-cli-examples.test.js` (3 test cases with fixture data)
- `CliOutputValidatorTests.cs` — equivalent of `validate-cli-output.js` tests
- `VersionDifferTests.cs` — unit tests for diff logic
- `VersionDifferTests.cs` includes test cases for pre-release version strings (e.g., 3.0.0-rc1 → 3.0.0, 3.0.0-beta.2 → 3.0.0-beta.3)

**Acceptance check:** `dotnet test mcp-doc-generation.sln --no-build --configuration Release` passes all new tests.

---

### Phase 4 — Folder Rename & Reference Fixes (Owner: aspira-eng-dotnet)

**Deliverable:** `test-npm-azure-mcp/` renamed to `mcp-cli-metadata/` via `git mv`; all references updated.

**Pre-condition:** Verify McpCliMetadata.Tests pass before removing Node.js test files (Phase 3 must be complete).

**Steps:**
1. `git mv test-npm-azure-mcp mcp-cli-metadata` (preserves history).
2. Remove `package.json`, `package-lock.json`, `node_modules/`, Node.js test files from the renamed folder (version snapshot JSON dirs and `.env`/`samples.env` are kept).
3. Add new C# project files into `mcp-cli-metadata/` or keep them in `mcp-tools/McpCliMetadata/` (separate from snapshots).
4. Update all references — see Dim 7 (Related Files) for full list.

**Acceptance check:** AC1, AC8, AC11 pass.

---

### Phase 5 — preflight.ps1 & Script Updates (Owner: aspira-eng-dotnet)

**Deliverable:** `preflight.ps1` Step 4 rewritten to call the new .NET extractor instead of `npm`.

**Steps:**
1. Replace `Push-Location $testNpmDir` / `npm install` / `npm run` block (lines 110–130) with a `dotnet run` call to `McpCliMetadata`.
2. Update `$testNpmDir` variable reference to `$mcpCliMetadataDir`.
3. Update `start.sh` if it has any direct `test-npm-azure-mcp` references.

**Acceptance check:** AC3 passes; `preflight.ps1` produces same three output files.

---

### Phase 6 — CI Workflow Updates (Owner: aspira-eng-dotnet)

**Deliverable:** All four affected workflows updated.

| Workflow | Change |
|----------|--------|
| `build-and-test.yml` | Path trigger `test-npm-azure-mcp/**` → `mcp-cli-metadata/**` |
| `update-azure-mcp.yml` | Rewrite to track NuGet/.NET tool version; remove Node.js setup; update `working-directory` to `mcp-cli-metadata/`; replace `npm install`, `npm ls`, `npm view`, `npx azmcp` with .NET equivalents |
| `test-azure-mcp-update.yml` | Update `working-directory` and path triggers; replace `npm ci` / `npx azmcp` with .NET equivalents |
| `generate-docs.yml` | Update any references to `test-npm-azure-mcp` |

**Acceptance check:** AC5, AC6, AC7 pass; CI green on PR.

---

### Phase 7 — Documentation Updates (Owner: aspira-eng-dotnet)

**Deliverable:** All doc files updated; CHANGELOG entry added.

Files to update: `docs/ARCHITECTURE.md`, `docs/ci-integration.md`, `docs/VALIDATION-RUNBOOK.md`, `docs/package-rationalization-plan.md`, `mcp-tools/validation/README.md`, `.copilot/skills/azure-ai-tools-mcp-coverage-audit/SKILL.md`, `README.md`, `3X-GENERATION-REPORT.md`, `CHANGELOG.md`.

**Acceptance check:** AC11, AC13 pass.

---

## 6. Owner Assignment

| Phase | Owner | Deliverable |
|-------|-------|-------------|
| Phase 1 — Research | nigel-prd-planning | OQ1 resolved |
| Phase 2 — .NET Extractor | aspira-eng-dotnet | `McpCliMetadata` C# project |
| Phase 3 — Tests | scooter-quality | xUnit test project |
| Phase 4 — Rename & Refs | aspira-eng-dotnet | `git mv` + reference cleanup |
| Phase 5 — Scripts | aspira-eng-dotnet | `preflight.ps1` updated |
| Phase 6 — CI | aspira-eng-dotnet | 4 workflows updated |
| Phase 7 — Docs | aspira-eng-dotnet | All docs + CHANGELOG |

**Post-Ship Owner:** aspira-eng-dotnet (ongoing version tracking health)

---

## 7. Related Files and Issues

| Resource | Relevance |
|----------|-----------|
| `test-npm-azure-mcp/package.json` | Primary artifact being replaced |
| `test-npm-azure-mcp/README.md` | Documents current NPM integration |
| `test-npm-azure-mcp/test/*.test.js` | Tests to migrate to xUnit |
| `mcp-tools/scripts/preflight.ps1` | Step 4 calls npm scripts — must be updated |
| `mcp-tools/DocGeneration.PipelineRunner/Steps/Bootstrap/BootstrapStep.cs` | Consumes CLI output; may have path config |
| `mcp-tools/DocGeneration.PipelineRunner.Tests/Unit/BootstrapStepTests.cs` | References `test-npm-azure-mcp` — update path constants |
| `.github/workflows/update-azure-mcp.yml` | Nightly version tracker — full rewrite |
| `.github/workflows/test-azure-mcp-update.yml` | PR-triggered test — update paths |
| `.github/workflows/build-and-test.yml` | Path trigger — update folder name |
| `docs/ARCHITECTURE.md` | Architecture doc references folder |
| `docs/ci-integration.md` | CI doc references folder |
| `docs/VALIDATION-RUNBOOK.md` | Runbook references folder |
| `docs/package-rationalization-plan.md` | Describes npm usage — needs update |
| `mcp-tools/validation/Scan-McpToolCoverage.ps1` | References folder |
| `mcp-tools/validation/README.md` | References folder |
| `.copilot/skills/azure-ai-tools-mcp-coverage-audit/SKILL.md` | References folder |
| `mcp-tools/DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests/pretest.sh` | References folder |
| `README.md` | Top-level readme references folder |
| `CHANGELOG.md` | Must record this migration |
| `3X-GENERATION-REPORT.md` | References folder |
| `diberry/microsoft-mcp-doc-generation#627` | Parent issue |

---

## 8. Open Questions

| # | Question | Decision / Resolution Date |
|---|----------|---------------------------|
| OQ1 | Is `azmcp` published as a .NET global tool on NuGet.org? If yes, what is the package name and install command? If no, we invoke the compiled binary from the Docker `mcp-builder` stage or a local build. | Open — resolve by 2026-05-30. Research: search NuGet for `azmcp` and check `microsoft/mcp` packaging config. |
| OQ2 | Should `generate-cli-examples`, `validate-cli-output`, `diff-versions`, and `generate-report` be migrated to C# console apps (added to solution) or to PowerShell scripts? | Open — resolve by 2026-05-30. Decision depends on OQ1 and team preference. Recommended: C# for testability and solution coherence; PowerShell only if the logic is <30 lines of JSON manipulation. |
| OQ3 | Should version snapshot directories (e.g., `2.0.0-beta.31+.../tools-list.json`) be preserved in the renamed `mcp-cli-metadata/` folder or moved to a separate `mcp-cli-metadata/snapshots/` subdirectory for clarity? | Open — resolve by 2026-05-30. Recommendation: keep at folder root for `git mv` simplicity; add `snapshots/` only if directory listing becomes unwieldy. |

---

## 9. Done-When Criteria

This PRD is done when **all** of the following are verifiable from the repository root:

1. `git ls-files test-npm-azure-mcp/` returns **empty** (folder does not exist).
2. `git ls-files mcp-cli-metadata/` returns **non-empty** (renamed folder exists with snapshot JSON files).
3. `grep -r "test-npm-azure-mcp" --include="*.yml" --include="*.ps1" --include="*.sh" --include="*.cs" --include="*.md" . | grep -v "^./docs/prds/prd-627"` returns **0 matches**.
4. `dotnet build mcp-doc-generation.sln --configuration Release` exits **0** with no warnings.
5. `dotnet test mcp-doc-generation.sln --no-build --configuration Release` exits **0** — all tests pass including new `McpCliMetadata.Tests`.
6. GitHub Actions `build-and-test` CI is **green** on the implementation PR.
7. `generated/cli/cli-output.json`, `cli-namespace.json`, and `cli-version.json` are produced by the new .NET extractor in a local `preflight.ps1` run.
8. Issue #627 is referenced in a merged PR.

---

## 10. Scope — In Scope

- Rename `test-npm-azure-mcp/` → `mcp-cli-metadata/` via `git mv`
- Create new C# project `McpCliMetadata` to replace Node.js metadata extraction scripts
- Create new xUnit test project `McpCliMetadata.Tests` to replace Node.js test files
- Update `preflight.ps1` to call .NET instead of npm
- Update all four affected CI workflows (`build-and-test.yml`, `update-azure-mcp.yml`, `test-azure-mcp-update.yml`, `generate-docs.yml`)
- Update all documentation files that reference `test-npm-azure-mcp`
- Update `BootstrapStepTests.cs` path constants
- Update `Scan-McpToolCoverage.ps1`, `.copilot/skills/...`, `pretest.sh`
- Remove `package.json`, `package-lock.json`, `node_modules/`, Node.js test files from the renamed folder
- Add `CHANGELOG.md` entry

---

## 11. Scope — Out of Scope

- Changes to Steps 1–6 of the documentation pipeline
- Changes to `.NET` build/test configuration beyond adding the new project
- Changes to Docker build stages (the MCP binary is already compiled there)
- Changes to the Azure OpenAI/Foundry integration
- Changes to the skills-generation pipeline
- Changes to generated output format (same JSON schema must be preserved)
- Removing version snapshot JSON directories (historical data preserved)
- Any changes to `guardrails.md`, `loop.md`, or `.squad/agents/*/charter.md`
- Changes to the public mirror repo

---

## 12. Dependencies

- **OQ1 resolution** (Phase 1) must complete before Phase 2 begins — the implementation approach depends on whether `azmcp` is available as a .NET global tool.
- **Phase 2** (C# extractor) must complete before **Phase 3** (tests), **Phase 4** (rename), and **Phase 5** (script updates) — tests need the implementation to verify, rename needs the new files to exist.
- **Phase 3** (tests) MUST complete before **Phase 4** (rename) — Phase 4 Step 2 (Remove Node.js test files) is blocked until Phase 3 verifies all test behavior is replicated in xUnit. Phases 3/4/5 are NOT parallelizable: Phase 3 → Phase 4 is a hard dependency.
- **Phase 3** (tests) must complete before **Phase 6** (CI) — CI must run the new tests.
- **All phases** must complete before the PR is created — this is a single atomic PR.
- `dotnet build mcp-doc-generation.sln --configuration Release` must pass at the end of Phase 2 — zero warnings policy (AD-007).

---

## 13. Risks and Assumptions

| Risk / Assumption | Mitigation |
|-------------------|------------|
| `azmcp` is not published on NuGet — binary path may differ between CI and local dev | OQ1 resolves this. If binary-only: document the path convention in `McpCliMetadata/README.md` and make path configurable via env var (consistent with existing `$env:MCP_SERVER_PATH` pattern). |
| `git mv` may not preserve history perfectly under some Git configs | Use `git mv` explicitly; verify with `git log --follow mcp-cli-metadata/`. If history breaks, document in CHANGELOG but proceed. |
| Node.js test logic has edge cases not obvious from reading `*.test.js` | Run existing Node.js tests against fixtures first, capture all assertions, then replicate in xUnit. Tests must fail without the fix (TDD rule AD-007). |
| CI `update-azure-mcp.yml` rewrite may miss a version-check edge case (pre-release channel detection) | Port the pre-release channel detection logic (lines 46–62 of `update-azure-mcp.yml`) to .NET equivalent; add test coverage for pre-release version parsing. |
| Snapshot directories (version folders) will make `mcp-cli-metadata/` look cluttered | Accepted risk — tracked in OQ3. Keep at root for now; add `snapshots/` subdirectory in a follow-on if needed. |
| `samples.env` in the renamed folder may still reference npm-specific variables | Audit and update `samples.env` to reflect .NET-based credential requirements only. |

---

## 14. Verification Commands

| Check | Command / Observable | Expected Result |
|-------|----------------------|-----------------|
| Folder renamed (positive) | `git -C . ls-files mcp-cli-metadata/ \| head -5` | Lists files including snapshot JSON dirs |
| Old folder gone (negative) | `git -C . ls-files test-npm-azure-mcp/ \| wc -l` | `0` |
| No npm references in scripts (negative) | `grep -r "npm run\|npm install\|npx azmcp" mcp-tools/scripts/preflight.ps1` | No matches |
| No old folder refs in workflows (negative) | `grep -r "test-npm-azure-mcp" .github/workflows/` | No matches |
| Solution builds clean (positive) | `dotnet build mcp-doc-generation.sln --configuration Release 2>&1 \| tail -5` | `Build succeeded. 0 Warning(s). 0 Error(s).` |
| All tests pass (positive) | `dotnet test mcp-doc-generation.sln --no-build --configuration Release --verbosity quiet 2>&1 \| tail -3` | `Passed!` with 0 failed |
| CLI outputs produced (positive) | `ls generated/cli/cli-output.json generated/cli/cli-version.json generated/cli/cli-namespace.json` | All three files exist |
| No stray references (negative) | `grep -r "test-npm-azure-mcp" --include="*.yml" --include="*.ps1" --include="*.sh" --include="*.cs" . \| grep -v "prd-627"` | No matches |
| CHANGELOG updated (positive) | `grep "627\|mcp-cli-metadata\|NPM to .NET" CHANGELOG.md` | Match found |

---

## 15. Dispatch Instructions

- **Trigger:** Manual — Dina approves this PRD after 8-agent review; Aspira executes implementation.
- **Entry point:** Phase 1 research (OQ1 resolution) runs first; then implementation proceeds Phase 2 → 7.
- **Autonomous execution:** Yes — all phases are self-contained and can run without human confirmation, except: (a) OQ1 resolution requires web search/NuGet lookup; (b) stop if `dotnet build` fails with errors.
- **Output targets:**
  - New folder: `mcp-cli-metadata/` (renamed from `test-npm-azure-mcp/`)
  - New C# project: `mcp-tools/McpCliMetadata/McpCliMetadata.csproj`
  - New test project: `mcp-tools/McpCliMetadata.Tests/McpCliMetadata.Tests.csproj`
  - Modified: `mcp-tools/scripts/preflight.ps1`
  - Modified: `.github/workflows/build-and-test.yml`
  - Modified: `.github/workflows/update-azure-mcp.yml`
  - Modified: `.github/workflows/test-azure-mcp-update.yml`
  - Modified: `docs/ARCHITECTURE.md`, `docs/ci-integration.md`, `docs/VALIDATION-RUNBOOK.md`, `docs/package-rationalization-plan.md`
  - Modified: `mcp-tools/validation/Scan-McpToolCoverage.ps1`, `mcp-tools/validation/README.md`
  - Modified: `.copilot/skills/azure-ai-tools-mcp-coverage-audit/SKILL.md`
  - Modified: `mcp-tools/DocGeneration.PipelineRunner.Tests/Unit/BootstrapStepTests.cs`
  - Modified: `mcp-tools/DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests/pretest.sh`
  - Modified: `README.md`, `CHANGELOG.md`
- **Repos affected:** `diberry/microsoft-mcp-doc-generation` only (single repo).
- **Branch naming:** `squad/627-npm-to-dotnet-migration`
- **Standing constraints:** `.github/skills/dina-prd-intake/standing-constraints.md`
- **Stop conditions:** OQ1 unresolved blocks Phase 2. Build failure blocks Phase 3+. Do NOT merge — create PR and stop.

---

## 16. Scope Fence

### In Scope
- `test-npm-azure-mcp/` rename and Node.js removal
- New `mcp-tools/McpCliMetadata/` C# project
- New `mcp-tools/McpCliMetadata.Tests/` xUnit project
- `preflight.ps1` npm → .NET migration
- All four CI workflows updated
- All 12+ documentation files that reference `test-npm-azure-mcp`
- `BootstrapStepTests.cs` path constant updates

### Out of Scope
- Pipeline Steps 1–6 code
- Generated output format changes
- Docker build changes
- Skills-generation pipeline
- Any file not in the reference list (Dim 7)

### Must Not Change

> The following files must not be modified by this PRD's implementation: `guardrails.md`, all `.squad/agents/*/charter.md` files, `loop.md`, `ambient.config.json`. The docs/prds directory structure must not be altered. The version snapshot JSON directories inside `mcp-cli-metadata/` (formerly `test-npm-azure-mcp/`) must not be deleted — only renamed with their parent.

```yaml
must-not-change:
  - "guardrails.md"
  - ".squad/agents/*/charter.md"
  - "loop.md"
  - "ambient.config.json"
  - "mcp-cli-metadata/*/tools-list.json"  # version snapshot data must be preserved
  - "mcp-cli-metadata/samples.env"         # template only — preserve for .env setup docs
  - "mcp-doc-generation.sln"               # solution file — only ADD new projects, never delete existing ones
```

---

## 17. Trigger Documentation

**Option B — One-time change:**
- **Change Type:** `one-time`
- **Cleanup Criteria:** PR #627-impl is merged, `test-npm-azure-mcp/` no longer exists in the repo, `mcp-cli-metadata/` is the new canonical folder, CI is green, and issue #627 is closed by Dina. No recurring trigger needed after migration — the ongoing nightly workflow is self-contained within `update-azure-mcp.yml`.

---

## 18. Environment Integration

### Integration Points

| Target File / System | Change Type | Purpose |
|---------------------|-------------|---------|
| `mcp-tools/scripts/preflight.ps1` | Modify | Bootstrap script invokes new .NET extractor instead of npm |
| `.github/workflows/build-and-test.yml` | Modify | Path trigger updated to `mcp-cli-metadata/**` |
| `.github/workflows/update-azure-mcp.yml` | Modify | Nightly version tracker rewritten for .NET/NuGet |
| `.github/workflows/test-azure-mcp-update.yml` | Modify | PR trigger path updated |
| `mcp-doc-generation.sln` | Modify (add) | Two new projects added to solution for CI build/test inclusion |
| `docs/ARCHITECTURE.md` | Modify | Architecture diagram/text updated |
| `README.md` | Modify | Setup instructions updated (no longer requires `npm install`) |
| `mcp-tools/validation/Scan-McpToolCoverage.ps1` | Modify | Path reference updated |
| `.copilot/skills/azure-ai-tools-mcp-coverage-audit/SKILL.md` | Modify | Path reference updated |

### Consumer Identification

- **Who/what reads the output?** `DocGeneration.PipelineRunner/Steps/Bootstrap/BootstrapStep.cs` reads `generated/cli/cli-output.json`, `cli-namespace.json`, `cli-version.json`. All 6 subsequent pipeline steps consume these files.
- **How do they discover it?** `PipelineContext.OutputPath` + hardcoded relative paths `cli/cli-output.json` etc. No change to consumer code needed — only the producer changes.
- **Who reads the version snapshot data?** `generate-cli-examples.js` (being replaced), diff scripts, the `update-azure-mcp.yml` workflow, and the `azure-ai-tools-mcp-coverage-audit` skill.

### Discovery Path

- **How does a new contributor find the metadata extractor?** `README.md` Setup section → references `mcp-cli-metadata/` → `mcp-cli-metadata/README.md` explains purpose and usage.
- **Routing table entry needed?** No — `mcp-cli-metadata/` is discovered by path reference in `preflight.ps1` and `start.sh`. No routing table change needed.

### Wiring Checklist

- [ ] `mcp-doc-generation.sln` includes `McpCliMetadata` and `McpCliMetadata.Tests` projects
- [ ] `build-and-test.yml` path trigger includes `mcp-cli-metadata/**`
- [ ] `preflight.ps1` produces the three `cli/*.json` output files via new .NET code path
- [ ] `update-azure-mcp.yml` nightly run tracks correct version source (NuGet or compiled binary)
- [ ] `README.md` setup section no longer instructs users to `npm install` in the metadata folder
- [ ] All consumers (BootstrapStep, coverage audit skill) can discover and read CLI outputs post-migration

---

## 19. Non-Functional Requirements

- **Zero warnings policy:** `dotnet build mcp-doc-generation.sln --configuration Release` must produce 0 compiler warnings (AD-007).
- **Test coverage:** New `McpCliMetadata.Tests` project must achieve ≥80% line coverage (consistent with skills-generation threshold).
- **Cross-platform:** `preflight.ps1` changes must work in both Windows (local dev) and Linux (CI Ubuntu). The .NET extractor must run on both platforms.
- **No Node.js dependency after migration:** After this PR merges, `Node.js` setup in CI should only be required by workflows explicitly requiring it (none in the main pipeline path).

---

## Intake Answers

| Q | Answer |
|---|--------|
| Q1 — Observable outcome | `test-npm-azure-mcp/` no longer exists. `mcp-cli-metadata/` exists with .NET-based extraction tooling. `preflight.ps1` produces `cli-output.json`, `cli-namespace.json`, `cli-version.json` without any npm calls. All CI workflows reference the new folder. |
| Q2 — Verification | `grep -r "test-npm-azure-mcp" --include="*.yml" --include="*.ps1" --include="*.cs" .` → 0 results. `dotnet test mcp-doc-generation.sln` → passes. `git ls-files test-npm-azure-mcp/ \| wc -l` → 0. |
| Q3 — Trigger | Manual — Dina approves PRD; Aspira implements. No schedule needed. |
| Q4 — Scope boundary | In: rename folder, migrate Node.js scripts to C#, update all references. Out: pipeline Steps 1–6, generated output format, Docker changes. Must not change: guardrails.md, charter files, loop.md, ambient.config.json. |
| Q5 — Output targets | New folder `mcp-cli-metadata/`, new C# projects in `mcp-tools/`, modified `preflight.ps1`, 4 CI workflows, 12+ doc files, `CHANGELOG.md`. Single repo: `diberry/microsoft-mcp-doc-generation`. |
| Q6 — Repeatability | No — one-time migration. No new skill needed. The updated workflows are the ongoing operational artifacts. |
| Q7 — Tier | **Complex** — >5 files, 1 repo, new C# projects (new external dependency structure), CI workflow rewrites, TDD required. |
| Q8 — Multi-repo | Single repo: `diberry/microsoft-mcp-doc-generation`. |
| Q9 — Credentials | No new credentials needed. Uses existing `gh` auth, `GH_TOKEN`, and the same Azure credentials already in `.env`. If `azmcp` becomes a NuGet tool, no extra auth needed for `dotnet tool install`. |
| Q10 — Environment Integration | `mcp-doc-generation.sln` gets two new projects. `preflight.ps1` consumer path changes. `build-and-test.yml` path trigger updated. `README.md` setup section updated. No repos.json change (same repo). |

---

## Self-Scoring: Completeness Scorecard

| Dim | Name | Score | Notes |
|-----|------|-------|-------|
| 1 | Problem Statement | ✅ 2/2 | Root cause identified (legacy npm origin, no migration trigger); user/pipeline impact articulated |
| 2 | Root Cause Analysis | ✅ 2/2 | Three distinct causes with structural gaps |
| 3 | Proposed Solution | ✅ 2/2 | Concrete mechanism (C# project, binary invocation, git mv); decision point captured as OQ1 |
| 4 | Acceptance Criteria | ✅ 2/2 | 13 measurable ACs, each with command-verifiable check |
| 5 | Implementation Plan | ✅ 2/2 | 7 phases, each with owner, deliverable, and acceptance check |
| 6 | Owner Assignment | ✅ 2/2 | Phase-level owner table with post-ship owner |
| 7 | Related Files/Issues | ✅ 2/2 | 20+ resources with relevance column; parent issue #627 linked |
| 8 | Open Questions | ✅ 2/2 | 3 OQs with resolution dates and recommendation; PRD stays Draft until OQs closed |
| 9 | Done-When Criteria | ✅ 2/2 | 8 verifiable conditions; all machine-checkable |
| 10 | Scope (in) | ✅ 2/2 | Explicit enumeration of all in-scope work |
| 11 | Scope (out) | ✅ 2/2 | Explicit exclusions prevent scope creep |
| 12 | Dependencies | ✅ 2/2 | OQ1 → Phase 2 sequencing; Phase 2 → Phases 3/4/5 ordering explicit |
| 13 | Risk/Assumptions | ✅ 2/2 | 5 risks with mitigations; pre-release channel risk called out |
| 14 | Verification Commands | ✅ 2/2 | 9 runnable commands (positive + negative); all from repo root |
| 15 | Dispatch Instructions | ✅ 2/2 | Trigger, entry point, output targets, repos, stop conditions, branch name |
| 16 | Scope Fence | ✅ 2/2 | Three sub-sections; YAML must-not-change block present |
| 17 | Trigger Documentation | ✅ 2/2 | One-time change type; cleanup criteria defined |
| 18 | Environment Integration | ✅ 2/2 | Integration points table, consumer identification, discovery path, wiring checklist |
| **Total** | | **36/36** ✅ | Structural completeness only. OQ1 must close before dispatch — PRD status is Draft — Blocked on OQ1. |

**Score guide:**
- **31–36** ✅ Ready for autonomous dispatch (structural score only — OQ1 must be resolved before this PRD is dispatch-ready)
- **22–30** ⚠️ Needs refinement — fill gaps before dispatch
- **0–21** ❌ Not ready — rework required
