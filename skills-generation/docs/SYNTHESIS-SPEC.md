# Azure Skills Content Synthesis Specification

> **Status:** v1.0 — Initial codification  
> **Last updated:** 2026-05-17  
> **Scope:** Governs how an upstream Azure skill `SKILL.md` is transformed into a customer-facing reference article for Microsoft Learn.  
> **Pipeline location:** `skills-generation/` within `diberry/microsoft-mcp-doc-generation`

---

## Table of Contents

1. [Source Format](#1-source-format)
2. [Extraction Rules](#2-extraction-rules)
3. [Enrichment & Overrides](#3-enrichment--overrides)
4. [Content Generation Rules](#4-content-generation-rules)
5. [Post-Processing](#5-post-processing)
6. [Validation Gates](#6-validation-gates)
7. [Known Conflicts & TODOs](#7-known-conflicts--todos)

---

## 1. Source Format

### 1.1 Upstream SKILL.md Structure

Each skill lives in the upstream repository at:

```
microsoft/azure-skills/skills/{skill-name}/SKILL.md
```

A SKILL.md file has two parts:

**YAML Frontmatter** (between `---` delimiters):

| Field | Required | Example |
|-------|----------|---------|
| `name` | Yes | `azure-compute` |
| `display_name` | No | `Azure Compute` |
| `description` | Yes | Free-text. May contain embedded delimiters (see §2.2) |
| `compatibility` | No | Comma-separated tool requirements |

**Markdown Body** — zero or more of these sections (identified by heading patterns):

| Section Heading Pattern | Maps To |
|------------------------|---------|
| `## Services` or `## Azure Services` | Services table |
| `## MCP Tools` or `## Tools` or `## MCP Server` | MCP tools table |
| `## Steps` or `## Workflow` or `## Suggested Workflow` | Workflow steps |
| `## Decision Guidance` or `## Decision` | Decision guidance tables |
| `## Related Skills` | Related skill cross-references |
| `## Prerequisites` | Prerequisites list |
| `## Required Inputs` | Prerequisites (merged with above) |
| `## Rules` | Prerequisite-like rules (filtered; see §2.4) |
| `## Quick Reference` | Key-value table (may contain MCP tool refs) |

> **Reference:** Parser at `SkillsGen.Core/Parsers/SkillMarkdownParser.cs`

### 1.2 Trigger Test Files

Each skill may have a companion test file at:

```
microsoft/azure-skills/skills/{skill-name}/triggers.test.ts
```

This TypeScript file contains arrays of:
- **`shouldTrigger`** — prompts that should activate the skill (used as example prompts)
- **`shouldNotTrigger`** — prompts that should NOT activate (used for validation only, never shown to customers)

> **Reference:** `SkillsGen.Core/Models/TriggerData.cs:3-6`

### 1.3 Sub-Skills

A skill may contain sub-skill directories:

```
microsoft/azure-skills/skills/{skill-name}/{sub-skill-name}/SKILL.md
```

Sub-skills are parsed with the same rules and rendered as `### {displayName}` sections under `## Sub-skills` in the output.

> **Reference:** `SkillsGen.Core/Models/SkillData.cs:16-25` (SubSkillData record)

### 1.4 Source SKILL.md Heading Inventory

> **Note:** There is no formal spec for upstream SKILL.md files in microsoft/azure-skills.
> This section documents the observed structure as extracted by the parser. Variations exist
> across skills — the parser handles multiple heading variants per concept.

| Source Section | Heading Variants | Frequency | Parser Reference |
|---|---|---|---|
| Use cases | `## Use for`, `## When to use` | Common (all skills) | `SkillMarkdownParser.cs:66-73` |
| Negative use cases | `## Do not use for`, `## Don't use for` | Common | `SkillMarkdownParser.cs:74-75` |
| Azure services | `## Services` | Common | `SkillMarkdownParser.cs:322-352` |
| MCP tools | `## MCP Tools`, `## Server`, `## Quick Reference` | Common | `SkillMarkdownParser.cs:355-381` |
| Prerequisites | `## Prerequisites`, `## Required Inputs`, `## Rules` | Common | `SkillMarkdownParser.cs:722-761` |
| RBAC roles | `## Required Roles`, `### RBAC`, `### Role Based Access` | Varies | `SkillMarkdownParser.cs:834-976` |
| Inline prereqs | Text patterns: `requires X`, `must have X`, `Docker required` | Varies | `SkillMarkdownParser.cs:785-831` |
| Related skills | `## Related Skills` + `@azure-*` links | Common | `SkillMarkdownParser.cs:677-706` |
| Workflow steps | `## Steps`, `## Workflows`, `## Workflow` | Optional | `SkillMarkdownParser.cs:583-600` |
| Decision guidance | `## Decision Guidance`, `## Decision`, `## Guidance` + `###` topics | Optional | `SkillMarkdownParser.cs:602-675` |
| Compatibility | `compatibility:` frontmatter field | Varies | `SkillMarkdownParser.cs:768-779` |

### 1.5 Source-to-Content Mapping

This section maps every source section to its destination in the output article and classifies it as customer-facing or implementation detail.

| Source Section | Output Destination | Classification | Pipeline Step |
|---|---|---|---|
| Use cases | `## When to use this skill` (bullets) | ✅ Customer-facing | Deterministic lift |
| Negative use cases | `### When not to use this skill` (sub-bullets) | ✅ Customer-facing | Deterministic lift |
| Azure services | `### Azure services knowledge` (under What it provides) | ✅ Customer-facing | Deterministic lift |
| MCP tools | **EXCLUDED** (per §4.2) | ❌ Implementation detail | Not rendered |
| Prerequisites (explicit) | `## Prerequisites` | ✅ Customer-facing | **Hybrid: deterministic extraction + LLM compression** |
| RBAC roles | `## Prerequisites` (RBAC sub-section) | ✅ Customer-facing | Deterministic lift |
| Inline prereqs | `## Prerequisites` | ✅ Customer-facing | **LLM synthesis** (scattered across source) |
| Related skills | `## Related skills` (conditional) | ✅ Customer-facing | Deterministic lift |
| Workflow steps | **EXCLUDED** (per §4.2) | ❌ Implementation detail | Not rendered |
| Decision guidance | `## Decision guidance` (conditional, Tier 1) | ⚠️ Conditional | Deterministic lift (when included) |
| Compatibility | Frontmatter metadata | ✅ Customer-facing | Deterministic lift |
| Description (H1 body) | Intro paragraph + `## What it provides` | ✅ Customer-facing | **LLM polish** (rewrite for customer voice) |

**Legend:**

- **Deterministic lift** — Content extracted by parser and placed by template with no LLM involvement
- **LLM polish** — Content extracted deterministically, then refined by LLM for clarity/style
- **LLM synthesis** — Content gathered from multiple scattered source sections, compressed by LLM into unified output
- **Hybrid** — Some content lifted deterministically (explicit prereqs, RBAC), some synthesized by LLM (inline mentions, cross-section prereq info)

### 1.6 Prerequisites Pipeline (Detail)

Prerequisites are the primary hybrid case where deterministic extraction and LLM synthesis work together. This detail documents the pipeline:

```
Source SKILL.md                    Pipeline                         Output Article
─────────────────                  ────────                         ──────────────
## Prerequisites  ──────┐
## Required Inputs ─────┤
## Rules (filtered) ────┤── Deterministic ──→ Structured prereqs
### RBAC / Roles ───────┤      extraction       (typed model)
compatibility: ─────────┘
                                     │
Inline text mentions ───── LLM ──────┤──→ ## Prerequisites
  ("requires X",           synthesis  │     (unified section
   "must have X",                     │      with install links)
   "Docker required")                 │
                                     │
                        Curated JSON ─┘
                     (skill-prereqs.json
                      overrides, if any)
```

The deterministic extraction produces a typed `SkillPrerequisites` model (Azure auth, subscription, RBAC roles, tools, resources, environment). The LLM synthesizes scattered inline mentions into this same model. Curated JSON overrides take priority when present.

> **Reference:** `SkillsGen.Core/Models/SkillPrerequisites.cs`, `SkillMarkdownParser.cs:722-831`

---

## 2. Extraction Rules

### 2.1 Frontmatter Parsing

Frontmatter is split from body using the `---` delimiter regex:

```
^---\s*\n(.*?)\n---
```

> **Reference:** `SkillMarkdownParser.cs:112-121` (SplitFrontmatter)

Field extraction rules:

| Field | Regex Pattern | Post-processing |
|-------|---------------|-----------------|
| `name` | `^name\s*:\s*(.+)$` | Normalized to lowercase (`ToLowerInvariant()`) |
| `display_name` | `^display_?name\s*:\s*(.+)$` | Surrounding quotes stripped; YAML escapes unescaped |
| `description` | `^description\s*:\s*(.+)$` | Double HTML-entity decoded; NOT truncated |
| `compatibility` | `^compatibility\s*:\s*(.+)$` | Split on commas |

> **Reference:** `SkillMarkdownParser.cs:123-148` (ExtractFrontmatterField)

### 2.2 Description Delimiter Extraction

The `description` field in frontmatter may contain embedded structured markers. These are extracted **in order** to prevent cross-contamination:

1. **Negative markers first** (prevents "USE FOR" inside "DO NOT USE FOR" from matching the positive pattern):
   - `DO NOT USE FOR:` → `DoNotUseFor` list
   - `DO NOT USE WHEN:` → merged into `DoNotUseFor`
   - `DON'T USE WHEN:` → merged into `DoNotUseFor`

2. **Positive markers second:**
   - `USE FOR:` (with negative lookbehind `(?<!NOT\s)`) → `UseFor` list
   - `WHEN:` → merged into `UseFor` (cleaned of quotes/punctuation)

Items appearing in both `DoNotUseFor` and `UseFor` are removed from `UseFor` (exact match, case-insensitive).

> **Reference:** `SkillMarkdownParser.cs:36-64`

### 2.3 List Parsing from Markers

After a marker is located, the following text is parsed as a list using `ParseCommaSeparatedOrBullets`:

1. Try bullet points (`- item` or `* item`) first
2. Try quoted strings (`"item1", "item2"`) second
3. Fall back to comma-separated values

Parsing stops at the next delimiter marker (`DO NOT USE`, `DON'T USE`, `WHEN:`, `USE FOR:`).

> **Reference:** `SkillMarkdownParser.cs:182-239`

### 2.4 Body Section Extraction

Each body section is extracted by matching heading patterns and capturing content until the next heading of equal or higher level.

| Data Model Field | Heading Pattern | Parse Strategy |
|-----------------|-----------------|----------------|
| `Services` | `##? (?:Azure\s+)?Services` | 4-column table (`Name \| UseWhen \| McpTools \| Cli`) or bold-bullet format |
| `McpTools` | `##? (?:MCP\s+)?(?:Tools\|Server)` | Dynamic-column table, bullet format (`\`tool\` with command \`cmd\` — desc`), code blocks, Quick Reference key-value |
| `WorkflowSteps` | `##? (?:Steps\|(?:Core\s+\|Suggested\s+)?Workflows?)` | Numbered list or bullet list |
| `DecisionGuidance` | `##? (?:Decision\s+Guidance\|Decision\|Guidance)` | Sub-headings as topics → option tables per topic |
| `RelatedSkills` | `##? Related\s+Skills` | `@azure-*` mentions and `[name](/skills/azure-*)` links |
| `Prerequisites` | `##? Prerequisites` | Bullet items |
| `Prerequisites` (merged) | `##? Required\s+Inputs` | Bullet items (deduplicated with above) |
| `Prerequisites` (filtered) | `##? Rules` | Only rules containing prerequisite keywords (`require`, `must`, `need`, `install`, etc.) and NOT starting with behavioral prefixes (`always`, `never`, `do not`, etc.) |

> **Reference:** `SkillMarkdownParser.cs:322-762`

### 2.5 Inline Prerequisite Detection

Beyond formal sections, the parser scans body text for:
- `requires X` / `must have X` / `X required` patterns
- App hosting patterns (e.g., "ASP.NET Core app hosted in Azure")
- Docker/container requirements

The formal `## Prerequisites` section is excluded to avoid double-counting.

> **Reference:** `SkillMarkdownParser.cs:785-831`

### 2.6 RBAC Role Extraction

Roles are extracted from `## Required Roles`, `### RBAC`, role tables, and inline role-name mentions.

> **Reference:** `SkillMarkdownParser.cs:838-850+`

### 2.7 Activation Trigger Extraction

Activation directives are parsed from:
- `MANDATORY` / `PREFER OVER` directives in the description
- Codebase detection markers (file patterns, config keys) from body sections

> **Reference:** `SkillMarkdownParser.cs:91` and `SkillData.cs:87`

### 2.8 Fallback Strategies

| Field | Primary Source | Fallback 1 | Fallback 2 |
|-------|---------------|------------|------------|
| `DisplayName` | `display_name` frontmatter | `skills-inventory.json` | Derived from slug (`DeriveDisplayName`) |
| `UseFor` | `USE FOR:` in description | `## When to use` body section | Prose use-case extraction from opening paragraphs |
| `DoNotUseFor` | `DO NOT USE FOR:` in description | `## Do not use` body section | Empty list |
| `ExamplePrompts` | Curated (`skill-example-prompts.json`) | `triggers.test.ts` shouldTrigger | Generated from UseFor/DetectionMarkers |
| `WhatItProvides` | LLM synthesis | Curated (`skill-what-it-provides.json`) | Mechanical build from services + tools |
| `Prerequisites` | `## Prerequisites` section | `## Required Inputs` + `## Rules` (filtered) | Inline prerequisite detection |

> **Reference:** `SkillPageGenerator.cs:41-97` (BuildContext), `SkillMarkdownParser.cs:67-75`

---

## 3. Enrichment & Overrides

### 3.1 Curated Data Overlay

Three JSON files provide human-curated overrides that take precedence over extracted data:

| File | Path | Content | Override Behavior |
|------|------|---------|-------------------|
| `skill-what-it-provides.json` | `data/skill-what-it-provides.json` | Skill name → prose paragraph | Replaces mechanical `BuildWhatItProvides()` output |
| `skill-example-prompts.json` | `data/skill-example-prompts.json` | Skill slug → array of prompt strings | Replaces trigger test data entirely (highest priority) |
| `skill-related-links.json` | `data/skill-related-links.json` | Skill name → array of `{title, url, category}` | Merged into Related Content section |

> **Reference:** `SkillsGen.Core/Data/CuratedDataLoader.cs:8`

### 3.2 Display Name Resolution

Priority order:
1. `skills-inventory.json` `displayName` field (canonical; 24 entries)
2. `display_name` from SKILL.md frontmatter
3. Derived from slug: `azure-compute` → `Azure Compute`

> **Reference:** `data/skills-inventory.json` and `SkillMarkdownParser.cs:23-24`

### 3.3 Skill Inventory

The canonical skill list is in `data/skills-inventory.json` (currently 24 skills). Each entry has:

```json
{ "name": "azure-compute", "displayName": "Azure Compute", "category": "Infrastructure and Compute" }
```

Optional fields: `slug` (for filename mismatches like `appinsights-instrumentation` → `app-insights-instrumentation`), `skillVersion`.

### 3.4 Tier Assessment

Skills are scored on 5 questions to determine Tier 1 (comprehensive) vs Tier 2 (essential):

| Question | Criterion | Score |
|----------|-----------|-------|
| Q1 | Services ≥ 3 | +2 |
| Q2 | UseFor items ≥ 5 | +2 |
| Q3 | ShouldTrigger prompts ≥ 3 | +1 |
| Q4 | Description ≥ 200 characters | +1 |
| Q5 | Services/McpTools reference specific Azure service names | +1 |

**Maximum score:** 7. **Tier 1 threshold:** ≥ 4 (default).

**Tier implications on output:**

| Feature | Tier 1 | Tier 2 |
|---------|--------|--------|
| Decision Guidance section | ✅ (if data exists) | ❌ |
| Suggested Workflow section | ✅ (if data exists) | ❌ |
| Detailed prompt display | ✅ | ❌ |
| MCP Tools section | ✅ (if data exists) | ✅ (if data exists) |
| Minimum word count (validation) | 100 | 50 |

> **Reference:** `SkillsGen.Core/Assessment/TierAssessor.cs:35-103`

---

## 4. Content Generation Rules

### 4.1 Maximum Template Capacity

The Handlebars template defines the maximum possible section order — the ceiling of what CAN be rendered. Not all sections are output in practice (see §4.2 for current content policy).

| Order | Section | Type | Condition |
|-------|---------|------|-----------|
| — | YAML frontmatter | MUST HAVE | Always |
| — | `# Azure skill for {DisplayName}` | MUST HAVE | Always |
| — | Description paragraph | MUST HAVE | Always |
| — | Skill metadata line | MUST HAVE | Always: `` **Skill:** `{name}` | [Source code](...) `` |
| 1 | `## What it provides` | MUST HAVE | Always |
| 1a | `### Azure services knowledge` | MAY HAVE | `hasServices` |
| 2 | `## Prerequisites` | MUST HAVE | Always (generator provides fallback) |
| 2a | `### Environment requirements` | MAY HAVE | `hasEnvironmentReqs` |
| 3 | `## When to use this skill` | MUST HAVE | Always (generator provides fallback) |
| 3a | `### When not to use this skill` | MAY HAVE | `hasDoNotUseFor` |
| 4 | `## MCP tools` | MAY HAVE | `hasMcpTools` |
| 5 | `## Decision guidance` | MAY HAVE | Tier 1 AND `hasDecisionGuidance` |
| 6 | `## Suggested workflow` | MAY HAVE | Tier 1 AND `hasWorkflow` |
| 7 | `## Deployment workflow` | MAY HAVE | Skill is `azure-prepare`, `azure-validate`, or `azure-deploy` |
| 8 | `## Example prompts` | MUST HAVE | Always (generator provides fallback) |
| 9 | `## Related skills` | MAY HAVE | `hasRelatedSkills` |
| 10 | `## Sub-skills` | MAY HAVE | `hasSubSkills` |
| 11 | `## Automatic activation` | MAY HAVE | `hasActivation` |
| 12 | `## Related content` | MUST HAVE | Always (includes default links) |

> **Reference:** `templates/skill-page-template.hbs:1-217`

### 4.2 Current Content Policy

Generated skills articles output **5 required H2 sections**:

1. `## What it provides`
2. `## Prerequisites`
3. `## When to use this skill`
4. `## Example prompts`
5. `## Related content`

#### Excluded sections (implementation details)

The following sections exist in the template (§4.1) but are **excluded from generated output**. Skills articles describe capabilities for customers — not internal implementation mechanics.

| Section | Reason for exclusion |
|---------|---------------------|
| `## MCP tools` | Implementation detail. Skills describe capabilities, not underlying MCP tool names. |
| `## Sub-skills` | Implementation detail. The published article covers the skill as a unit. |
| `## Suggested workflow` | Implementation detail. Workflows are internal to how the skill operates. |
| `## Decision guidance` | Conditional (Tier 1 only). Currently still rendered but may be reconsidered. |
| `## Deployment workflow` | Hardcoded for 3 skills (`azure-prepare`, `azure-validate`, `azure-deploy`); implementation detail. |

> **Generator enforcement:** The `hasMcpTools`, `hasSubSkills`, and `hasWorkflow` conditionals should be suppressed in the generator or the template blocks removed. See §7.1 (RESOLVED).

#### Conditional user-facing sections (allowed)

These MAY-HAVE sections ARE rendered when data exists — they are user-facing, not implementation details:

- `## Automatic activation` — tells the customer when the skill activates without prompting
- `## Related skills` — helps the customer discover adjacent capabilities

> ⚠️ The validator currently only checks for 3 of the 5 required sections — see §7.2.

### 4.3 Frontmatter Requirements

```yaml
---
title: Azure skill for {DisplayName}
description: {Full description from SKILL.md}
ms.topic: reference
ms.date: {M/d/yyyy format, UTC}
ms.custom: skill-version-{version}    # Only if skillVersion is set
author: diberry
ms.author: diberry
ms.service: azure-mcp-server
---
```

> **Reference:** `templates/skill-page-template.hbs:1-10`

### 4.4 Writing Style Rules

All generated prose must follow these rules:

| Rule | Correct | Incorrect |
|------|---------|-----------|
| Present tense | "returns", "creates" | "will return", "will create" |
| Contractions | "doesn't", "isn't", "can't" | "does not", "is not", "cannot" |
| Active voice | "The tool lists resources" | "Resources are listed by the tool" |
| Second person | "you", "your" | "we", "our", "us" |
| Sentence length | ≤ 25 words (hard max: 30) | Sentences over 30 words |
| Goal-before-action | "To list resources, use the command" | "Run the command to list resources" |
| No wordy phrases | "to", "use", "before", "because" | "in order to", "utilize", "prior to", "due to the fact that" |
| Prerequisite label format | `**Azure CLI** Install v2.60+` | `**Azure CLI:** Install v2.60+` (no colon after bold) |

> **Reference:** `prompts/skill-page-system-prompt.txt:1-24`, `data/shared-acrolinx-rules.txt`

### 4.5 LLM Rewriting Rules

> **Pipeline philosophy:** Lift-and-shift first, LLM polish second. The upstream SKILL.md is
> the source of truth for all factual content. The parser extracts it deterministically. The
> template renders it structurally. The LLM refines prose for clarity and Microsoft voice —
> it never generates structural content or invents facts.

#### 4.5.1 Pipeline Approach

The pipeline follows a **lift-and-shift-then-polish** model:

1. **Step 1 — Deterministic extraction:** The parser extracts all factual content from SKILL.md (structure, prompts, prerequisites, services, body content). The template renders it into the target article layout.
2. **Step 2 — LLM polish layer:** The LLM is applied ONLY to refine extracted prose for clarity, style, and Microsoft voice compliance. It is NOT a content generator — it refines what was extracted, it does not invent.

See §1.5 for the complete source-to-content mapping and classification of which source sections are deterministic lift vs LLM synthesis.

#### 4.5.2 Current LLM Usage

| Method | Purpose | Default State | Notes |
|--------|---------|---------------|-------|
| `RewriteIntroAsync` | Polish intro paragraph (2-3 sentences, max 60 words) | ON | Rewrites extracted description for customer voice |
| `GenerateKnowledgeOverviewAsync` | Polish knowledge overview | ON | Summarizes extracted body content |
| `SynthesizeWhatItProvidesAsync` | Polish "What it provides" summary | ON (with fallback chain) | Priority: LLM synthesis → curated JSON → mechanical build |
| `TranslateWorkflowStepsAsync` | Rewrite workflow steps | **OFF by default** | Implementation detail per §4.2 content policy; enable only if workflow section is re-included |

> **Reference:** `SkillsGen.Core/Generation/ILlmRewriter.cs`, `AzureOpenAiRewriter.cs`

#### 4.5.3 LLM Guardrails

- LLM does NOT generate: section headings, example prompts, prerequisites, service tables, or any structural content
- LLM receives extracted data as input, returns polished prose
- Temperature: 0.3, MaxTokens: 500 (`AzureOpenAiRewriter.cs:196-199`)
- Retry: 5 attempts with exponential backoff (1s, 2s, 4s, 8s, 16s)

#### 4.5.4 Note on `SynthesizeWhatItProvidesAsync`

This method currently feeds MCP tool purposes and workflow steps into the LLM prompt (`AzureOpenAiRewriter.cs:61-87`). Per §4.2 content policy, MCP tools and workflows are excluded from output — the prompt input should be reviewed/simplified to align with the content policy (i.e., remove implementation-detail inputs that could leak into customer-facing prose).

### 4.6 Content Constraints

| Constraint | Value | Rationale |
|-----------|-------|-----------|
| Max example prompts | 8 per skill (system prompt) / 10 (code cap) | Skills are high-level; 8 diverse prompts cover the range |
| "What it provides" | Must add NEW info beyond description | System prompt rule: no paraphrasing |
| UseFor items cap | 10 | `SkillPageGenerator.cs:50-51` |
| DoNotUseFor source | Only `SKILL.md` `DO NOT USE FOR:` | Never from `shouldNotTrigger` test data |
| Fallback prompts | Generated from UseFor with verb-detection | `SkillPageGenerator.cs:331-367` |
| Deployment workflow | Hardcoded for 3 skills only | `SkillPageGenerator.cs:238-277` |

### 4.7 Example Prompt Priority

1. **Curated prompts** from `skill-example-prompts.json` (highest fidelity)
2. **Trigger test data** from `triggers.test.ts` `shouldTrigger` entries
3. **Fallback generation** from `UseFor` items or `DetectionMarkers` — converted to natural-language questions ("How do I {verb phrase}?")

> **Reference:** `SkillPageGenerator.cs:67-91`

### 4.8 "What It Provides" Priority

1. **LLM synthesis** (passed as parameter, when available)
2. **Curated data** from `skill-what-it-provides.json`
3. **Mechanical build** (`BuildWhatItProvides`): Opening sentence + service list + tool purposes

> **Reference:** `SkillPageGenerator.cs:96-97`

### 4.9 Prerequisite Structure

Prerequisites are rendered as a structured object with typed sub-sections:

| Sub-section | Model | Rendered As |
|------------|-------|-------------|
| Azure authentication | `AzureRequirements` | `- **Azure authentication**—Sign in with \`az login\`...` |
| Azure subscription | `AzureRequirements` | `- **Azure subscription**—An active subscription...` |
| RBAC roles | `List<RbacRequirement>` | `- **{RoleName}** role at {Scope} scope—{Reason}` |
| Tools | `List<ToolRequirement>` | `- **{Name}** (v{MinVersion}+)—Install: \`{cmd}\`` |
| Resources | `List<ResourceRequirement>` | `- **{ResourceType}**—{Description}` |
| Environment | `List<string>` | `### Environment requirements` + bullets |

> **Reference:** `SkillsGen.Core/Models/SkillPrerequisites.cs`, `templates/skill-page-template.hbs:36-65`

---

## 5. Post-Processing

All post-processing is applied after template rendering, operating on the full rendered markdown. Frontmatter is separated, processed minimally (static replacements only), then rejoined.

> **Reference:** `SkillsGen.Core/PostProcessing/AcrolinxPostProcessor.cs`

### 5.1 Processing Pipeline Order

| Step | Operation | Scope |
|------|-----------|-------|
| 0 | Split frontmatter from body | — |
| 0a | Apply static text replacements to frontmatter description | Frontmatter only |
| 1 | Wrap bare skill names in backticks | Body (skip headings/frontmatter lines) |
| 2 | Static text replacements | Body (backtick-protected) |
| 3 | Contractions | Body (backtick-protected) |
| 4 | Expand acronyms on first use | Body |
| 5 | Remove duplicate acronym expansions | Body |
| 6 | Normalize URLs (strip `learn.microsoft.com` prefix) | Body |
| 7 | Rewrite goal-before-action patterns | Body |
| 8 | Wrap technical API terms in backticks | Body |
| 9 | Add commas after introductory phrases | Body |
| 10 | Remove bold label colons | Body |
| 11 | Split long sentences (>30 words at conjunctions) | Body |
| 12 | Remove consecutive duplicate sentences | Body |
| 13 | Rejoin frontmatter + body | — |

> **Reference:** `AcrolinxPostProcessor.cs:80-149`

### 5.2 Backtick Protection

Steps 2 and 3 use a protection mechanism: backtick spans (`` `content` ``) are temporarily replaced with placeholders before the transform runs, then restored afterward. This prevents transforms from modifying inline code.

> **Reference:** `AcrolinxPostProcessor.cs:210-233`

### 5.3 Contraction Rules

| Original | Replacement |
|----------|-------------|
| does not | doesn't |
| do not | don't |
| is not | isn't |
| are not | aren't |
| was not | wasn't |
| has not | hasn't |
| have not | haven't |
| will not | won't |
| would not | wouldn't |
| could not | couldn't |
| should not | shouldn't |
| can not / cannot | can't |
| it is | it's |
| that is | that's |

> **Reference:** `AcrolinxPostProcessor.cs:13-32`

### 5.4 Static Text Replacements

Loaded from `data/static-text-replacement.json`. Each entry maps a `Parameter` to a `NaturalLanguage` replacement (word-boundary-aware regex). Examples:

| Parameter | Replacement |
|-----------|-------------|
| `e.g.` | `for example` |
| `i.e.` | `in other words` |
| `id` | `ID` |
| `url` | `URL` |
| `sku` | `SKU` |

> **Reference:** `data/static-text-replacement.json`

### 5.5 Acronym Expansion

Loaded from `data/acronym-definitions.json`. Each acronym is expanded on **first use only**. Some acronyms have a `ContextPattern` for context-aware expansion:

| Acronym | Expansion | Context Pattern |
|---------|-----------|-----------------|
| MCP | Model Context Protocol | `Azure MCP Server` → `Azure Model Context Protocol (MCP) Server` |
| VM | virtual machine | — |
| AKS | Azure Kubernetes Service | — |
| RBAC | role-based access control | — |
| IaC | infrastructure as code | — |

> **Reference:** `data/acronym-definitions.json`, `AcrolinxPostProcessor.cs:122-124`

### 5.6 URL Normalization

All `learn.microsoft.com` URLs are stripped to relative paths:
- `https://learn.microsoft.com/en-us/azure/...` → `/azure/...`
- `https://learn.microsoft.com/azure/...` → `/azure/...`

> ⚠️ **Hard rule:** Generated articles must NEVER contain absolute `learn.microsoft.com` URLs. This triggers `docs-link-absolute` build validation errors.

> **Reference:** `AcrolinxPostProcessor.cs:128-129`

### 5.7 Technical Term Wrapping

Known API terms are auto-wrapped in backticks if not already inside backtick spans. Terms include: `DefaultAzureCredential`, `az login`, `azd`, `AMQP`, and others.

> **Reference:** `AcrolinxPostProcessor.cs:185-204`

### 5.8 Bare Skill Name Wrapping

References to `azure-*` patterns in body text are wrapped in backticks, with exceptions for:
- Content already inside backticks
- Markdown link URLs `](url)`
- Raw URLs `https://...`
- Heading lines

> **Reference:** `AcrolinxPostProcessor.cs:269-308`

### 5.9 Sentence Splitting

Sentences exceeding 30 words are split at conjunctions (`and`, `but`, `or`, `which`, `that`) after word 20. Splitting skips: frontmatter, headings, list items, table rows, and code blocks.

> **Reference:** `AcrolinxPostProcessor.cs:310-365`

### 5.10 Trigger Prompt Processing

A separate `ProcessText()` method applies lightweight transforms (static replacements + contractions only) to trigger prompts and other non-markdown strings. It does NOT apply URL normalization, acronym expansion, or sentence splitting.

> **Reference:** `AcrolinxPostProcessor.cs:158-179`

---

## 6. Validation Gates

Validation runs on the fully rendered and post-processed article.

> **Reference:** `SkillsGen.Core/Validation/SkillPageValidator.cs`

### 6.1 Required Section Checks (Errors)

The validator checks for the presence of these sections:

| Required Section | Pattern Matched |
|-----------------|-----------------|
| `## Prerequisites` | Case-insensitive contains |
| `## When to use` | Case-insensitive contains |
| `## What it provides` | Case-insensitive contains |

**Missing a required section = validation ERROR** (blocks generation).

> **Reference:** `SkillPageValidator.cs:8-13`

### 6.2 Frontmatter Validation (Errors)

- Content must start with `---`
- Frontmatter must contain `title:` field
- Frontmatter must contain `description:` field

> **Reference:** `SkillPageValidator.cs:72-87`

### 6.3 Word Count Thresholds (Warnings)

| Tier | Minimum Words |
|------|---------------|
| Tier 1 | 100 |
| Tier 2 | 50 |

No maximum word count — content length is driven by complexity.

Word count is calculated after stripping: frontmatter, HTML comments, and markdown formatting characters.

> **Reference:** `SkillPageValidator.cs:63-69`, `SkillPageValidator.cs:176-186`

### 6.4 Prompt Grounding Verification (Warnings)

When trigger data exists (`shouldTrigger.Count > 0`), at least one trigger prompt must appear verbatim in the rendered content. Otherwise: `GROUNDING` warning.

> **Reference:** `SkillPageValidator.cs:52-59`

### 6.5 Example Prompt Count (Errors/Warnings)

- **0 prompts** in rendered content → ERROR
- **< 5 prompts** → WARNING (recommended minimum: 5)

> **Reference:** `SkillPageValidator.cs:148-163`

### 6.6 Link Quality Checks (Warnings)

Bad link patterns detected:

| Pattern | Issue |
|---------|-------|
| `github-cilot` | Typo for "github-copilot" |
| `github-copiliot` | Typo |
| `micosoft\|microsft\|microsfot` | Microsoft misspellings |
| `/docs/azure/` | Fabricated URL pattern |
| `learn.microsoft.com/en-us/en-us/` | Double locale |

> **Reference:** `SkillPageValidator.cs:16-23`

### 6.7 Additional Quality Checks (Warnings)

| Check | ID | Description |
|-------|----|-------------|
| GitHub Copilot mentioned | `PREREQ_COPILOT` | GitHub Copilot should be listed as required tool |
| Duplicate Copilot prereq | `PREREQ_DUPLICATE` | GitHub Copilot appears multiple times in prerequisites |
| "Work with" fragments | `FRAGMENT` | Vague bullet pattern |
| Absolute URLs | `ACROLINX_URLS` | Contains `learn.microsoft.com` absolute URLs |
| Negative in positive | `NEGATIVE_IN_POSITIVE` | "When to use" section contains redirect/negative items |
| Missing trigger file | `PROMPT_SOURCE` | No `triggers.test.ts` found; fallback prompts used |

> **Reference:** `SkillPageValidator.cs:46-169`

---

## 7. Known Conflicts & TODOs

### 7.1 ✅ RESOLVED: MCP Tools Section vs "No MCP Tool Names" Directive

**Template:** `skill-page-template.hbs:87-98` renders a `## MCP tools` section with a table of tool names and descriptions when `hasMcpTools` is true.

**Team convention:** Published articles must NOT list MCP tool names — no tools tables, no inline MCP tool references.

**Resolution (§4.2 content policy):** The content policy excludes MCP tools, sub-skills, and workflows from generated output as implementation details. Skills articles describe capabilities for customers, not internal mechanics.

**Action required:** Either suppress the `hasMcpTools` conditional in the generator (set to `false` regardless of data), OR remove the `## MCP tools` block from the template entirely. The template code may remain as dead code temporarily, but the content policy (§4.2) is authoritative.

### 7.2 ⚠️ GAP: Validator Checks Only 3 of 5 Required Sections

The validator at `SkillPageValidator.cs:8-13` checks for:
- `## Prerequisites`
- `## When to use`
- `## What it provides`

But team conventions require **5 required H2 sections**. Missing from validation:
- `## Example prompts`
- `## Related content`

**TODO:** Add these two sections to the `RequiredSections` array.

### 7.3 ⚠️ GAP: Example Prompt Cap Inconsistency

- **System prompt** (`skill-page-system-prompt.txt:16`): "Maximum 8 example prompts per skill"
- **Code cap** (`SkillPageGenerator.cs:32`): `MaxExamplePrompts = 10`

**Resolution needed:** Align to 8 (the editorial standard per PR #8978 feedback).

### 7.4 ⚠️ GAP: Word Count Convention vs Code

- **Team convention:** Minimum 250 words (not 1000); typical articles ~300-400 words
- **Validator code:** Tier 1 minimum is 100, Tier 2 minimum is 50

The code thresholds are permissive relative to the convention. Whether the convention should be enforced in code is a design decision — the current approach allows the pipeline to succeed with thin content and rely on human review to catch it.

### 7.5 NOTE: `ms.service` Value

All generated articles use `ms.service: azure-mcp-server`. If skills move to a different service taxonomy, this frontmatter field must be updated in the template.

### 7.6 NOTE: Deployment Workflow is Hardcoded

The deployment workflow section (`SkillPageGenerator.cs:238-277`) is hardcoded for exactly 3 skills: `azure-prepare`, `azure-validate`, `azure-deploy`. Any new deployment pipeline skill would require a code change.

### 7.7 NOTE: `learn` Parameter Convention

The `learn` parameter is a global/common MCP parameter and should NOT appear in per-tool parameter documentation. This is enforced in the MCP pipeline via `common-parameters.json` filtering but is not directly relevant to skills articles (skills don't have parameter tables). Noted here for cross-pipeline consistency.

### 7.8 TODO: Shared Acrolinx Rules Injection

The system prompt contains a `{{ACROLINX_RULES}}` placeholder (`skill-page-system-prompt.txt:24`) that is replaced at runtime with the content of `data/shared-acrolinx-rules.txt`. The injection mechanism should be documented in the pipeline orchestrator.

### 7.9 TODO: `SynthesizeWhatItProvidesAsync` Prompt Alignment

Per §4.5.4, `SynthesizeWhatItProvidesAsync` currently feeds MCP tool purposes and workflow steps into the LLM prompt (`AzureOpenAiRewriter.cs:61-87`). These are implementation details excluded by §4.2 content policy. The prompt input should be simplified to remove MCP tool names and workflow steps, feeding only customer-facing extracted content. `TranslateWorkflowStepsAsync` is OFF by default per pipeline philosophy (§4.5.2).

---

## Appendix A: Data Model Reference

### SkillData

```
SkillsGen.Core/Models/SkillData.cs
```

| Property | Type | Source |
|----------|------|--------|
| `Name` | `string` | Frontmatter `name` (lowercased) |
| `DisplayName` | `string` | Frontmatter `display_name` or derived |
| `Description` | `string` | Frontmatter `description` (full, decoded) |
| `ShortDescription` | `string` (computed) | First 1-2 sentences, max 200 chars |
| `UseFor` | `List<string>` | `USE FOR:` marker or body section |
| `DoNotUseFor` | `List<string>` | `DO NOT USE FOR:` marker or body section |
| `Services` | `List<ServiceEntry>` | Body services table |
| `McpTools` | `List<McpToolEntry>` | Body MCP tools section |
| `WorkflowSteps` | `List<string>` | Body workflow section |
| `DecisionGuidance` | `List<DecisionEntry>` | Body decision guidance section |
| `RelatedSkills` | `List<string>` | Body cross-references |
| `Prerequisites` | `List<string>` | Body prerequisite sections (merged) |
| `Compatibility` | `List<string>` | Frontmatter `compatibility` |
| `RawBody` | `string` | Full body after frontmatter |
| `Activation` | `ActivationTrigger?` | Description directives + body markers |
| `SubSkills` | `List<SubSkillData>` | Sub-skill directories |

### TriggerData

```
SkillsGen.Core/Models/TriggerData.cs
```

| Property | Type | Source |
|----------|------|--------|
| `ShouldTrigger` | `List<string>` | `triggers.test.ts` shouldTrigger array |
| `ShouldNotTrigger` | `List<string>` | `triggers.test.ts` shouldNotTrigger array |
| `SourceFile` | `string?` | Path to source file |

### TierAssessment

```
SkillsGen.Core/Models/TierAssessment.cs
```

| Property | Type | Description |
|----------|------|-------------|
| `Tier` | `int` | 1 (comprehensive) or 2 (essential) |
| `Questions` | `List<QuestionResult>` | Q1-Q5 results with evidence |
| `Rationale` | `string` | Human-readable score summary |
| `ShowToolsSection` | `bool` | MCP tools exist |
| `ShowTriggersSection` | `bool` | Trigger prompts exist |
| `ShowDecisionGuidance` | `bool` | Tier 1 AND guidance exists |
| `ShowWorkflow` | `bool` | Tier 1 AND workflow exists |
| `ShowDetailedPrompts` | `bool` | Tier 1 |

---

## Appendix B: Pipeline Module Map

```
skills-generation/
├── SkillsGen.Cli/                    # CLI entry point
├── SkillsGen.Core/
│   ├── Assessment/
│   │   └── TierAssessor.cs           # Tier 1/2 scoring (§3.4)
│   ├── Data/
│   │   └── CuratedDataLoader.cs      # Loads curated JSON overrides (§3.1)
│   ├── Generation/
│   │   └── SkillPageGenerator.cs     # Template rendering + context building (§4)
│   ├── Models/
│   │   ├── SkillData.cs              # Core data model (§2, Appendix A)
│   │   ├── SkillPrerequisites.cs     # Prerequisite types (§4.9)
│   │   ├── TierAssessment.cs         # Tier result record (§3.4)
│   │   └── TriggerData.cs            # Trigger test data (§1.2)
│   ├── Parsers/
│   │   └── SkillMarkdownParser.cs    # Source extraction (§2)
│   ├── PostProcessing/
│   │   └── AcrolinxPostProcessor.cs  # Acrolinx fixes (§5)
│   └── Validation/
│       └── SkillPageValidator.cs     # Validation gates (§6)
├── data/
│   ├── skills-inventory.json         # Canonical skill list (§3.2-3.3)
│   ├── skill-what-it-provides.json   # Curated overrides (§3.1)
│   ├── skill-example-prompts.json    # Curated prompts (§3.1)
│   ├── skill-related-links.json      # Curated links (§3.1)
│   ├── static-text-replacement.json  # Acrolinx replacements (§5.4)
│   ├── acronym-definitions.json      # Acronym expansion (§5.5)
│   └── shared-acrolinx-rules.txt     # Injected into system prompt (§5.8)
├── prompts/
│   ├── skill-page-system-prompt.txt  # Writing style rules (§4.4)
│   └── skill-page-user-prompt-intro.txt  # LLM intro prompt (§4.5)
└── templates/
    └── skill-page-template.hbs       # Output template (§4.1)
```
