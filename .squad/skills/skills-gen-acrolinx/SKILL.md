---
name: "Skills Generation Acrolinx Improvement"
description: "Improve Acrolinx compliance scores for generated skill pages"
domain: "content-quality"
confidence: "high"
source: "manual"
tools:
  - name: "powershell"
    description: "Run Vale, regenerate, check scores"
    when: "To lint, generate, and verify improvements"
  - name: "github-mcp-server-pull_request_read"
    description: "Read Acrolinx scorecard from PR comments"
    when: "To get latest Acrolinx scores from the content PR"
  - name: "edit"
    description: "Fix generator code"
    when: "To update parser, post-processor, template, or replacements"
---

## Context

Generated skill pages are scored by Acrolinx on the content PR (azure-dev-docs-pr). The minimum passing score is 80. Scores come from three categories: Terminology, Spelling, and Clarity.

## When to Use

- Acrolinx scores are below 80 for generated skill pages
- New skills generate with low scores
- After template or parser changes that might affect content quality

## Improvement Loop

### Step 1: Get current Acrolinx scores

Read the latest Acrolinx scorecard from the content PR comments:

```
gh api repos/MicrosoftDocs/azure-dev-docs-pr/issues/comments/{comment_id} --jq '.body'
```

Parse to find failing files and their worst category (Terminology, Spelling, or Clarity).

### Step 2: Run Vale locally FIRST

Before pushing to GitHub, check with Vale:

```bash
pwsh skills-generation/scripts/lint-vale.ps1 -TargetDir ./generated-skills/
```

Vale catches ~80% of what Acrolinx flags, instantly, with no API calls.

### Step 3: Diagnose the root cause

| Acrolinx category | Common causes | Where to fix |
|-------------------|--------------|-------------|
| **Terminology** | Wrong product name, deprecated term, non-standard naming | `data/static-text-replacement.json` |
| **Terminology** | Raw API names not in backticks | `AcrolinxPostProcessor.WrapTechnicalTerms()` |
| **Terminology** | Lowercase acronym (aws, gcp, kql) | `CleanListItem` or `CleanDescription` keepUpper set |
| **Spelling** | Hyphenated skill names in prose | `AcrolinxPostProcessor.WrapBareSkillNames()` |
| **Spelling** | HTML entities (&quot;, &#8212;) | `DecodeHtmlEntities()` in parser |
| **Spelling** | Handlebars HTML-escaping quotes | Use `{{{var}}}` not `{{var}}` in template |
| **Clarity** | Long sentences (>35 words) | `AcrolinxPostProcessor.SplitLongSentences()` |
| **Clarity** | Passive voice | LLM rewrite or template phrasing |
| **Clarity** | Too many bullet points | `NaturalizeItems` cap (max 10) |
| **Clarity** | Em-dash spacing (` — ` vs `—`) | Template uses `—` (no spaces) |

### Step 4: Fix in the GENERATOR, not content files

**Never edit generated files.** Always fix the source:

1. **Parser** (`SkillMarkdownParser.cs`) — for input cleaning
2. **Post-processor** (`AcrolinxPostProcessor.cs`) — for output cleanup
3. **Template** (`skill-page-template.hbs`) — for structural changes
4. **Static replacements** (`static-text-replacement.json`) — for terminology
5. **Generator** (`SkillPageGenerator.cs`) — for NaturalizeItems, display names

### Step 5: Regenerate and verify

```bash
# Rebuild
dotnet build skills-generation/skills-generation.slnx --configuration Release

# Regenerate all 24 from local clone
dotnet run --project skills-generation/SkillsGen.Cli --configuration Release -- \
  generate-skills --all --no-llm --source local \
  --source-path ./GitHub-Copilot-for-Azure/plugin/skills \
  --tests-path ./GitHub-Copilot-for-Azure/tests \
  --out ./generated-skills/

# Run Vale locally
pwsh skills-generation/scripts/lint-vale.ps1 -TargetDir ./generated-skills/
```

### Step 6: Push to content PR and check Acrolinx

Copy regenerated files to the content PR branch and push. Wait for Acrolinx to re-score (~2-3 min).

## Acrolinx Score History

The pipeline improved from avg 69 to avg 92 over 7 rounds:

| Round | Passing | Avg | Key fix |
|-------|---------|-----|---------|
| 1 | 1/24 | 69 | Initial generation |
| 3 | 14/24 | 82 | Display names, entity decoding |
| 4 | 16/24 | 82 | Acronym preservation |
| 7 | 23/24 | 92 | Vale CLI + em-dash + backtick wrapping |

## Three Acronym Lists to Keep in Sync

The parser has THREE places where acronyms are listed — they must all contain the same set:

1. `CleanDescription()` → `keepUpper` HashSet
2. `CleanListItem()` → `keepUpperInItems` HashSet
3. `DeriveDisplayName()` → `acronyms` HashSet

Current list: AI, API, CLI, SDK, MCP, RBAC, AKS, SQL, SMB, ARM, AWS, GCP, KQL, ADX, MSAL, OAuth, IoT, SaaS, PaaS, IaaS, VM, VMs, VMSS, HTTP, HTTPS, REST, JSON, YAML, DNS, CDN, SSH, SSL, TLS, TCP, UDP, IP, URL, URI, SKU, SLA
