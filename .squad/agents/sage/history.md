# Project Context

- **Owner:** diberry
- **Project:** Azure MCP Documentation Generator — automated pipeline producing 800+ markdown docs for 52 Azure MCP namespaces
- **Stack:** .NET 9, C#, Handlebars.Net, PowerShell 7, bash, Docker, Azure OpenAI
- **Created:** 2026-03-20

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-25: Acrolinx Compliance Research

**Task:** Researched how to make tool-family articles score 80+ on Acrolinx.

**Findings:**
- Analyzed 10 tool-family PRs in `MicrosoftDocs/azure-dev-docs-pr`. Current pass rate is 30% (3/10 above 80). Worst: Deploy at 61, Postgres at 64. Best: Cosmos DB at 92.
- Identified 20+ specific Acrolinx rule violations across 7 categories (Clarity, Grammar, Tone, Terminology, Consistency, Brand, Inclusive Language).
- The #1 score killer is **embedded JSON schemas in parameter descriptions** (Deploy article). The #2 is **long compound sentences** (40-60+ words). The #3 is **missing positive contractions** ("it is" instead of "it's").
- ContractionFixer (#145) only covers negative contractions. Extending to positive forms ("it is"→"it's", "you are"→"you're") is a quick P1 win.
- Adding an Acrolinx compliance section to the Step 4 system prompt (with sentence length, active voice, present tense rules) is the highest-impact single change.
- `static-text-replacement.json` only has 10 entries — needs Microsoft Entra ID, deprecated terms, ableist language removals.

**Output:** `docs/acrolinx-compliance-strategy.md` — comprehensive strategy with prioritized implementation plan (P0-P4), specific code examples, and estimated score improvements.
