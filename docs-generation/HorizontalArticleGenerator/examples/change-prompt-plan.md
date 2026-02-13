# Change Prompt Plan: Horizontal Article Generation Issues

## Purpose

This document analyzes why `horizontal-article-keyvault.md` (the generated output) does not match the expected format and quality of `horizontal-article-keyvault-correct.md` (the hand-edited correct version). It identifies specific gaps in the system prompt, user prompt, and Handlebars template, and proposes changes to close each gap.

---

## Summary of Differences

| Aspect | Generated (Current) | Correct (Target) |
|--------|---------------------|-------------------|
| **Title** | `Use Azure MCP Server with Keyvault` | `Manage Azure Key Vault with Azure MCP Server` |
| **Frontmatter** | Missing `author`, `ms.author`, `ms.reviewer`, `content_well_notification`, `ai-usage`, `ms.custom`, `mcp-cli.version`, `#customer intent` | All present |
| **H1** | `Use Azure MCP Server with Keyvault` | `Manage Azure Key Vault with Azure MCP Server` |
| **Intro paragraph** | Generic AI/MCP boilerplate intro | Service-action-oriented intro with doc link and context |
| **Section: "Overview"** | Generic overview, capabilities as bullet list | Replaced by `What is the Azure MCP Server?` with `[!INCLUDE]` and contextual bullet points |
| **Section: Prerequisites** | Generic MCP setup bullets | Structured subsections: "Azure requirements" with doc links; uses `[!INCLUDE]` for shared prereqs |
| **Section: "Where can you use..."** | Missing entirely | Present with `[!INCLUDE]` reference |
| **Section: Tools** | Flat list linking to parameter files | Grouped by functional category (keys, secrets, certificates, HSM) with H3s, descriptions, and "Common scenarios" bullets |
| **Section: Scenarios** | Numbered "Scenario 1/2/3" with generic examples | Replaced by a concise "Get started" section with 3 numbered steps and example prompts |
| **Section: Auth & Permissions** | Separate section with RBAC roles and auth notes | Folded into Prerequisites as doc links |
| **Section: Troubleshooting** | Present with issue/resolution pairs | Removed entirely (not in correct version) |
| **Section: Best practices** | Bullet list of AI-generated practices | Curated practices with service doc links, MCP-specific guidance, emphasis on combining tools |
| **Section: Related content** | Links to GitHub repo and generic docs | Links to internal doc paths (`../overview.md`, `/azure/key-vault/...`) using MS Learn link format |
| **Footer** | Generated timestamp line | No footer |

---

## Root Cause Analysis

### 1. Template Structure Mismatch

**Problem**: The Handlebars template (`horizontal-article-template.hbs`) produces a fundamentally different article structure than the correct version.

**Specific issues**:
- Template hardcodes `# Use Azure MCP Server with {{serviceBrandName}}` — the correct version uses verb-first titles like `# Manage Azure Key Vault with Azure MCP Server`
- Template has `## Overview` — correct version has `## What is the Azure MCP Server?` with `[!INCLUDE]`
- Template has `## Available MCP tools` as a flat list — correct version groups tools into functional categories with H3 headings and "Common scenarios" sub-bullets
- Template has `## Common scenarios` with numbered `Scenario 1:` headings — correct version has `## Get started` with a simple numbered list
- Template has standalone `## Authentication and permissions` and `## Troubleshooting` sections — correct version folds auth into Prerequisites and omits troubleshooting
- Template includes a `---` footer with timestamp — correct version has none

**Impact**: Even if the AI generates perfect JSON, the template will render an incorrect structure.

### 2. Frontmatter Gaps

**Problem**: The template produces minimal frontmatter. The correct version requires:

```yaml
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ms.service: azure-mcp-server      # Not the service identifier
content_well_notification: 
  - AI-contribution
ai-usage: ai-generated
ms.custom: build-2025
mcp-cli.version: 2.0.0-beta.19+...

#customer intent: As an Azure Key Vault administrator, I want to...
```

