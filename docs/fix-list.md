# Content Generation System — Handoff Prompts

**From:** Content maintenance squad (Dina Berry, owner)
**To:** Content generation system squad
**Date:** 2026-03-08
**Version context:** Issues discovered during v2.0.0-beta.23 content review cycle
**Namespaces reviewed:** `cosmos`, `extension`

## How to Use This Document

Each section below is a **self-contained prompt** describing one issue found in the content generation system's output. The prompts are ordered by priority:

- **P1** — Content is wrong or misleading. Readers get incorrect information.
- **P2** — Content is incomplete. Information is missing or degraded.
- **P3** — Content quality/polish. Correct but could be better.

Each prompt includes the specific gen system file paths where the fix likely lives. All paths are relative to the content generation repo root (`microsoft-mcp-doc-generation`). Refer to the generation output structure:

| Output type | Path pattern |
|---|---|
| Tool family file | `generated-{namespace}/tool-family/{namespace}.md` |
| Metadata | `generated-{namespace}/tool-family-metadata/{namespace}-metadata.md` |
| Related content | `generated-{namespace}/tool-family-related/{namespace}-related.md` |
| Horizontal article | `generated-{namespace}/horizontal-articles/horizontal-article-{namespace}.md` |
| CLI data | `generated-{namespace}/cli/cli-output.json`, `cli-namespace.json` |

**Guiding principle:** Each fix should be the **smallest possible change** to the generation system. Do not refactor surrounding code — fix the specific issue.

---

## Prompt 1 — Broken Cross-References in Horizontal Articles

**Priority:** P1 — Content is wrong

**What's wrong:** The horizontal (service) article for the `cosmos` namespace contains links to the tool-family page using the generated filename `cosmos.md`. The actual published filename on Microsoft Learn is `azure-cosmos-db.md`. All three cross-reference links in the generated horizontal article are broken.

**Example (beta.23):**
- Generated file: `generated-cosmos/horizontal-articles/horizontal-article-cosmos.md`
- Contains links like: `[Cosmos DB tools](cosmos.md)`
- Correct link target: `azure-cosmos-db.md`

