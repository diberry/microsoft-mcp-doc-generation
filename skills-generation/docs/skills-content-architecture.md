# Skills Content Architecture: Skill Folder → Published Article

> **Created:** 2026-06-04, Wednesday 3:13 PM PT  
> **Goal:** H2 1.2 — Azure Skills content generation pipeline  
> **Status:** Draft  
> **Pipeline repo:** `diberry/microsoft-mcp-doc-generation` (`skills-generation/`)  
> **Output:** Single `.md` article per skill for Microsoft Learn

---

## Purpose

Define a clear conceptual model for transforming a skill folder into a published content article. The model answers:

1. **What content is user-facing** (belongs in the article)?
2. **What content is implementation detail** (excluded from the article)?
3. **How do we audit** skill folders and generated articles against this model?

This architecture is the **conceptual layer above** the existing SYNTHESIS-SPEC. It does not replace SYNTHESIS-SPEC — that document remains the operational source of truth for heading mappings, parser behavior, and rendering rules.

---

## What Stays The Same

- The existing generation pipeline (.NET CLI, `SkillPipelineOrchestrator`, 10-stage execution) continues to work.
- SYNTHESIS-SPEC remains authoritative for concrete heading mappings, parser behavior, and rendering rules.
- The 25 currently generated articles are preserved unless separately changed.
- Pipeline components (`SkillMarkdownParser`, `TierAssessor`, `AzureOpenAiRewriter`, `SkillPageGenerator`, `AcrolinxPostProcessor`, `SkillPageValidator`) remain the implementation.
- Output format: single `.md` article per skill.
- Curated data overlays (`skill-what-it-provides.json`, `skill-example-prompts.json`, `skill-related-links.json`) continue to provide highest-priority overrides.

---

## The Canonical Skill Content Model

A skill folder contains skill folder content across four categories plus one transient processing state:

```
Skill Folder
├─ Identity Metadata          (what is this skill?)
├─ User-Facing Content        (what goes in the article)
├─ Conditional Content        (may go in article based on policy/tier)
├─ Implementation Details     (excluded from article)
└─ Unmapped Source Material   (unclassified — flagged for review)
```

**Note:** Unmapped Source Material is a transient processing state, not a permanent category. All content eventually resolves to User-Facing, Conditional, or Implementation Detail through the classification review process.

### Classification Rule

> **A datum is User-Facing if it helps the user decide whether to use the skill, prepare to use it, invoke it correctly, or understand expected outcomes.**
>
> **A datum is an Implementation Detail if it tells the agent how to execute the work after activation.**

When content is borderline, ask: "Would a user who has never seen the skill's internals need this information to get value from the skill?" If yes → User-Facing or Conditional. If no → Implementation Detail.

```text
Content → Does it help the user decide/prepare/invoke/understand?
  YES → Is it always needed for minimum viable article?
    YES → User-Facing (Required)
    NO → Conditional (policy/tier gated)
  NO → Implementation Detail (excluded)
```

---

## Identity Metadata

Metadata used by both parsing and rendering. Not a content section itself — it drives titles, frontmatter, routing, and inventory.

| Field | Source | Fallback |
|-------|--------|----------|
| Slug (name) | YAML `name` field | Directory name |
| Display name | `skills-inventory.json` `displayName` | `display_name` frontmatter → derive from slug |
| Version | YAML `metadata.version` | `skills-inventory.json` `skillVersion` |
| Category | `skills-inventory.json` `category` | None (uncategorized) |
| Source repo URL | Computed from slug | — |

---

## User-Facing Content (Required)

These sections MUST appear in every generated article. They represent the minimum viable article that lets a user understand and invoke the skill.

| Conceptual Field | What It Answers | Source in Skill Folder | Article Section |
|---|---|---|---|
| **Description** | What does this skill do? | H1 body text, LLM-polished | Intro paragraph |
| **Features & Results** | What does it produce/provide? | Services, capabilities → LLM synthesis | `## What it provides` |
| **Prerequisites** | What must exist before I use it? | `## Prerequisites`, `## Required Inputs`, RBAC, inline detection | `## Prerequisites` |
| **When to use** | When should I invoke this? | `USE FOR:` markers, `## When to use` body | `## When to use this skill` |
| **When NOT to use** | What should I use instead? | `DO NOT USE FOR:` markers, `## Do not use` body | `### When not to use this skill` |
| **Example prompts** | What do I say to trigger it? | `triggers.test.ts` shouldTrigger (upstream), curated JSON | `## Example prompts` |
| **Related content** | Where can I learn more? | Curated links JSON, default Learn links | `## Related content` |

### Prerequisites Detail

Prerequisites are the primary hybrid extraction case:

