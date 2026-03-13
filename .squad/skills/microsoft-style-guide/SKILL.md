---
name: "microsoft-style-guide"
description: "Microsoft Writing Style Guide conventions for technical documentation on Microsoft Learn"
domain: "documentation"
confidence: "high"
source: "https://github.com/coreai-microsoft/doc-review-agent/pull/40 and https://learn.microsoft.com/style-guide/welcome/"
---

## Context

All content produced by this team targets Microsoft Learn. Every agent that writes, edits, or reviews documentation MUST follow these conventions. Source: [Microsoft Writing Style Guide](https://learn.microsoft.com/style-guide/welcome/).

## Voice & Tone

- **Warm and relaxed** — write like a knowledgeable friend, not a textbook.
- **Confident but not arrogant** — state facts directly; avoid hedging
  ("To do X, run..." not "You might want to consider running...").
- **Optimistic and encouraging** — focus on what the reader *can* do.

## Person & Voice

- **Second person ("you")** — address the reader directly.
  ✅ "You create a resource group."
  ❌ "The user creates a resource group." / "One creates a resource group."
- **Active voice** — make it clear who does what.
  ✅ "Run the following command to create the app."
  ❌ "The following command should be run to create the app."
- **Present tense** for instructions.
  ✅ "The CLI displays the results." / "This throws an error."
  ❌ "The CLI will display the results." / "This will throw an error."
  Rewrite every "will + verb" to simple present tense.

## Headings & Structure

- **Sentence case** for ALL headings (H1 through H4) — capitalize only the first
  word and proper nouns (product names like Azure, Node.js, Microsoft Entra).
  ✅ "Authenticate apps to Azure services"
  ❌ "Authenticate Apps to Azure Services"
  Common nouns like "apps", "services", "group", "roles" stay lowercase.
- **Use headings to help readers scan** — every major task gets its own heading.
- **Keep paragraphs short** — aim for 2-3 sentences per paragraph.

## Sentences & Word Choice

- **Short sentences** — 25 words or fewer is ideal; break long sentences apart.
- **One idea per sentence** — don't chain multiple instructions with semicolons.
- **"Select" not "click"** — device-neutral language.
  ✅ "Select **Save**."  ❌ "Click the Save button."
- **"On the left" not "in the left-hand navigation pane"** — be direct.
- **Avoid jargon** without first explaining it. Spell out acronyms on first use:
  CLI (command-line interface), API (application programming interface),
  SDK (software development kit). Use "Microsoft Entra" on first reference;
  "Entra" alone is acceptable after that.
- **Use simple words** — "use" not "utilize", "start" not "initiate",
  "about" not "approximately".

## Formatting

- **Bold** for UI elements: "Select **Create**", "On the **Overview** page".
- **Backticks** for code, commands, parameter names, and values:
  "Run `az group create`", "Set `--location` to `eastus2`".
- **Numbered lists** for sequential steps; **bulleted lists** for non-ordered items.
- **One command per step** — don't combine multiple commands in a single step
  unless they are tightly coupled (e.g., `cd` then `npm install`).

## Inclusive & Bias-Free Language

- **Gender-neutral** — use "they/them" for singular third person.
- **No ableist language** — avoid "easy", "simple", "just", "obviously" in ALL
  contexts, including temporal "just" ("if you just added" → "if you recently added").
  ✅ "Run the command."  ❌ "Simply run the command."
  ✅ "If you recently created the group..."  ❌ "If you just created the group..."
- **Global-ready** — avoid idioms, slang, and culturally specific references.

## Procedures & Instructions

- **Start steps with a verb** — "Create a file...", "Open the terminal...".
- **Include expected output** after commands so readers can verify success.
- **State prerequisites before the procedure**, not inline.
- **Use "if" to describe conditions** — "If the command fails, run..."
  not "Should the command fail, run...".

## Scoring (for reviews)

When reviewing documentation for Clarity (scored out of 10), deduct points for:
- Consistent passive voice
- Third-person references to the reader
- Title Case headings (instead of sentence case)
- Unnecessarily complex language
- Ableist minimizers ("simply", "just", "easy", "obviously")
- "will + verb" instead of present tense

## References

- [Microsoft Writing Style Guide](https://learn.microsoft.com/style-guide/welcome/)
- [doc-review-agent PR #40](https://github.com/coreai-microsoft/doc-review-agent/pull/40)
