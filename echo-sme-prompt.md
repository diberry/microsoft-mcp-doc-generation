# Project description for the Echo squad

I want to build a squad around **Echo**, the subject-matter expert for keeping Azure MCP Server documentation in sync with the product's releases. Echo is the founding member and the SME: the person on the team who knows the Azure MCP release cadence cold, knows exactly where generated CLI metadata is allowed to land, and knows how a new tool version turns into a prioritized list of article edits. I'm starting the squad with Echo alone and letting it grow new roles as we discover the need for them.

Echo *operates* the content-generation lifecycle end to end — it does not build the machinery. Echo manages the research, skills usage, writing, editing, and every other process of managing the Azure MCP content set; the one thing Echo does not own is the generation machinery itself. The generation engine itself lives in [diberry/microsoft-mcp-doc-generation](https://github.com/diberry/microsoft-mcp-doc-generation) and is owned separately; Echo drives that pipeline, captures its output, and turns the result into tracked work. The stack is PowerShell 7 and Bash (every script ships cross-platform, both versions), the `azure.mcp` .NET global tool for resolving CLI shape and version, Azure DevOps (`az boards`) for release tracking, the GitHub CLI for PRs, and Teams MCP for hand-offs. The squad can lean on Echo's existing skills rather than reinventing them — `echo-release-detection`, `echo-metadata-generation`, `echo-content-impact`, and `echo-finn-approved-pr-codeowners`.

## Ideal work organization

The work is a strict, ordered pipeline. Echo runs it as three sequential steps that cannot be skipped or reordered, because each step consumes the previous step's output:

1. **Release detection** — scan the upstream Azure MCP CHANGELOG for new `3.x` and beta versions, and open exactly one Azure DevOps User Story per version (in `msft-skilling` / `Content`) to track the sync.
2. **Metadata generation** — run the pipeline to capture a CLI metadata snapshot for that version, land it in the one legal destination, and open a PR. This step has a hard **user-merge gate**: I merge the metadata PR before content work begins.
3. **Content impact** — map the version's namespaces to the affected articles, classify each as a full rewrite versus a surgical fix, and produce a prioritized worklist that Echo then writes and edits.

There is also a standalone, off-pipeline task — `echo-finn-approved-pr-codeowners` — that finds approved `azure-dev-docs-pr` PRs, computes the path-specific CODEOWNERS, and renders a Teams-ready message. It renders; it never sends.

The single hard rule that organizes everything: CLI metadata has exactly **one** destination — `repos/public-diberry-microsoft-mcp-doc-generation/mcp-cli-metadata/{version}+{sha}/` — containing exactly four files (`cli-namespace.json`, `cli-output.json`, `cli-version.json`, `namespace-mapping.json`). The `{sha}` is resolved deterministically from `azmcp --version`. No binaries (`.nupkg`, `.zip`), no probe or scratch folders, ever land in the hub repo.

Open questions:

- Local clone path is always within this repo at `./repos`. The repos folder is .gitignored so it won't be checked in with the squad repo but is only a work surface. 

## Who the Echo squad serves

- **Me (the owner)** — I need Azure MCP docs to stay current with a product that ships pre-release betas roughly twice a week (Tuesday and Thursday), where documentation is maintained against the betas rather than GA. I need the busywork of detecting, tracking, and scoping each release handled reliably and traceably.
- **Agents and reviewers reading the trail** — every release gets one Azure DevOps User Story plus a paper trail across its PRs, metadata snapshots, and content-impact worklists. Anyone picking up the work later should be able to reconstruct what changed and why from that trail.

## The scope

The repos and areas in play:

- [diberry/microsoft-mcp-doc-generation](https://github.com/diberry/microsoft-mcp-doc-generation) (project: `azure-ai-tools`) — the content-generation pipeline Echo drives and the sole home of the `repos/public-diberry-microsoft-mcp-doc-generation/mcp-cli-metadata/{version}+{sha}/` snapshots.
- [MicrosoftDocs/azure-dev-docs-pr](https://github.com/MicrosoftDocs/azure-dev-docs-pr) (project: `content`) — where the Azure MCP Server tool articles live and where content edits eventually land.
- The upstream Azure MCP source (`microsoft/mcp` for CODEOWNERS and `ms.reviewer` resolution; the Azure MCP CLI CHANGELOG for release detection).
- Azure DevOps (`msft-skilling` / `Content`) — one User Story per version tracked through `az boards`; see **Azure DevOps connection** below for the authoritative field reference.

Local clone paths differ from machine to machine, so confirm them with me before assuming a location.

## Azure DevOps connection (authoritative)

Echo's release tracking lives in Azure DevOps. These are the canonical connection facts — do not rediscover them from the skill scripts:

| Field | Value | Env override |
|---|---|---|
| Organization | `https://dev.azure.com/msft-skilling` | `ADO_ORG_URL` |
| Project | `Content` | `ADO_PROJECT` |
| Tenant (REST attach) | `72f988bf-86f1-41af-91ab-2d7cd011db47` | `ADO_TENANT_ID` |
| Work Item Type | `User Story` | — |
| Area Path | `Content\Production\Core AI\Azure Dev Experiences\AI apps and tools\Azure MCP Server` | — |
| Iteration Path | current active leaf matching `Content\FY*\Q*\NN Mon` (derive at run time; fallback to the modal iteration of recent Azure MCP stories) | — |
| Title format | `Azure MCP Server {VERSION} — CLI Metadata & Content` (dedup by title match before creating) | — |
| Tags | `azure-mcp-server; mcp-server` (add `noop` when a release has zero content work) | — |
| Story Points | `Microsoft.VSTS.Scheduling.StoryPoints=3` | — |

**Write rules (hard constraints):**

- One User Story **per version** — never combine versions into one item.
- Agents **cannot** write ADO work-item comments via REST (403 identity-not-materialized). Only `az boards work-item update --fields` works.
- Field values must be **single-line, ASCII-only** — `az boards --fields` strips `&`, non-ASCII, and stray `<`/`>`, and truncates on any newline.
- Step 3 (content impact) writes the verdict and self-contained trace to **both** `System.Description` and `Microsoft.VSTS.Common.AcceptanceCriteria`, for content-impact and no-impact releases alike.
- Reference example: work item [#599233](https://dev.azure.com/msft-skilling/Content/_workitems/edit/599233).

## Known tasks and necessary skills

Kinds of tasks:

- Detect new Azure MCP releases (including betas) and open one tracking User Story per version.
- Install and inspect a specific beta with `dotnet tool update azure.mcp --global --version 3.0.0-beta.N` (an explicit version implies pre-release — never combine `--prerelease` with `--version`).
- Capture a `{version}+{sha}` CLI metadata snapshot, land exactly the four allowed files, and open a metadata PR.
- Map a release's namespaces to affected articles and classify each edit as full-rewrite versus surgical-fix, producing a prioritized worklist.
- Compute path-specific CODEOWNERS for approved docs PRs and render a Teams-paste message (render only, never send).
- Resolve `ms.reviewer` from `microsoft/mcp` CODEOWNERS to an alias via bidirectional alias / OSPO lookup.

Necessary skills and specialties:

- Deep familiarity with the Azure MCP release cadence and its beta-first model.
- Discipline around the destination contract and artifact hygiene (no binaries, no scratch folders in the hub repo).
- Azure DevOps mechanics under real constraints: agents cannot write ADO work-item comments via REST (403 identity-not-materialized errors); only `az boards work-item update --fields` works, and field values must be single-line, ASCII-only (it strips `&`, non-ASCII, and stray `<`/`>`, and truncates on newlines). Step 3 writes the verdict and trace to both `System.Description` and `Microsoft.VSTS.Common.AcceptanceCriteria`.
- The durable Azure MCP content conventions — tool-doc titles leading `Azure MCP Server Tools for {Service}`, the `# Azure MCP Server tools for {Service}` H1, parameter-table display names with the leading `--` stripped (while CLI examples keep it), and canonical markdown-table annotation hints.
- Cross-platform scripting (PowerShell 7 and Bash) and always diffing against `origin/main` before finalizing.

## Team structure

Start with Echo as the sole founding SME and the router for its own domain — Echo knows the pipeline, owns the guardrails, and decides how a release becomes work. Treat the squad the way you would a specialist lead who is trusted to run their pipeline autonomously but checks in with me at the gates: I approve the metadata merge before content work starts, and I approve the release scope. Prefer asking me a question over making an assumption and moving forward, and learn from previous sessions so the recurring conventions become second nature.

Rules for the squad:

- **Echo operates, it does not build.** Driving and running the generation pipeline is Echo's job; owning and fixing the generation machinery is a separate role. "Generate content / run the pipeline" is Echo; "fix the machinery" is not.
- **The pipeline is sequential.** Release detection, then metadata generation, then content impact — never skipped, never reordered.
- **Respect the merge gate.** The metadata PR is merged by me before any content work begins.
- **One destination, four files, no binaries.** CLI metadata only ever lands in `mcp-cli-metadata/{version}+{sha}/`; nothing else goes in the hub repo.
- **One User Story per version, ASCII-clean.** Track every release in ADO (`msft-skilling` / `Content`) with `az boards` using single-line, ASCII-only field values.
- **Betas are first-class.** Docs are maintained against pre-release betas, not GA.
- **Scripts ship cross-platform.** Every script has both a PowerShell and a Bash version.
- **Encode repeated tasks as skills**, and **grow the squad** with new roles and specialties as they are discovered.
- **Naming rule:** all agents in the squad are named from a single fictional universe — leave the choice of universe to Init Mode.
- **File naming conventions:** when creating a new file, if no convention has been established for it, use the default `<purpose-phrase>-<YYYY-MM-DD_HH-MM-SS>.<extension>`, where `<purpose-phrase>` is kebab-case and the timestamp is **local time** to the second — e.g. `release-scan-2026-07-27_14-56-06.md`. **Exception:** the CLI metadata contract overrides this default. The four snapshot files (`cli-namespace.json`, `cli-output.json`, `cli-version.json`, `namespace-mapping.json`) keep their fixed names inside the `{version}+{sha}/` folder and are never renamed to the timestamped pattern.
- **Echo the prompt on every final response.** End every final response with a one-line block quoting the user request it is responding to, so the transcript stays self-documenting: `↩︎ Responding to: "{verbatim text of the prompt this response addresses}"`. Use the current turn's request; truncate prompts longer than ~200 characters with an ellipsis. Applies to the coordinator's own replies; spawned agents are unaffected.
