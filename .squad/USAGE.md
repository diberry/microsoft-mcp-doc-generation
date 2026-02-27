# How to Use SQUAD with This Project

SQUAD is a framework for working with a pre-configured team of specialist AI agents in **VS Code Copilot Chat**. Each agent knows this codebase deeply and stays in its lane, so you get focused, high-quality help without re-explaining context every session.

---

## Quick Start (Every Session)

Open **VS Code Copilot Chat** and type:

```
@squad Set up the team
```

Squad will summarize recent decisions, confirm the team roster, and ask what you want to work on. From there, it routes your request to the right specialist.

---

## The Team

| Name | Role | Ask when you need... |
|------|------|----------------------|
| **Avery** | Lead / Architect | New feature design, cross-project decisions, data file changes, PR reviews |
| **Morgan** | C# Generator Dev | Generator code changes, `.cs` files, Handlebars templates, .NET pipeline |
| **Quinn** | DevOps / Scripts | PowerShell/Bash scripts, GitHub Actions, Docker changes |
| **Sage** | AI Engineer | Azure OpenAI prompts, retry logic, new generative AI features |
| **Parker** | QA / Tester | Writing tests, fixing CI failures, adding regression coverage |
| **Reeve** | Scribe | Logging decisions, updating READMEs, writing documentation |

---

## Common Workflows

### Fix a bug in the generator
```
@squad Morgan: the parameter table is sorting incorrectly — required params should come first.
```
Morgan fixes the C# code; Parker adds a regression test.

### Add a new AI-powered feature
```
@squad Avery, Sage: I want to generate H2 headings for each tool using Azure OpenAI.
```
Avery designs the approach, Sage writes the prompts and AI integration.

### Change a generation script
```
@squad Quinn: start.sh is failing on Windows Git Bash — path translation issue.
```

### Log an architectural decision
```
@squad Reeve: log that we decided to use sequential (not parallel) API calls for example prompts to avoid rate limits.
```

### New session — catch up on project state
```
@squad What decisions have been made recently? What should I know before I start working today?
```

---

## Two Ways to Use SQUAD

### 1. VS Code Copilot Chat (interactive, local)
- Opens `.github/agents/squad.agent.md` automatically
- Use `@squad` to address the coordinator directly
- Squad coordinates the team and spawns specialists
- Best for: new features, design discussions, multi-step work

### 2. GitHub Browser — Create an Issue, Assign to Copilot (automated)

**Yes, Squad knowledge works here too.** When you file a GitHub issue and assign it to Copilot, the automated coding agent reads all the `.squad/` files before starting work:

- `.squad/decisions.md` — architectural decisions the agent respects
- `.squad/routing.md` — tells the agent which domain each file type belongs to
- `.squad/agents/*/charter.md` and `history.md` — specialist knowledge for the relevant domain

**What the agent CANNOT access** from the browser: `.github/agents/squad.agent.md` (security restriction — the coding agent cannot read its own instruction files). The coordinator prompt is VS Code only.

**What this means in practice:**

| Scenario | What to do |
|----------|------------|
| Design discussion, architecture question | Use VS Code Copilot Chat with `@squad` |
| Implement a feature / fix a bug | Create a GitHub issue, assign to Copilot — agent uses `.squad/` knowledge automatically |
| Both | Discuss in VS Code first, then file the issue for automated implementation |

Both modes benefit from the SQUAD files — `.squad/` is accessible to both.

---

## Maintaining the Team Knowledge

The team stays effective when its shared knowledge stays current.

### After making architectural decisions
Ask Reeve to log them:
```
@squad Reeve: add a decision — we now default to generated-<namespace>/ output directories for single-namespace runs.
```
Decisions live in `.squad/decisions.md` and are read at the start of every session.

### After a major feature ships
Ask Reeve to update the relevant agent's `history.md`:
```
@squad Reeve: update Morgan's history to include the new CompleteToolGenerator and how it works.
```
History files are in `.squad/agents/{name}/history.md`.

### When project conventions change
Ask Reeve to update the agent charters or `copilot-instructions.md`:
```
@squad Reeve: update Quinn's charter to reflect that all scripts now live in docs-generation/scripts/.
```

---

## Key Rules the Team Knows

These are already baked into every agent — you don't need to repeat them:

- **Never edit generated files** in `generated/` or `generated-*/` — fix the generators
- **Zero warnings** — the CI build treats warnings as errors (`--configuration Release`)
- **Every bug fix needs a test** — Parker adds regression coverage automatically
- **Central Package Management** — package versions go in `Directory.Packages.props`, not `.csproj`
- **New .NET projects** must be added to `docs-generation.sln` and include a `README.md`
- **Universal design** — all logic must work for all 52 namespaces, no service-specific hardcoding
- **Minimal console output** — verbose output goes to log files via `LogFileHelper`

---

## File Map

```
.github/agents/
  squad.agent.md          ← Squad coordinator (VS Code Copilot Chat only)

.squad/
  USAGE.md                ← This file
  team.md                 ← Full team roster + parallel spawning patterns
  routing.md              ← Which agent handles which files/tasks
  decisions.md            ← Shared architectural decisions log (append only)
  casting/
    policy.json           ← Spawning policy
    registry.json         ← Agent registry
  agents/
    avery/
      charter.md          ← Avery's identity, expertise, and boundaries
      history.md          ← Avery's project-specific knowledge
    morgan/               ← (same structure for all 6 agents)
    quinn/
    sage/
    parker/
    reeve/
```

---

## Adding a New Agent

If the project grows into a new domain (e.g., a dedicated localization engineer):

1. Create `.squad/agents/{name}/charter.md` (identity + expertise + boundaries)
2. Create `.squad/agents/{name}/history.md` (project-specific knowledge)
3. Add the agent to `.squad/team.md` roster
4. Add routing rules to `.squad/routing.md`
5. Ask Reeve to log the decision: `AD-016: Added {name} agent for {domain}`