- **Explicit sections** (`## Prerequisites`, `## Required Inputs`) → deterministic lift
- **RBAC roles** (`## Required Roles`, inline role mentions) → deterministic lift
- **Scattered inline mentions** ("requires X", "must have X") → LLM synthesis
- **Compatibility requirements** (`compatibility:` frontmatter) → deterministic lift
- **Curated overrides** (planned `skill-prereqs.json`) → highest priority

### Safety Behavior (External Summary)

User-visible safety behavior is user-facing content:
- "This skill asks for confirmation before destructive operations"
- "This skill requires validated deployment plan before executing"

The exact guardrail rules, confirmation protocol text, and gating implementation remain internal.

---

## Conditional User-Facing Content

These sections appear when source data exists AND policy allows. They provide additional value but aren't required for a minimum viable article.

| Conceptual Field | Condition | Source | Article Section |
|---|---|---|---|
| **Related skills** | Source has `@azure-*` references | `## Related Skills` | `## Related skills` |
| **Automatic activation** | Source has detection markers | Codebase detection patterns | `## Automatic activation` |
| **Decision guidance** | Tier 1 skill AND data exists | `## Decision Guidance` sub-headings | `## Decision guidance` |

### Tier Assessment

Skills are scored (0–7) to determine Tier 1 (comprehensive, ≥4) vs Tier 2 (essential). Tier determines which conditional sections render.

---

## Implementation Details (Excluded)

These are in the skill folder but NEVER appear in the published article. They exist to instruct the AI agent during execution.

| Source Content | Why It's Internal |
|---|---|
| **Procedure / Steps** (`## Steps`, `## Workflow`) | Execution recipe for the agent |
| **MCP tool calls** (`## MCP Tools`, `## Tools`) | Agent-side API calls |
| **Behavioral rules** (`## Rules`) | Agent behavioral constraints |
| **Sub-skill delegation** (sub-directories with SKILL.md) | Internal orchestration |
| **Safety guardrail enforcement** (exact rule text, gating logic) | Agent protocol, not user instruction |
| **Reference files** (`references/`) — default disposition | Detailed execution context for the agent |
| **Recipes** (`references/recipes/`) | Step-by-step agent procedures |
| **SDK quick-references** (`references/sdk/`) | Condensed SDK context for agent use |

### References: Source Location ≠ Policy

Content from `references/` is internal **by default**, but facts may be synthesized into user-facing content if they directly affect user preparation, invocation, or expected results. The pipeline may distill facts from reference files into Prerequisites or Features — but reference files are never exposed directly.

---

## Unmapped Source Material

Any heading or content not matched by the parser's known patterns falls here. The architecture requires:

1. **Detection:** Parser reports unrecognized H2/H3 headings per skill in pipeline run output
2. **Default disposition:** Excluded until explicitly classified
3. **Review queue:** Unmapped headings are flagged for human classification by the content owner during the monthly freshness audit
4. **Resolution:** Classified headings get added to SYNTHESIS-SPEC §1.4 heading inventory
5. **SLA:** No hard SLA — unmapped headings become backlog items, not publication blockers

## Edge Cases & Failure Handling

### Empty user-facing content

If a skill folder has no content matching any user-facing pattern, the pipeline generates a **stub article** containing identity metadata only and explicit flags for manual authoring. The pipeline never publishes an empty article.

### Missing example prompts

If `triggers.test.ts` is unavailable and no curated prompts exist, the article still publishes. The `## Example prompts` section renders with the placeholder **"No example prompts available yet"** and the skill is flagged for prompt authoring.

### Classification drift control

Classification decisions are recorded in SYNTHESIS-SPEC §1.4 heading inventory. Once a heading is classified, its disposition is deterministic. New or unrecognized headings default to excluded until explicitly classified.

### Tier downgrade

If a skill loses Tier 1 status on reassessment, conditional Tier 1 sections are removed on the next regeneration. Published articles always reflect the current tier; there is no grandfather clause.

### Unmapped heading review

Unmapped headings are reported in pipeline run output. Review responsibility sits with the content owner during the monthly freshness audit. There is no hard SLA — this is a backlog item, not a blocker.

### SYNTHESIS-SPEC conflict (current state)

If this document and SYNTHESIS-SPEC disagree today, SYNTHESIS-SPEC §4.2 wins for rendering behavior until this architecture is formally adopted and SYNTHESIS-SPEC is updated. This document is the target state; SYNTHESIS-SPEC is the current state.

### Sub-skill contribution

When a parent skill's value depends on sub-skills, the parent article may reference sub-skill capabilities in `## What it provides` as synthesized features. Sub-skill internal structure, delegation flow, and orchestration details remain excluded.

### Audit failure threshold

A skill fails audit if **any** check in category C (User-Facing Coverage) has zero substantive content and no documented exclusion or degradation reason. Checks A, B, and D-G produce warnings, not failures.

---

## Pipeline Transform

### Conceptual Model (4 phases)

