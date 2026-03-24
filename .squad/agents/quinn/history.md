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
- Common params (7 total) are defined in `docs-generation/data/common-parameters.json`
- `node:test` + `node:assert/strict` (built-in to Node 22) works well for zero-dep test suites in this project
- Tool `option[]` array uses `required: true` flag (not `isRequired`) — different from `common-parameters.json` which uses `isRequired`
