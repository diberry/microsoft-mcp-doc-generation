# Acrolinx Compliance Strategy for Tool-Family Generation

**Author:** Sage (AI / Prompt Engineer)
**Date:** 2026-03-25
**Status:** Research Complete — Ready for Implementation

## Executive Summary

Our generated tool-family articles consistently score below the Acrolinx minimum of 80. Of 10 articles sampled across recent PRs to `MicrosoftDocs/azure-dev-docs-pr`, **7 fail** (below 80) and only 3 pass. The worst performer (Azure Deploy) scores 61. This document catalogs every identified Acrolinx rule violation, maps each to a pipeline stage, and prescribes specific fixes — either as prompt instructions or deterministic post-processors.

**Goal:** Bring all tool-family articles to score **85+** consistently (5-point margin above the 80 threshold).

## Current Scores (from PR Research)

| Article | PR | Latest Score | Status |
|---------|-----|-------------|--------|
| Azure Cosmos DB | #8641 | 92 | ✅ Pass |
| Azure WAF | #8754 | 83 | ✅ Pass |
| Azure App Lens | #8749 | 82 | ✅ Pass |
| Azure Compute (VMs) | #8668 | 79 | ❌ Borderline |
| Azure AI Search | #8753 | 78 | ❌ Borderline |
| Azure Monitor | #8747 | 76 | ❌ Fail |
| Azure File Shares | #8748 | 76 | ❌ Fail |
| Azure Cloud Architect | #8755 | 67 | ❌ Fail |
| Azure Database PostgreSQL | #8751 | 64 | ❌ Fail |
| Azure Deploy | #8750 | 61 | ❌ Fail (worst) |

**Pass rate: 30%** (3/10). Mean score: 75.8. Include files (`azure-services.md`) score 96 — the problem is tool-family articles specifically.

### Score Progression Insights

- **Cosmos DB** went from 88→92 after targeted fixes (MCP acronym, sentence splitting, branding).
- **Compute** went from 74→85 over ~15 iterations — primarily clarity improvements.
- **Deploy** remained stuck at 60-61 across 6 iterations — structural problems too deep for manual fixes.

---

## Acrolinx Scoring Categories

Acrolinx evaluates content on these weighted categories. The total score is a composite:

| Category | What It Measures | Weight (approx) |
|----------|-----------------|-----------------|
| **Clarity / Readability** | Sentence length, complexity, paragraph structure | High |
| **Spelling & Grammar** | Grammar rules, punctuation, tense consistency | High |
| **Terminology** | Brand names, product names, deprecated terms | Medium |
| **Tone** | Contractions, formality level, reader address | Medium |
| **Consistency** | Term usage patterns, capitalization | Low–Medium |
| **Brand** | Microsoft branding guidelines | Low–Medium |
| **Inclusive Language** | Bias-free language, gendered terms | Low |

---

## Catalog of Acrolinx Rules Affecting Tool-Family Docs

### Category 1: Clarity / Readability

These rules have the **highest impact** on scores. Clarity jumped +24 points on Compute when addressed.

| Rule | Description | Current Violation Pattern | Severity |
|------|-------------|-------------------------|----------|
| **CR-1: Sentence length** | Sentences should be ≤30 words | AI generates compound sentences of 40-60+ words. Tool descriptions often pack all capabilities into one sentence. | 🔴 Critical |
| **CR-2: Paragraph length** | Paragraphs should be ≤3 sentences | Tool descriptions are often a single massive paragraph with no breaks. | 🔴 Critical |
| **CR-3: Complex sentence structure** | Avoid multiple subordinate clauses | AI produces nested "which…that…when…if" chains. | 🟠 High |
| **CR-4: JSON schema in prose** | Long inline JSON degrades readability score | Deploy article embeds full JSON schemas as parameter descriptions, creating hundreds of non-prose tokens. | 🔴 Critical (Deploy-specific) |
| **CR-5: Wordy phrases** | "In order to" → "To"; "due to the fact that" → "because" | AI favors verbose constructions. | 🟡 Medium |
| **CR-6: Vague qualifiers** | Avoid "various", "several", "numerous", "certain" | AI uses hedging language frequently. | 🟡 Medium |

### Category 2: Grammar (Spelling & Grammar)