The pipeline is described conceptually as 4 phases. This is a simplification of the actual implementation (see Implementation Detail below) for reasoning about content flow.

```
┌──────────────────────────────────────────────────────────────────┐
│  SKILL FOLDER                                                     │
│  SKILL.md + references/ + triggers.test.ts*                       │
│  * triggers.test.ts lives in upstream repo, not always in local   │
│    sparse checkout                                                 │
└──────────────────────┬───────────────────────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────────────────────┐
│  1. PARSE — Extract all content into typed model                  │
│     Identity → IdentityMetadata                                   │
│     Headings → Classified sections (User/Conditional/Internal)    │
│     Triggers → ExamplePrompts                                     │
└──────────────────────┬───────────────────────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────────────────────┐
│  2. CLASSIFY — Apply disposition to each section                  │
│     User-Facing → render                                          │
│     Conditional → render if policy/tier allows                    │
│     Internal → exclude                                            │
│     Unmapped → exclude + flag                                     │
└──────────────────────┬───────────────────────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────────────────────┐
│  3. ENRICH — Apply curated overrides + LLM polish                 │
│     Curated JSON > LLM synthesis > deterministic extraction       │
│     Tier assessment → conditional section gating                  │
└──────────────────────┬───────────────────────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────────────────────┐
│  4. RENDER — Template produces single article                     │
│     Only User-Facing + allowed Conditional sections               │
│     Identity metadata → frontmatter + title                       │
└──────────────────────────────────────────────────────────────────┘
```

### Implementation Detail (10 actual orchestrator stages)

The `SkillPipelineOrchestrator` (in `SkillsGen.Core/Orchestration/SkillPipelineOrchestrator.cs`) executes 10 discrete steps:

| # | Stage | Conceptual Phase | What It Does |
|---|-------|-----------------|--------------|
| 1 | **Fetch** | Parse | Retrieve `SKILL.md` + `references/` from local path or GitHub; fetch `triggers.test.ts` separately when needed because it is not in the local sparse checkout |
| 2 | **Catalog** | Parse | Match skill against `skills-inventory.json`, resolve display name |
| 3 | **Parse** | Parse | `SkillMarkdownParser` extracts frontmatter, headings, sections into typed model |
| 4 | **Assess** | Classify | `TierAssessor` scores skill (0–7), assigns Tier 1 or Tier 2 |
| 5 | **LLM: Rewrite Intro** | Enrich | `AzureOpenAiRewriter.RewriteIntroAsync()` — polishes intro paragraph |
| 6 | **LLM: Synthesize What-It-Provides** | Enrich | `AzureOpenAiRewriter.SynthesizeWhatItProvidesAsync()` — generates features section |
| 7 | **Generate** | Render | `SkillPageGenerator` applies Handlebars template with context flags |
| 8 | **Post-Process** | Render | `AcrolinxPostProcessor` applies style rules |
| 9 | **Validate** | Render | `SkillPageValidator` checks required sections present |
| 10 | **Write** | Render | Output `.md` file to target directory |

*Wiring note:* `Program.cs:134-226` composes `SkillMarkdownParser`, `TriggerTestParser`, `TierAssessor`, `AzureOpenAiRewriter`, `SkillPageGenerator`, `AcrolinxPostProcessor`, and `SkillPageValidator` into `SkillPipelineOrchestrator`.

### Exclusion Enforcement Mechanism

Excluded sections (MCP tools, sub-skills, workflow) are suppressed at the **generator level**, not the template level:

```csharp
// SkillPageGenerator.cs — context flags
hasMcpTools = false;      // Template block exists but never renders
hasSubSkills = false;     // Template block exists but never renders  
showWorkflow = false;     // Template block exists but never renders
```

The Handlebars template (`templates/skill-page-template.hbs`) still contains conditional blocks for these sections, but the generator always passes `false` for their display flags. This means:
- ✅ Exclusion is enforced in practice
- ⚠️ Template contains dead code (blocks that can never render under current policy)
- Future policy changes would only require flipping flags in the generator, not template edits

---

## Audit / Conformance Model

The architecture defines these repeatable conformance checks:

### Audit Severity & Thresholds

- **Blocking failure:** Category **C. User-Facing Coverage**. A skill fails audit if any required section has zero substantive content and no documented exclusion or degradation reason.
- **Warnings only:** Categories **A, B, D, E, F, and G**. These findings must be reported and tracked, but they do not fail the audit on their own.
- **Graceful degradation:** `## Example prompts` may pass with the placeholder **"No example prompts available yet"** when the skill is flagged for prompt authoring.

### A. Inventory Coverage

Every skill folder in `microsoft/azure-skills/skills/` has a status:
- ✅ **Generated** — article exists in output
- ⏸️ **Excluded with reason** — documented reason for exclusion (e.g., too new, sub-skill only)
- 🔲 **Pending** — not yet in `skills-inventory.json`