**Where the fix likely lives:**
- The horizontal article template constructs tool-page links using the namespace name (`{namespace}.md`). It needs to use the actual published filename instead.
- The mapping from namespace → published filename is: `cosmos` → `azure-cosmos-db.md`, `extension` → `azure-compliance-quick-review.md`, `appservice` → `azure-app-service.md`, etc.
- For existing articles, this mapping may already exist in the system (https://github.com/MicrosoftDocs/azure-dev-docs-pr in ./articles/azure-mcp-server/tools) (since `generated-cosmos/tool-family/` produces `azure-cosmos-db.md` as the filename). The horizontal article template just isn't using it.
- For new articles where the tool and service files are generated in the same initial PR, perhaps the tool and service file names should be generated into the branding json file? 

---

## Prompt 2 — Phantom Service Link for Catch-All Namespaces

**Priority:** P1 — Content is wrong

**What's wrong:** The generated tool-family file for the `extension` namespace includes an intro paragraph that links to a non-existent service documentation page at `/azure/extension/`. The `extension` namespace is a catch-all bucket — it does not correspond to a single Azure service. There is no `/azure/extension/` page on Microsoft Learn.

**Example (beta.23):**
- Generated file: `generated-extension/tool-family/extension.md`
- Intro text contains a link like: `[Azure Extension](/azure/extension/)`
- This URL does not resolve on learn.microsoft.com

**Where the fix likely lives:**
- The template or logic that generates the intro paragraph in tool-family files. It likely constructs the service doc link as `/azure/{namespace}/` by default.
- For catch-all namespaces (where one namespace contains unrelated tools), this default link pattern produces invalid URLs.
- The related content generation at `generated-extension/tool-family-related/extension-related.md` may also produce this phantom link.

---

## Prompt 3 — Wrong Heading Name for Catch-All Namespace Tools

**Priority:** P1 — Content is misleading

**What's wrong:** The generated tool section heading for the azqr tool uses the namespace name instead of the tool's actual name. The heading reads "Use Azure Extension" when it should reference the specific tool (e.g., "Run Azure Compliance Quick Review").

**Example (beta.23):**
- Generated file: `generated-extension/tool-family/extension.md`
- Heading: `## Use Azure Extension`
- Expected: `## Run Azure Compliance Quick Review` (or similar tool-specific heading)

**Where the fix likely lives:**
- The tool-family template constructs section headings using a pattern like `Use Azure {Namespace}`. For catch-all namespaces, this produces a meaningless heading because "Extension" is a grouping name, not a tool or service.
- The heading should derive from the tool's display name or description in `cli-output.json`, not the namespace name.

---

## Prompt 4 — "overview" Appended to Generated Titles

**Priority:** P2 — Content is incomplete (title is degraded)

**What's wrong:** Generated tool-family files have the word "overview" appended to the `title` field in the YAML frontmatter and/or the H1 heading. Microsoft Learn tool reference pages should not include "overview" — that word is reserved for actual overview/landing pages.

**Example (beta.23):**
- Generated title: `"Azure Cosmos DB tools for Azure MCP Server overview"`
- Expected title: `"Azure Cosmos DB tools for Azure MCP Server"`

**Where the fix likely lives:**
- The title construction logic in the metadata template (`generated-{namespace}/tool-family-metadata/{namespace}-metadata.md`). The word "overview" is being appended as a suffix — likely a template string like `"{ServiceName} tools for Azure MCP Server overview"`.
- Remove "overview" from the title template for tool-family files.

---

## Prompt 5 — Duplicate `@mcpcli` HTML Comment Markers

**Priority:** P2 — Content quality

**What's wrong:** Generated tool-family files contain duplicate `<!-- @mcpcli {namespace} {tool} -->` HTML comment markers within the same tool section. Each tool section should have exactly one marker. These markers are used by the content team for cross-referencing against `azmcp-commands.md`.

**Example (beta.23):**
- Generated file: `generated-cosmos/tool-family/cosmos.md`
- A tool section contains:
  ```
  <!-- @mcpcli cosmos list -->
  ...content...
  <!-- @mcpcli cosmos list -->
  ```
- Expected: Only one `<!-- @mcpcli cosmos list -->` per tool section

**Where the fix likely lives:**
- The tool-family template that inserts HTML comment markers. It likely runs the marker insertion logic more than once per tool section — possibly once in a header and once in a body section of the template.

---

## Prompt 6 — Potentially Outdated Quickstart URL in Horizontal Articles

**Priority:** P2 — Content may be wrong

**What's wrong:** The Cosmos DB horizontal article contains a quickstart link that may use an outdated SQL API path. Microsoft Learn restructured Cosmos DB documentation, and the old quickstart URLs may redirect or 404.

**Example (beta.23):**
- Generated file: `generated-cosmos/horizontal-articles/horizontal-article-cosmos.md`
- Contains a quickstart link (specific URL not confirmed — needs verification)
- The current correct quickstart path for Cosmos DB NoSQL is: `/azure/cosmos-db/nosql/quickstart-portal`

**Where the fix likely lives:**
- The related content or template data that populates quickstart links in horizontal articles. These links may be hardcoded in the template or stored in a configuration file.
- Check `generated-cosmos/tool-family-related/cosmos-related.md` or the horizontal article template for the quickstart URL source.
---

## Prompt 7 — Horizontal Articles Miss Conditional Behavior from `azmcp-commands.md`

**Priority:** P2 — Content is incomplete

**What's wrong:** Horizontal (service) articles summarize tool capabilities, but they don't account for conditional parameters or behavior documented in `azmcp-commands.md` that aren't present in `cli-output.json`. The generation system only reads `cli-output.json` for tool behavior, but `azmcp-commands.md` sometimes contains additional conditional logic (e.g., "parameter X is only required when Y is set").

**Example (beta.23):**
- `azmcp-commands.md` documents conditional parameters for several tools
- `generated-cosmos/cli/cli-output.json` does not include these conditions
- The horizontal article at `generated-cosmos/horizontal-articles/horizontal-article-cosmos.md` summarizes capabilities without noting conditions

**Where the fix likely lives:**
- The horizontal article generation reads from `cli-output.json` only. It should also reference `azmcp-commands.md` data (the gen system already produces `generated-{namespace}/cli/azmcp-commands.json` which may contain this information).
- If `azmcp-commands.json` is already being generated, the horizontal article template should cross-reference it for conditional behavior.

**Correct behavior:** When a tool has conditional parameters or behavior documented in `azmcp-commands.md` (or its local derivative `azmcp-commands.json`), the horizontal article's capability summary should reflect those conditions rather than presenting a flattened view.


---

## Prompt 8 — Redundant Intro Paragraphs

**Priority:** P3 — Content polish

**What's wrong:** Generated tool-family files contain redundant introductory text — the same information appears in both the intro paragraph and the first tool section's description. This makes the page feel repetitive.

**Example (beta.23):**
- Generated file: `generated-extension/tool-family/extension.md`
- Intro paragraph describes what the tool does
- First tool section repeats substantially the same description

**Where the fix likely lives:**
- The tool-family template has separate sections for "page intro" and "tool description." Both pull from similar source data (likely the namespace description in `cli-namespace.json`).
- The intro should provide a high-level page summary, while tool sections should describe specific tool capabilities.

---


## Summary Table

| # | Issue | Priority | Root cause area | Reported by |
|---|---|---|---|---|
| 1 | Broken cross-references in horizontal articles | P1 | Horizontal template link construction | Kermit, Gonzo |
| 2 | Phantom service link for catch-all namespaces | P1 | Intro template link construction | Kermit, Rowlf |
| 3 | Wrong heading for catch-all namespace tools | P1 | Tool section heading template | Rowlf |
| 4 | "overview" appended to titles | P2 | Title template string | Gonzo |
| 5 | Duplicate `@mcpcli` comment markers | P2 | Tool section template | Gonzo, Rowlf |
| 6 | Potentially outdated quickstart URL | P2 | Related content / template config | Rowlf |
| 7 | Horizontal articles miss conditional behavior | P2 | Horizontal template data sources | Kermit |
| 8 | Redundant intro paragraphs | P3 | Page intro vs tool description template | Gonzo |

**P1 total:** 3 prompts (wrong/misleading content)
**P2 total:** 4 prompts (incomplete content)
**P3 total:** 1 prompt (polish)