**Missing from template**:
- `author` / `ms.author` / `ms.reviewer` — should be static config, not AI-generated
- `ms.service` is set to `{{serviceIdentifier}}` (e.g., `keyvault`) — should be `azure-mcp-server`
- `content_well_notification`, `ai-usage`, `ms.custom` — completely absent
- `mcp-cli.version` — absent (the version goes in footer instead)
- `#customer intent` comment — not in template

### 3. Title and H1 Format

**Problem**: Template hardcodes `Use Azure MCP Server with {{serviceBrandName}}`.

**Correct pattern**: `Manage [Service] with Azure MCP Server` — the verb should be dynamic and action-oriented, reflecting the service's primary operations.

**Required change**: Either the AI should generate the title verb, or the system prompt should instruct the AI to produce it (e.g., `genai-titleVerb`).

### 4. Intro Paragraph Style

**Problem (template)**:
```
Azure Model Context Protocol (MCP) Server enables AI assistants like GitHub Copilot, 
Claude Desktop, and others to interact with {{serviceBrandName}} through natural language 
commands. This integration allows you to manage {{genai-serviceShortDescription}} without 
writing code or remembering complex CLI syntax.
```

**Correct pattern**:
```
Manage keys, secrets, and certificates using natural language conversations with AI 
assistants through the Azure MCP Server.

[Azure Key Vault](/azure/key-vault/general/overview) is a cloud service for securely 
storing and accessing secrets, keys, and certificates. ...
```

**Issues**:
- The correct intro is 1 short sentence summarizing the action, not a 2-sentence MCP explanation
- The correct intro is followed by a service overview paragraph with a doc link — not the `## Overview` section
- The correct version never mentions specific AI assistant names (GitHub Copilot, Claude Desktop) in the intro

### 5. Tools Section: Flat List vs Categorized Groups

**Problem**: The AI generates `tools` with `genai-shortDescription` per tool, and the template renders them as a flat bullet list.

**Correct pattern**: Tools are grouped by functional category (Manage keys, Manage secrets, Manage certificates, Manage Managed HSM settings), each with:
- An H3 heading (`### Manage keys`)
- A contextual paragraph with doc links
- A `**Common scenarios**:` section with 2-3 bullet points

**Required change**: The AI needs to generate `genai-toolCategories` (or similar) that groups tools by function, with per-category descriptions and common scenario bullets. The template must render these as H3 groups.

### 6. Scenarios vs "Get started"

**Problem**: The template renders 3-5 full scenario sections with titles, descriptions, example commands, and expected outcomes.

**Correct pattern**: A single `## Get started` section with 3 numbered steps:
1. Set up your environment (links to setup docs)
2. Start exploring (3 example prompts)
3. Learn more (link to tools reference)

**Required change**: Either remove the scenarios section from the template and add a `## Get started` section, or instruct the AI to generate a `genai-getStartedPrompts` field instead of full scenarios.

### 7. `[!INCLUDE]` References

**Problem**: The generated output has no `[!INCLUDE]` directives. The correct version uses:
- `[!INCLUDE [mcp-introduction](../includes/mcp-introduction.md)]`
- `[!INCLUDE [mcp-prerequisites](../includes/mcp-prerequisites.md)]`
- `[!INCLUDE [mcp-usage-contexts](../includes/mcp-usage-contexts.md)]`

**Required change**: The template must include these `[!INCLUDE]` references in the appropriate sections. These are static (same for every article), not AI-generated.

### 8. Link Format

**Problem**: Generated links use GitHub URLs (`https://github.com/microsoft/azure-mcp-server`) and full `https://learn.microsoft.com/en-us/azure/...` URLs.

**Correct pattern**: Microsoft Learn relative paths (`../overview.md`, `../tools/azure-key-vault.md`) and Azure doc-relative paths (`/azure/key-vault/general/overview`).

