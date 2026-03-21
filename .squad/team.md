# Squad Team

> Azure MCP Documentation Generator — 800+ markdown docs across 52 Azure namespaces

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. Does not generate domain artifacts. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Avery | Lead / Architect | `.squad/agents/avery/charter.md` | ✅ Active |
| Morgan | C# Generator Developer | `.squad/agents/morgan/charter.md` | ✅ Active |
| Quinn | DevOps / Scripts Engineer | `.squad/agents/quinn/charter.md` | ✅ Active |
| Sage | AI / Prompt Engineer | `.squad/agents/sage/charter.md` | ✅ Active |
| Parker | QA / Tester | `.squad/agents/parker/charter.md` | ✅ Active |
| Reeve | Documentation Engineer | `.squad/agents/reeve/charter.md` | ✅ Active |
| Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |

## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps in generators
- Adding missing test coverage for content validators
- Configuration file updates (brand mappings, compound words)
- Template fixes with clear expected output

**🟡 Needs review — route to @copilot but flag for squad member PR review:**
- New generator features following established patterns
- Prompt modifications with clear quality criteria
- Script improvements with defined behavior change

**🔴 Not suitable — route to squad member instead:**
- Pipeline architecture changes (cross-stage data flow)
- New AI prompt design (fabrication risk)
- Quality standard definitions across 52 namespaces
- Security-critical changes (API keys, credentials)

## Project Context

- **Owner:** diberry
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Description:** Automated pipeline generating 800+ markdown documentation files for Microsoft Azure MCP server tools across 52 namespaces
- **Created:** 2026-03-20