### B. Identity Completeness

For each skill in inventory:
- [ ] Slug present and valid
- [ ] Display name resolvable (any fallback level)
- [ ] Version present or derivable
- [ ] Category assigned

### C. User-Facing Coverage

Generated article has all required sections with substantive content:
- [ ] Intro paragraph (non-empty, ≥1 sentence)
- [ ] `## What it provides` (non-empty)
- [ ] `## Prerequisites` (non-empty OR explicit "none required")
- [ ] `## When to use this skill` (≥1 bullet)
- [ ] `## Example prompts` (≥1 prompt OR placeholder plus prompt-authoring flag)
- [ ] `## Related content` (≥1 link)

### D. Internal Leakage Check

Generated article contains NONE of the following:
- [ ] MCP tool names (e.g., `azmcp_*`, `azure__*`)
- [ ] Workflow step tables with numbered execution sequences
- [ ] Sub-skill orchestration references (internal `@azure-*` chaining instructions)
- [ ] Raw agent rule text ("MUST", "FORBIDDEN", agent-voice instructions)
- [ ] Direct reference file paths (`references/*.md`)

### E. Conditional Rendering Correctness

- [ ] Automatic activation renders only when detection markers exist in source
- [ ] Decision guidance renders only for Tier 1 skills with `## Decision Guidance` data
- [ ] Related skills renders only when `@azure-*` references found

### F. Unmapped Heading Report

- [ ] List of H2/H3 headings in source not covered by parser patterns
- [ ] Each has disposition: pending review, confirmed internal, or promoted to user-facing

### G. Source Sufficiency

For each skill, skill folder contains enough material for required sections:
- [ ] Description extractable from frontmatter + H1 body
- [ ] At least one prerequisite source (explicit section, RBAC, or inline)
- [ ] At least one use-case source (USE FOR markers or body section)
- [ ] At least one example prompt source (triggers.test.ts or curated JSON)

### Suggested Audit Output Format

Produce a matrix:

| Skill | Identity ✓ | Source Sufficient ✓ | Article Exists ✓ | Coverage ✓ | No Leakage ✓ | Conditional ✓ | Unmapped ✓ |
|-------|---|---|---|---|---|---|---|

---

## Relationship to SYNTHESIS-SPEC

| Concern | Governed By |
|---------|-------------|
| Conceptual classification (external/internal/conditional) | **This document** |
| Classification rule (decision test) | **This document** |
| Audit criteria and conformance checks | **This document** |
| Concrete heading patterns and regex | SYNTHESIS-SPEC §2.4 |
| Extraction fallback chains | SYNTHESIS-SPEC §2.8 |
| Content policy (which sections render) | SYNTHESIS-SPEC §4.2 |
| Writing style rules | SYNTHESIS-SPEC §4.4 |
| Parser implementation details | SYNTHESIS-SPEC (references to .cs files) |

**Governance model:** This document defines the target architecture. SYNTHESIS-SPEC defines current implementation behavior. During the transition period, SYNTHESIS-SPEC governs runtime behavior. When this architecture is formally adopted (via team decision), SYNTHESIS-SPEC will be updated to align. Until then, discrepancies are tracked as **alignment gaps** in the audit report, not treated as errors.

If this architecture and SYNTHESIS-SPEC disagree today, SYNTHESIS-SPEC §4.2 wins for current rendering behavior. This document remains the target-state model for future alignment.

---

## Next Steps

1. **Run the audit** — Apply checks A-G against current skill folders and generated output
2. **Resolve gaps** — Address missing inventory entries, source sufficiency warnings, and alignment gaps
3. **Validate leakage** — Confirm generated articles have no internal content
4. **PG template review** — Use this model as shared vocabulary in PG partnership sync — specifically validate that PG's SKILL.md authoring guidance aligns with the user-facing/internal classification
5. **Writer handoff test** — Have another writer run the pipeline using the ops guide

---

## Glossary

- **SYNTHESIS-SPEC** — The current implementation spec that defines parser mappings, fallback chains, and rendering behavior.
- **LLM** — Large language model used for limited rewrite and synthesis steps in the pipeline.
- **RBAC** — Role-based access control requirements surfaced as prerequisites when relevant.
- **PG** — Product group partner responsible for upstream skill authoring guidance.
- **Tier 1 / Tier 2** — Skill classification levels that control whether richer conditional sections render.
- **Curated overlay** — Maintained JSON input that overrides or supplements extracted skill folder content.
- **Sparse checkout** — Partial local clone strategy where only selected upstream paths are present on disk.

---

## Open Questions

1. Should safety behavior summaries be required (always in Prerequisites) or conditional?
2. Should `## Decision guidance` remain Tier-1-only or become universally conditional?
3. Should the audit tooling be a script in the pipeline repo or a Copilot skill?
