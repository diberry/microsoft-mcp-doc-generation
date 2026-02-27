# Team Roster — Azure MCP Documentation Generator

## Active Team

| Name | Role | Specialty | Charter |
|------|------|-----------|---------|
| **Avery** | Lead / Architect | System architecture, cross-cutting decisions, PR reviews | [charter](.squad/agents/avery/charter.md) |
| **Morgan** | C# Generator Developer | .NET 9, Handlebars templates, generation pipeline | [charter](.squad/agents/morgan/charter.md) |
| **Quinn** | DevOps / Scripts Engineer | PowerShell, Bash, GitHub Actions, Docker | [charter](.squad/agents/quinn/charter.md) |
| **Sage** | AI / Prompt Engineer | Azure OpenAI, prompt engineering, generative AI | [charter](.squad/agents/sage/charter.md) |
| **Parker** | QA / Tester | xUnit tests, test strategy, CI validation | [charter](.squad/agents/parker/charter.md) |
| **Reeve** | Scribe | Decisions logging, documentation, README updates | [charter](.squad/agents/reeve/charter.md) |

## Team Purpose

This team maintains the **Azure MCP Documentation Generator** — an automated system that generates 800+ markdown documentation files for 52 Microsoft Azure MCP (Model Context Protocol) service namespaces.

## Key Project Facts

- **Output**: `./generated/` (full catalog) or `./generated-<namespace>/` (single namespace)
- **Generation entry point**: `./start.sh` (orchestrator) → `docs-generation/scripts/start-only.sh` (worker)
- **Build**: `dotnet build docs-generation.sln --configuration Release`
- **Test**: `dotnet test docs-generation.sln`
- **Critical rule**: NEVER edit files in `generated/` or `generated-*/` — fix the generators
- **AI credentials**: Required in `docs-generation/.env` for steps 2–5

## Parallel Work Capability

When a task involves multiple domains, spawn agents simultaneously:
- **Code + Tests**: Morgan (C#) + Parker (tests) in parallel
- **Scripts + Docs**: Quinn (scripts) + Reeve (docs) in parallel
- **Architecture + Prompts**: Avery + Sage in parallel when AI features are involved

## Communication

- Team-wide decisions → append to `.squad/decisions.md`
- Session summaries → written by Reeve
- Routing decisions → see `.squad/routing.md`