| Rule | Description | Current Violation Pattern | Severity |
|------|-------------|-------------------------|----------|
| **GR-1: Present tense** | Use present tense, not future ("lists" not "will list") | AI often writes "This tool will..." or "The command will return..." | 🟠 High |
| **GR-2: Active voice** | Use active voice ("The tool lists clusters" not "Clusters are listed by the tool") | AI defaults to passive constructions for technical descriptions. | 🟠 High |
| **GR-3: Comma after introductory phrase** | "After you authenticate, run the command" | AI omits commas after introductory "If/When/After/Before" clauses. | 🟡 Medium |
| **GR-4: Subject-verb agreement** | Particularly with compound subjects | Rare but happens with complex tool descriptions. | 🟢 Low |
| **GR-5: Dangling modifiers** | "Using the tool, the resources are listed" → "When you use the tool, it lists the resources" | AI produces dangling participial phrases. | 🟡 Medium |

### Category 3: Tone

| Rule | Description | Current Violation Pattern | Severity |
|------|-------------|-------------------------|----------|
| **TN-1: Use contractions** | "don't", "isn't", "it's", "you're" | ContractionFixer handles negative contractions (#145). **Positive contractions still missing**: "it is"→"it's", "you are"→"you're", "we have"→"we've". | 🟠 High |
| **TN-2: Address the reader as "you"** | "You can list clusters" not "Users can list clusters" or "One can list clusters" | AI sometimes uses third-person. | 🟡 Medium |
| **TN-3: Conversational tone** | Avoid overly formal academic language | AI produces stiff, manual-like prose. | 🟡 Medium |

### Category 4: Terminology

| Rule | Description | Current Violation Pattern | Severity |
|------|-------------|-------------------------|----------|
| **TM-1: Microsoft brand names** | "Azure Cosmos DB" not "CosmosDB", "Microsoft Entra ID" not "Azure AD" | Static replacement covers CosmosDB→Azure Cosmos DB. Missing: many others. | 🟡 Medium |
| **TM-2: Deprecated terms** | "Azure Active Directory"→"Microsoft Entra ID"; "master"→"primary" | No systematic scanning for deprecated Microsoft terms. | 🟠 High |
| **TM-3: Acronym definition** | Define acronyms on first use (MCP, AKS, IaC, RBAC, etc.) | MCP expanded by PostProcessor (#142). Other acronyms inconsistent. | 🟡 Medium |
| **TM-4: Product name casing** | "Key Vault" not "key vault", "App Service" not "app service" | AI sometimes lowercases Azure product names in prose. | 🟡 Medium |

### Category 5: Consistency

| Rule | Description | Current Violation Pattern | Severity |
|------|-------------|-------------------------|----------|
| **CS-1: Term consistency** | Use the same term throughout (not "resource group" then "RG" then "resource-group") | AI alternates between hyphenated CLI forms and natural language. | 🟡 Medium |
| **CS-2: Capitalization consistency** | Consistent heading capitalization (sentence case for H2+) | Generally good from templates, but AI-generated H2s vary. | 🟢 Low |

### Category 6: Brand

| Rule | Description | Current Violation Pattern | Severity |
|------|-------------|-------------------------|----------|
| **BR-1: "Azure MCP Server" usage** | Full name on first use, can abbreviate afterward | Template handles this correctly. | 🟢 Low |
| **BR-2: Microsoft product linking** | Link to official product pages on first mention | AI sometimes fabricates or omits links. | 🟡 Medium |

### Category 7: Inclusive Language

| Rule | Description | Current Violation Pattern | Severity |
|------|-------------|-------------------------|----------|
| **IL-1: Gendered language** | Avoid "he/she", "his/her" — use "they/their" | Rare in tool docs but occurs. | 🟢 Low |
| **IL-2: Ableist language** | Avoid "simple", "easy", "just" | AI uses "simply run" or "just provide" frequently. | 🟡 Medium |
| **IL-3: Violent language** | Avoid "kill", "abort", "hit" | Rare but "kill the process" appears occasionally. | 🟢 Low |

---

## Root Cause Analysis: Why Deploy Scores 61

The Azure Deploy article is the worst performer because it combines **multiple compounding issues**:

1. **Massive JSON schema in parameter descriptions**: The `architecture_diagram_generate` tool's "Raw mcp tool input" parameter has its description set to the full JSON schema (~200 lines of `&quot;` HTML entities). Acrolinx treats every `&quot;` as a grammar/clarity violation.
2. **Run-on tool descriptions**: Each tool's description is a single paragraph of 60-100+ words.
3. **Passive voice throughout**: "The diagram is rendered from...", "The topology is scanned..."
4. **No contractions**: Formal academic style throughout.
5. **Missing introductory commas**: "To build the topology the tool scans..." (no comma after "topology").

The JSON schema issue alone likely accounts for 15-20 points of score loss.

---

## Pipeline Stage Mapping

### Where Content Is Generated vs. Where It Should Be Fixed

| Rule ID | Content Source Stage | Fix Strategy | Fix Location |
|---------|---------------------|-------------|--------------|
| **CR-1** (sentence length) | Step 4 AI cleanup | Prompt + Post-processor | System prompt instruction + `SentenceLengthChecker` |
| **CR-2** (paragraph length) | Step 4 AI cleanup | Prompt instruction | System prompt: "Split paragraphs longer than 3 sentences" |
| **CR-3** (complex sentences) | Step 4 AI cleanup | Prompt instruction | System prompt: "Use one idea per sentence" |
| **CR-4** (JSON in prose) | Step 3 template rendering | Post-processor | New `JsonSchemaCollapser` in FamilyFileStitcher |
| **CR-5** (wordy phrases) | Step 4 AI cleanup | Static replacement | Add entries to `static-text-replacement.json` |
| **CR-6** (vague qualifiers) | Step 4 AI cleanup | Prompt instruction | System prompt: "Replace vague words with specifics" |
| **GR-1** (present tense) | Step 4 AI cleanup | Prompt + Post-processor | System prompt emphasis + `TenseFixer` regex patterns |
| **GR-2** (active voice) | Step 4 AI cleanup | Prompt instruction | System prompt: "Always use active voice" |
| **GR-3** (intro commas) | Step 4 AI cleanup | Post-processor | New `IntroductoryCommaFixer` (#146) |
| **GR-5** (dangling mods) | Step 4 AI cleanup | Prompt instruction | System prompt: "Use 'you' as subject" |
| **TN-1** (contractions+) | Step 4 post-processing | Post-processor | Extend `ContractionFixer` with positive contractions |
| **TN-2** (address "you") | Step 4 AI cleanup | Prompt instruction | System prompt: "Always address reader as 'you'" |
| **TM-1** (brand names) | Step 4 post-processing | Static replacement | Expand `static-text-replacement.json` |
| **TM-2** (deprecated terms) | Step 4 post-processing | Static replacement | New `deprecated-terms.json` replacements |
| **TM-3** (acronyms) | Step 4 post-processing | Post-processor | Extend `PostProcessor.ExpandMcpAcronym()` to handle AKS, IaC, RBAC, etc. |
| **TM-4** (product casing) | Step 4 post-processing | Static replacement | Expand `static-text-replacement.json` |
| **IL-2** (ableist language) | Step 4 post-processing | Static replacement | New entries: "simply"→"", "just"→"" |

---

## Recommended Changes

### Priority 1: Prompt Changes (Step 4 System Prompt)

These changes go into `mcp-tools/prompts/tool-family-cleanup-system-prompt.txt`. Add a new section:

```
## Acrolinx Compliance (Score Target: 85+)

Apply these rules strictly — they directly impact the automated Acrolinx quality score:

### Sentence Structure
- **Maximum 25 words per sentence.** If a sentence exceeds 25 words, split it into two sentences.
- **One idea per sentence.** Don't chain multiple concepts with "which", "that", "and", or semicolons.
- **Maximum 3 sentences per paragraph.** Use a blank line to start a new paragraph.
- **Start sentences with the subject.** Use "The tool lists..." not "By using the tool, you can list..."

### Voice and Tense
- **Use present tense exclusively.** Write "lists" not "will list". Write "returns" not "will return". Write "creates" not "will create".
- **Use active voice exclusively.** Write "The tool creates a VM" not "A VM is created by the tool." Write "You specify the resource group" not "The resource group is specified."
- **Address the reader as "you."** Write "You can list clusters" not "Users can list clusters" or "The user can list clusters."

### Tone and Contractions
- **Use contractions.** Write "don't" not "do not", "isn't" not "is not", "it's" not "it is", "you're" not "you are", "doesn't" not "does not", "can't" not "cannot", "won't" not "will not", "aren't" not "are not", "you'll" not "you will", "you've" not "you have", "we're" not "we are", "there's" not "there is".
- **Use conversational language.** Avoid stiff, manual-like phrasing. Write for a developer reading docs in their IDE.
- **Never use "simply", "just", "easy", or "easily"** — these are flagged by inclusive language checks.

### Word Choice
- **Replace wordy phrases:**
  - "in order to" → "to"
  - "due to the fact that" → "because"
  - "provides the ability to" → "lets you" or "can"
  - "at this point in time" → "now"
  - "a number of" → "several" or a specific number
  - "for the purpose of" → "to" or "for"
  - "in the event that" → "if"
  - "is able to" → "can"
  - "make use of" → "use"
  - "prior to" → "before"
  - "subsequent to" → "after"
  - "utilize" → "use"
  - "whether or not" → "whether"
  - "with regard to" → "about" or "for"

- **Replace vague qualifiers with specifics:**
  - Don't write "various resources" — write "resources such as VMs, databases, and storage accounts"
  - Don't write "several options" — list the actual options
  - Don't write "certain parameters" — name the parameters

### Commas
- **Always add a comma after introductory phrases**: "After you authenticate, run the command." "If the resource group exists, the tool returns its details." "When you specify a subscription, the tool filters results."
```

### Priority 2: New Post-Processors (FamilyFileStitcher)

#### 2a. Extend ContractionFixer — Positive Contractions

Currently only covers negative contractions. Add positive forms:

```csharp
// Add to ContractionFixer.Rules array:
(BuildRule("it is"), "it's"),
(BuildRule("you are"), "you're"),
(BuildRule("you will"), "you'll"),
(BuildRule("you have"), "you've"),
(BuildRule("we are"), "we're"),
(BuildRule("we have"), "we've"),
(BuildRule("there is"), "there's"),
(BuildRule("that is"), "that's"),
(BuildRule("what is"), "what's"),
(BuildRule("here is"), "here's"),
(BuildRule("they are"), "they're"),
```

**Caution:** "It is" → "it's" must not fire inside code blocks or frontmatter. The existing backtick-avoidance logic in `BuildRule` should handle this, but test carefully.

#### 2b. New: IntroductoryCommaFixer (#146)

Add a post-processor that inserts commas after introductory subordinate clauses:

```csharp
public static class IntroductoryCommaFixer
{
    // Patterns: introductory phrase starters that need a comma
    private static readonly string[] Starters = [
        "If you", "When you", "After you", "Before you",
        "Once you", "While you", "Although you",
        "If the", "When the", "After the", "Before the",
        "To use", "To create", "To list", "To get", "To delete",
        "For example", "In this case", "By default",
    ];

    public static string Fix(string markdown)
    {
        foreach (var starter in Starters)
        {
            // Match: "If you [verb phrase without comma]" and insert comma
            // before the next clause boundary
            // Regex: starter + non-comma clause (3-12 words) + missing comma
            var pattern = $@"(?m)^({Regex.Escape(starter)}\s+\w[\w\s]{{5,50}}?)(\s+(?:the|a|an|you|it|this|that|each|all)\s)";
            markdown = Regex.Replace(markdown, pattern, "$1,$2");
        }
        return markdown;
    }
}
```

#### 2c. New: WordyPhraseFixer

Add a deterministic post-processor for the most common wordy phrases:

```csharp
public static class WordyPhraseFixer
{
    private static readonly (string Find, string Replace)[] Phrases = [
        ("in order to", "to"),
        ("due to the fact that", "because"),
        ("provides the ability to", "lets you"),
        ("at this point in time", "now"),
        ("for the purpose of", "to"),
        ("in the event that", "if"),
        ("is able to", "can"),
        ("make use of", "use"),
        ("prior to", "before"),
        ("subsequent to", "after"),
        ("whether or not", "whether"),
        ("with regard to", "about"),
        ("with respect to", "about"),
        ("in order for", "for"),
        ("on a daily basis", "daily"),
    ];

    public static string Fix(string markdown)
    {
        foreach (var (find, replace) in Phrases)
        {
            markdown = Regex.Replace(markdown,
                $@"(?i)(?<!\w){Regex.Escape(find)}(?!\w)",
                replace);
        }
        return markdown;
    }
}
```

#### 2d. New: JsonSchemaCollapser

For parameters whose descriptions are raw JSON schemas (like Deploy's "Raw mcp tool input"), collapse them to a human-readable summary:

```csharp
public static class JsonSchemaCollapser
{
    public static string Fix(string markdown)
    {
        // Detect parameter table cells containing JSON schema patterns
        // Replace with "JSON object. See [tool documentation] for the full schema."
        // Pattern: cell content starting with { and containing "type", "properties"
        var pattern = @"(\| \*\*[^|]+\*\* \|\s*Required \|)\s*\{[\s\S]*?\""type\""[\s\S]*?\""properties\""[\s\S]*?\|";
        // Collapse to concise description
        return Regex.Replace(markdown, pattern, m =>
        {
            return m.Groups[1].Value + " JSON object describing the input. See the tool's parameter schema for details. |";
        });
    }
}
```

**This single fix could add 15-20 points to the Deploy article score.**

### Priority 3: Static Text Replacement Expansions

Add these entries to `data/static-text-replacement.json`:

```json
[
    {"Parameter": "Azure Active Directory", "NaturalLanguage": "Microsoft Entra ID"},
    {"Parameter": "Azure AD", "NaturalLanguage": "Microsoft Entra ID"},
    {"Parameter": "AAD", "NaturalLanguage": "Microsoft Entra ID"},
    {"Parameter": "master branch", "NaturalLanguage": "main branch"},
    {"Parameter": "master/slave", "NaturalLanguage": "primary/secondary"},
    {"Parameter": "whitelist", "NaturalLanguage": "allowlist"},
    {"Parameter": "blacklist", "NaturalLanguage": "blocklist"},
    {"Parameter": "sanity check", "NaturalLanguage": "validation check"},
    {"Parameter": "dummy", "NaturalLanguage": "placeholder"},
    {"Parameter": "utilize", "NaturalLanguage": "use"},
    {"Parameter": "leverages", "NaturalLanguage": "uses"},
    {"Parameter": "leverage", "NaturalLanguage": "use"}
]
```

### Priority 4: Present Tense Post-Processor

Add a `TenseFixer` to catch the most common future-tense patterns:

```csharp
public static class TenseFixer
{
    private static readonly (Regex Pattern, string Replacement)[] Rules = [
        // "will list" → "lists", "will create" → "creates"
        (new Regex(@"\bwill (\w+)\b", RegexOptions.Compiled), m =>
            ConjugateThirdPerson(m.Groups[1].Value)),
        // "will be listed" → "is listed" (passive but at least present)
        (new Regex(@"\bwill be (\w+ed)\b", RegexOptions.Compiled), "is $1"),
    ];

    private static string ConjugateThirdPerson(string verb)
    {
        // Simple heuristic for regular verbs
        if (verb.EndsWith("e")) return verb + "s";
        if (verb.EndsWith("y")) return verb[..^1] + "ies";
        return verb + "s";
    }
}
```

**Caution:** This must skip content inside code blocks, quotes, and example prompts. Tense in user-facing example prompts is different (imperative mood: "List my VMs").

### Priority 5: AcronymExpander Enhancement

Extend the existing `PostProcessor.ExpandMcpAcronym()` to handle additional acronyms:

```csharp
// Acronyms to expand on first body use
private static readonly Dictionary<string, string> Acronyms = new()
{
    ["MCP"] = "Model Context Protocol (MCP)",  // existing
    ["AKS"] = "Azure Kubernetes Service (AKS)",
    ["IaC"] = "infrastructure as code (IaC)",
    ["RBAC"] = "role-based access control (RBAC)",
    ["VMSS"] = "virtual machine scale set (VMSS)",
    ["SKU"] = "stock-keeping unit (SKU)",
    ["FQDN"] = "fully qualified domain name (FQDN)",
    ["HNS"] = "hierarchical namespace (HNS)",
};
```

### Priority 6: Ableist Language Remover

Add to static-text-replacement.json or as a dedicated post-processor:

```json
[
    {"Parameter": "simply ", "NaturalLanguage": ""},
    {"Parameter": "just ", "NaturalLanguage": ""},
    {"Parameter": "easily ", "NaturalLanguage": ""},
    {"Parameter": "easy to ", "NaturalLanguage": ""}
]
```

**Note:** These need careful word-boundary handling to avoid corrupting words like "adjust" (contains "just").

---

## Implementation Priority Order

| Priority | Change | Expected Impact | Effort | Pipeline Stage |
|----------|--------|----------------|--------|---------------|
| **P0** | Prompt: Add Acrolinx compliance section to system prompt | +10-15 pts across all articles | Low (text change) | Step 4 prompt |
| **P1** | Post-proc: JsonSchemaCollapser | +15-20 pts for Deploy, +5 for others with complex params | Medium | FamilyFileStitcher |
| **P1** | Post-proc: Extend ContractionFixer (positive forms) | +3-5 pts across all articles | Low | FamilyFileStitcher |
| **P2** | Post-proc: WordyPhraseFixer | +2-4 pts across all articles | Low | FamilyFileStitcher |
| **P2** | Static replacement: Deprecated terms + brand names | +2-3 pts for terminology score | Low | static-text-replacement.json |
| **P2** | Post-proc: IntroductoryCommaFixer | +1-3 pts grammar score | Medium | FamilyFileStitcher |
| **P3** | Post-proc: TenseFixer (will→present) | +2-4 pts grammar score | Medium-High (edge cases) | FamilyFileStitcher |
| **P3** | Post-proc: AcronymExpander enhancement | +1-2 pts terminology score | Low | PostProcessor |
| **P3** | Static replacement: Ableist language removal | +1-2 pts inclusive language | Low (needs boundary care) | static-text-replacement.json |
| **P4** | Post-proc: SentenceLengthWarner (log, don't fix) | Diagnostic only | Low | Validation gate |

**Estimated total impact with P0+P1+P2:** +20-30 points for the worst articles (Deploy 61→80+), +10-15 for borderline articles.

---

## What Cosmos DB (Score 92) Does Right

Cosmos DB is our highest-scoring article. Analyzing what works:

1. **Short, focused descriptions**: Each tool description is 2-3 sentences, under 25 words each.
2. **Active voice throughout**: "Lists accounts", "Queries items", "Returns results".
3. **Present tense**: "The tool lists..." not "The tool will list..."
4. **Contractions used**: "doesn't", "isn't" applied by ContractionFixer.
5. **MCP acronym expanded**: First body mention says "Model Context Protocol (MCP)".
6. **No JSON schemas in parameter cells**: Simple parameter names with clear descriptions.
7. **Sentence case headings**: "List accounts, databases, and containers" not "LIST ACCOUNTS".
8. **Proper branding**: "Azure Cosmos DB" consistently (from static replacement).

This is the template all articles should follow.

---

## Known Gaps Not Addressed Here

1. **Step 6 (Horizontal Articles)**: Different template and generation path. Needs separate Acrolinx analysis once Step 6 post-validators exist (see AD-021).
2. **Step 2 (Example Prompts)**: Example prompts are embedded in tool family articles but are wrapped in quotes — Acrolinx may flag them as sentence fragments. These are intentional and may need Acrolinx suppression markers.
3. **Include files**: Already score 96. No action needed.
4. **Per-namespace instructions**: Some namespaces may need service-specific terminology fixes (e.g., Foundry branding). These should go in the `instruction-generation/` prompt system, not in universal post-processors.

---

## Testing Strategy

Per AD-007 and AD-010, every post-processor needs tests:

1. **ContractionFixer extension**: Add test cases for "it is"→"it's", verify no firing in backticks/frontmatter.
2. **IntroductoryCommaFixer**: Test with "If you run the command the tool lists resources" → comma inserted.
3. **WordyPhraseFixer**: Test "in order to create" → "to create"; verify no partial matches.
4. **JsonSchemaCollapser**: Test with actual Deploy article parameter cell content.
5. **TenseFixer**: Test "will list" → "lists"; verify no firing in example prompts or quotes.
6. **AcronymExpander**: Test that each acronym expands only on first body occurrence, not in frontmatter or code.
7. **Static replacements**: Test "Azure Active Directory" → "Microsoft Entra ID" with surrounding context.

All tests should use the existing `CallbackProcessRunner`/mock pattern and realistic inputs from actual generated articles.

---

## Appendix: PR Sources

| PR | URL | Key Findings |
|----|-----|-------------|
| #8641 | [Cosmos DB](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8641) | Score 88→92. Shows what good looks like. |
| #8668 | [Compute](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8668) | Score 74→85 over 15+ iterations. Clarity improvements key. |
| #8747 | [Monitor](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8747) | Score 76. Multi-namespace complexity. |
| #8748 | [File Shares](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8748) | Score 76. Typical mid-range failures. |
| #8749 | [App Lens](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8749) | Score 82. Passes but barely. |
| #8750 | [Deploy](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8750) | Score 61. JSON schema problem. |
| #8751 | [Postgres](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8751) | Score 64. Long descriptions. |
| #8753 | [AI Search](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8753) | Score 78. Borderline. |
| #8754 | [WAF](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8754) | Score 83. Passes. |
| #8755 | [Cloud Architect](https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8755) | Score 67. Similar to Deploy problems. |
