# Squad Team

> Azure MCP Documentation Generator — 800+ markdown docs across 52 Azure namespaces

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. Does not generate domain artifacts. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Avery | Team Lead | `.squad/agents/avery/charter.md` | ✅ Active |
| Riley | Architect | `.squad/agents/riley/charter.md` | ✅ Active |
| Morgan | C# Generator Developer | `.squad/agents/morgan/charter.md` | ✅ Active |
| Quinn | DevOps / Scripts Engineer | `.squad/agents/quinn/charter.md` | ✅ Active |
| Sage | AI / Prompt Engineer | `.squad/agents/sage/charter.md` | ✅ Active |
| Cameron | Test Lead | `.squad/agents/cameron/charter.md` | ✅ Active |
| Parker | QA / Tester | `.squad/agents/parker/charter.md` | ✅ Active |
| Reeve | Documentation Engineer | `.squad/agents/reeve/charter.md` | ✅ Active |
| Ellis | Evaluation Reviewer (nondeterministic) | `.squad/agents/ellis/charter.md` | ✅ Active |
| Scribe | Session Logger | `.squad/agents/scribe/charter.md` | 📋 Silent |

> **Standing roster, not a ceiling.** These ten are the permanent team. The Coordinator may
> hire additional specialists **at-will** — as disposable *guests* for a single task/review, or
> as *standing* members for recurring needs — when the roster lacks required expertise. Hires are
> tracked in `.squad/casting-registry.json`; see "Hiring New Agents" in `.github/agents/squad.agent.md`.
> **Scribe ≠ Reeve:** Scribe keeps team memory (decisions, history, wisdom); Reeve writes product docs.

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
