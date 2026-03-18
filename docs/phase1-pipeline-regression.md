# Phase 1 pipeline regression verification

Date: 2026-03-15  
Requested by: Dina  
Branch verified: `squad/phase1-ai-abstraction`

## Scope
I re-ran the generation pipeline against the two namespaces requested for regression checking:
- `workbooks` (baseline 5/5/5)
- `extensionfoundry` baseline check, executed with the runnable namespace key `foundryextensions` (baseline 11/11/11)

## How I ran it
I used the typed pipeline runner so bootstrap could prepare the CLI metadata before namespace generation. The direct single-script entry point was not sufficient by itself because it expected prebuilt metadata.

## Results

### 1) workbooks
**Outcome:** Counts match baseline, but output is not clean enough to call equivalent.

- Tool files generated: **5**
- `tool_count` in `tool-family\workbooks.md`: **5**
- Tool sections in `tool-family\workbooks.md`: **5**
- Dropped tools: **none**
- Duplicated tools: **none**
- Output files present: **yes** (`tools\`, `tool-family\workbooks.md`, `horizontal-articles\`, `skills-relevance\`, `logs\`)
- Validation status: **PASS with warnings**

**Matched baseline**
- File count matched the expected **5/5/5** baseline.
- Expected tool files were present:
  - `azure-workbooks-create.md`
  - `azure-workbooks-delete.md`
  - `azure-workbooks-list.md`
  - `azure-workbooks-show.md`
  - `azure-workbooks-update.md`

**Differences / concerns**
- `create` is missing the standard example-prompts header.
- `create` is missing both annotation markers.
- Required parameters were not fully represented in example prompts for:
  - `create`
  - `show`
- Additional wording warnings were raised around "this command" vs. "this tool".
- Skills relevance also warned that `GITHUB_TOKEN` was not set, so that step ran under unauthenticated rate limits.

**Assessment**
- **Structure:** equivalent
- **Content cleanliness:** degraded

### 2) extensionfoundry
**Outcome:** The requested alias `extensionfoundry` did not run successfully; the runnable namespace key was `foundryextensions`. Using that key, counts matched baseline, but output still had validator warnings.

- Tool files generated: **11**
- `tool_count` in `tool-family\foundryextensions.md`: **11**
- Tool sections in `tool-family\foundryextensions.md`: **11**
- Dropped tools: **none**
- Duplicated tools: **none**
- Output files present: **yes** (`tools\`, `tool-family\foundryextensions.md`, `horizontal-articles\`, `skills-relevance\`, `logs\`)
- Validation status: **PASS with warnings**

**Matched baseline**
- File count matched the expected **11/11/11** baseline.
- Cross-reference validation confirmed all 11 tool files matched 11 article sections.

**Differences / concerns**
- The alias `extensionfoundry` failed as a direct namespace target; `foundryextensions` was the working namespace key for generation.
- Required parameters were not fully represented in example prompts for:
  - `openai-chat-completions-create`
  - `threads-create`
  - `openai-embeddings-create`
  - `threads-get-messages`
  - `knowledge-index-schema`
- `resource-get` is missing the standard example-prompts header.
- `knowledge-index-schema` used a nonstandard example header and is missing one annotation marker.
- A wording warning was raised around "this command" vs. "this tool".
- Skills relevance again warned that `GITHUB_TOKEN` was not set.

**Assessment**
- **Structure:** equivalent
- **Content cleanliness:** degraded

## Overall verdict
**OUTPUT DEGRADED**

## Why
Both namespaces preserved the expected tool counts and article structure:
- `workbooks`: **5/5/5**
- `foundryextensions`: **11/11/11**

So there is **no evidence of tool loss, duplication, or assembly collapse** from the Phase 1 IChatClient migration.

However, both runs produced nontrivial validation warnings in the generated markdown, especially around:
- required parameters missing from example prompts
- missing or nonstandard example headers
- missing annotation markers
- minor branding phrasing issues

## Practical readout
If the question is **"did the migration break namespace assembly or drop tools?"** the answer is **no**.

If the question is **"is the generated output fully equivalent and clean enough to sign off without caveats?"** the answer is **no**.