**Required changes**:
- System prompt should instruct AI to use `/azure/...` format for Azure doc links (not full URLs)
- Template static links should use relative paths (not GitHub URLs)
- The `genai-additionalLinks` should use `/azure/...` format

### 9. Best Practices Style

**Problem**: Generated best practices use `**Title** - Description` format and include generic advice like "Use Azure AD for authentication" and "Regularly rotate secrets and keys."

**Correct pattern**: 
- Action-oriented with bold verb start: `**Specify vault name clearly**: Always include...`
- Service-specific and MCP-context-aware: "Use Azure MCP Server for quick queries and inventory checks. Use Azure CLI or PowerShell for vault configuration changes..."
- Includes a link to the service's security doc at the end

**Required change**: System prompt should emphasize MCP-specific best practices and instruct against generic Azure advice. The template closing should add a "For general [Service] security guidance" sentence with a doc link.

### 10. "ms.service" Value

**Problem**: Template sets `ms.service: {{serviceIdentifier}}` (e.g., `keyvault`).

**Correct value**: `ms.service: azure-mcp-server` (it's always `azure-mcp-server` since these are MCP Server docs, not service-specific docs).

---

## Proposed Changes

### Phase 1: Template Overhaul (No AI changes needed)

| # | Change | File | Details |
|---|--------|------|---------|
| 1.1 | Fix frontmatter | `horizontal-article-template.hbs` | Add `author`, `ms.author`, `ms.reviewer`, `content_well_notification`, `ai-usage`, `ms.custom`, `mcp-cli.version`. Change `ms.service` to `azure-mcp-server`. Add `#customer intent` comment. |
| 1.2 | Add `[!INCLUDE]` blocks | `horizontal-article-template.hbs` | Add `mcp-introduction`, `mcp-prerequisites`, `mcp-usage-contexts` includes in correct sections. |
| 1.3 | Remove footer | `horizontal-article-template.hbs` | Remove the `---` and `*This article was generated...` line. |
| 1.4 | Fix static links | `horizontal-article-template.hbs` | Replace GitHub URLs with relative doc paths (`../overview.md`, `../get-started.md`). |
| 1.5 | Remove `## Troubleshooting` | `horizontal-article-template.hbs` | Not present in correct version; remove or make conditional. |
| 1.6 | Replace `## Authentication and permissions` | `horizontal-article-template.hbs` | Fold RBAC info into Prerequisites; remove standalone section. |

### Phase 2: System/User Prompt Changes (AI output changes)

| # | Change | File | Details |
|---|--------|------|---------|
| 2.1 | Add `genai-titleVerb` | System prompt | New field: a single action verb for the title (e.g., "Manage", "Monitor", "Deploy"). Used in template as `{{genai-titleVerb}} {{serviceBrandName}} with Azure MCP Server`. |
| 2.2 | Add `genai-introSentence` | System prompt | New field: 1 short sentence summarizing what users can do (e.g., "Manage keys, secrets, and certificates using natural language conversations..."). |
| 2.3 | Add `genai-serviceContextParagraph` | System prompt | New field: 2-3 sentences explaining the service with a doc link in `/azure/...` format, starting with `[Service Name](/azure/...)` is a cloud service for...`. |
| 2.4 | Add `genai-mcpBenefitBullets` | System prompt | New field: 4-6 bullet points on what MCP enables for this service specifically (replaces `genai-capabilities`). Phrased as "verb + noun" without leading dash. |
| 2.5 | Replace `tools` with `genai-toolCategories` | System prompt | New field: array of `{ categoryTitle, categoryDescription, docLinks, tools[], commonScenarios[] }`. Groups tools by functional area. |
| 2.6 | Replace `genai-scenarios` with `genai-getStartedPrompts` | System prompt | New field: array of 3 natural language example prompts for the "Get started" section. These should demonstrate diverse tool categories. |
| 2.7 | Add `genai-customerIntent` | System prompt | New field: customer intent statement for frontmatter comment (e.g., "As an Azure Key Vault administrator, I want to manage secrets, keys, and vault configurations using natural language..."). |
| 2.8 | Update link format guidance | System prompt + User prompt | Instruct AI to use `/azure/...` relative format for all Azure doc links, not full URLs. |
| 2.9 | Update best practices guidance | System prompt | Emphasize MCP-specific practices, service doc link at end, avoid generic Azure advice. |
| 2.10 | Remove `genai-commonIssues` / troubleshooting | System prompt | Remove from JSON schema, not needed in correct format. |
| 2.11 | Remove `genai-aiSpecificScenarios` | System prompt | Not present in correct version; simplify. |
| 2.12 | Remove `genai-authenticationNotes` | System prompt | Folded into Prerequisites via `[!INCLUDE]`. |

### Phase 3: Model + Template Data Changes (C# code)

| # | Change | File | Details |
|---|--------|------|---------|
| 3.1 | Update `AIGeneratedArticleData.cs` | Models | Add new fields: `TitleVerb`, `IntroSentence`, `ServiceContextParagraph`, `McpBenefitBullets`, `ToolCategories`, `GetStartedPrompts`, `CustomerIntent`. Remove: `CommonIssues`, `AISpecificScenarios`, `AuthenticationNotes`. |
| 3.2 | Update `HorizontalArticleTemplateData.cs` | Models | Mirror the model changes from 3.1. |
| 3.3 | Update `MergeData()` | `HorizontalArticleGenerator.cs` | Map new fields in the merge step. |
| 3.4 | Update `RenderAndSaveArticle()` | `HorizontalArticleGenerator.cs` | Add new fields to the template data dictionary. |
| 3.5 | Update `ValidateAIContent()` | `HorizontalArticleGenerator.cs` | Adjust validations for new fields (e.g., validate `titleVerb` is a single word, `getStartedPrompts` has exactly 3 items). |

### Phase 4: New Template

| # | Change | File | Details |
|---|--------|------|---------|
| 4.1 | Create new template | `templates/horizontal-article-template-v2.hbs` | New template matching the correct version's structure exactly, using the new AI-generated fields and `[!INCLUDE]` references. |
| 4.2 | Switch template path | `HorizontalArticleGenerator.cs` | Update `TEMPLATE_PATH` constant to point to the new template. |

---

## Priority Order

1. **Phase 1 (Template)** — Many issues are purely structural and don't require AI changes. Fixing the template alone would close ~50% of the gap.
2. **Phase 2 (Prompts)** — The AI is being asked to generate the wrong fields. Updating the prompt to request the correct fields is necessary for the remaining ~40%.
3. **Phase 3 (Code)** — Required to wire the new fields through the pipeline.
4. **Phase 4 (New Template)** — Delivers the final correct structure. Could be done in parallel with Phase 3.

---

## Validation Criteria

A correctly generated article should:

1. Have `ms.service: azure-mcp-server` (not the service identifier)
2. Have all required frontmatter fields including `author`, `ai-usage`, `#customer intent`
3. Use a verb-first H1: `{Verb} {Service} with Azure MCP Server`
4. Have a 1-sentence intro paragraph (not MCP boilerplate)
5. Have `## What is the Azure MCP Server?` with `[!INCLUDE]`
6. Have `## Prerequisites` with Azure requirements subsection and `[!INCLUDE]`
7. Have `## Where can you use Azure MCP Server?` with `[!INCLUDE]`
8. Have `## Available tools for {Service}` with categorized H3 groups
9. Have `## Get started` with 3 numbered steps
10. Have `## Best practices` with MCP-specific guidance
11. Have `## Related content` with relative doc links
12. Have NO `## Troubleshooting`, `## Authentication and permissions`, or footer timestamp
13. Use `/azure/...` link format (not full URLs)
14. Use `[!INCLUDE]` references (not inline shared content)
