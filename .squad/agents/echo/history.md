# Echo — History

## Project context (seeded at onboarding — 2026-07-27)

- **Project:** squad-azure-skills — a squad built around Echo, the SME for keeping Azure MCP Server documentation in sync with the product's releases.
- **Owner:** diberry
- **Echo's role:** Founding member and SME. Operates the content-generation lifecycle end to end (research, skills, writing, editing); does not build the generation machinery.
- **Universe:** Star Wars (Echo = clone trooper).
- **Stack:** PowerShell 7 + Bash (every script ships both, cross-platform), `azure.mcp` .NET global tool for CLI shape/version, Azure DevOps (`az boards`) for release tracking, GitHub CLI for PRs, Teams MCP for hand-offs.
- **Generation engine (owned separately):** [diberry/microsoft-mcp-doc-generation](https://github.com/diberry/microsoft-mcp-doc-generation).

## Key facts to remember

- **The pipeline is strict-sequential:** release detection → metadata generation (hard user-merge gate) → content impact. Never skipped, never reordered.
- **Destination contract:** CLI metadata → `repos/public-diberry-microsoft-mcp-doc-generation/mcp-cli-metadata/{version}+{sha}/`, exactly 4 files, no binaries/scratch. `{sha}` from `azmcp --version`.
- **ADO:** `msft-skilling` / `Content`, one `User Story` per version. Agents CANNOT write ADO comments via REST (403); only `az boards work-item update --fields` works, single-line ASCII-only values. Step 3 writes verdict/trace to both `System.Description` and `Microsoft.VSTS.Common.AcceptanceCriteria`. Reference: #599233.
- **Betas are first-class** — docs maintained against pre-release betas (~Tue/Thu), not GA.
- **My skills:** `echo-release-detection` (Step 1, ADO-native), `echo-metadata-generation` (Step 2, no ADO), `echo-content-impact` (Step 3, ADO-native), `echo-finn-approved-pr-codeowners` (off-pipeline, no ADO).

## Gates I stop at

- **Release scope** — owner approves what's in scope.
- **Metadata merge** — owner merges the metadata PR before any content work begins.

## Session log

- **2026-07-27** — Onboarded as founding SME. Charter seeded from `echo-sme-prompt.md`. Universe cast as Star Wars. GitHub `squad` and `squad:echo` labels created for issue routing.
