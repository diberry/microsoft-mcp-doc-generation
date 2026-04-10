# AI-Powered Content Generation Requirements

> **Document type:** Formal requirements specification
> **Source:** [Issue #202](https://github.com/diberry/microsoft-mcp-doc-generation/issues/202)
> **Authors:** Avery (Lead/Architect), Riley (Pipeline Architect)
> **Reviewers:** Sage (AI/Prompt Engineer), Parker (QA), Reeve (Documentation Engineer), Morgan (C# Generator Developer), Quinn (DevOps/Scripts Engineer)
> **Date:** 2026-03-24 (filed) · 2026-04-01 (last updated) · Consolidated by Reeve
> **Status:** Living document — sub-issues track individual gaps

---

## Executive summary

This document formalizes the requirements for a production-grade AI content generation system, drawn from the team's experience building a pipeline that generates 800+ markdown documentation files across 52 Azure MCP namespaces through 7 pipeline stages (4 AI-powered).

Every requirement maps to a failure mode the team has experienced, a gap identified during operation, or a pattern that has proven effective. The ~100 requirements were authored by Avery (architecture, quality, content correctness, safety) and Riley (pipeline contracts, execution mechanics, recovery, observability), then reviewed by 5 additional team members. As of 2026-04-01, approximately 30 requirements are implemented, 1 is in progress, 15 remain unimplemented, and 3 are deferred.

### Guiding principles

| Perspective | Principle |
|---|---|
| **Avery (Architecture)** | Every stage must produce verifiably accurate content, or the whole chain falls apart. "It works for storage" is never sufficient — all 52 namespaces must pass. |
| **Riley (Pipeline)** | The pipeline is a compiler. Each step transforms data with a contract. If a contract breaks, everything downstream is suspect. |

---

## Requirements

### 1. Content correctness

Requirements ensuring generated content is accurate and grounded in source data.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-CC-01 | Ground truth validation gates — validate AI artifacts against deterministic source data before advancing | P0 | ✅ Implemented | Post-validators per step (`IPostValidator`) |
| REQ-CC-02 | Cross-reference integrity — generated content must reference only entities that exist (`tool_count` matches H2 count matches files on disk) | P0 | ✅ Implemented | `ParameterCoverageChecker`, `verify-quantity` |
| REQ-CC-03 | Fabrication detection via structural checks — don't rely on AI to police AI; use deterministic `ParameterCoverageChecker` | P0 | ✅ Implemented | `ParameterCoverageChecker` (deterministic, regex + slug variants) |
| REQ-CC-04 | Token truncation detection — `FinishReason==Length` must throw, not silently produce partial content | P1 | ⏸️ Deferred | Linked to REQ-AI-09 |
| REQ-CC-05 | Leaked template token detection — scan for `<<`, `{{`, `}}` in final output | P0 | ✅ Implemented | Template token scan in validators (REQ-DI-06) |
| REQ-CC-06 | No silent degradation — step dependency enforcement prevents skipping or producing partial results | P0 | ✅ Implemented | Step dependency enforcement (#320) |
| REQ-CC-07 | Semantic fabrication detection — catch invented URL patterns, fabricated RBAC roles, wrong service quotas | P1 | ❌ Not started | Sage flagged: structural checks are necessary but not sufficient |

**Sage's addition:** Add reference-set validators per namespace — verify example values match curated sets (valid locations from CLI metadata), verify tool descriptions don't mention non-existent features, verify links follow `/{service}` pattern and resolve.

**Morgan's clarification:** `ParameterCoverageChecker` performs *incompleteness detection* (flags bare placeholders without concrete values), not fabrication detection of invented parameters. The distinction matters for requirement scoping.

---

### 2. Deterministic output and prompt management

Requirements for reproducibility and auditability of AI-generated content.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-DT-01 | Configurable seed parameter for reproducible AI output during debugging (seed=42 for debug, seed=null for production) | P3 | ⏸️ Deferred | Configurable but not per-step |
| REQ-DT-02 | Temperature configurable per AI step (≤0.3 factual, ≤0.7 creative) | P3 | ⏸️ Deferred | Configurable but not per-step |
| REQ-DT-03 | Non-AI steps (0, 1, 5) MUST be fully deterministic | P1 | ✅ Implemented | Steps 0, 1, 5 produce identical output for identical input |
| REQ-DT-04 | Pipeline runs tagged with run ID (git hash, prompt hashes, model, timestamp) | P2 | 🔄 Partial | Prompt hashing implemented (#211 → PR #331); full run ID not yet |
| REQ-DT-05 | Hash-based cache invalidation — unchanged inputs = reusable outputs | P3 | ❌ Not started | Part of incremental generation (REQ-RR-07) |
| REQ-DT-06 | Deterministic post-processing chain (FamilyFileStitcher) — 9 stages, all idempotent | P0 | ✅ Implemented | All post-processors have "already-correct = no-op" tests |
| REQ-DT-07 | Structural contracts, not content contracts — validate structure (frontmatter, H2 count) not prose | P0 | ✅ Implemented | Frontmatter field validation, H2 count checks |
| REQ-DT-08 | Prompt versioning with SHA256 hash identifiers | P1 | ✅ Implemented | #211 → PR #331, SHA256 of system+user prompts |
| REQ-DT-09 | Prompt regression testing framework | P1 | ✅ Implemented | #214 → PR #329 |
| REQ-DT-10 | Idempotent transformations — running twice produces identical results | P0 | ✅ Implemented | FrontmatterEnricher, ExampleValueBackticker, ContractionFixer, AnnotationSpaceFixer all tested |

**Sage's addition:** Seed provides reproducibility *only within a model version* — GPT-4o-2025-01 + seed=42 ≠ GPT-4o-2024-11 + seed=42. Recommend logging model version + seed in frontmatter as a cache invalidation key.

---

### 3. Step contracts

Requirements for typed, explicit contracts between pipeline stages.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-SC-01 | Every step MUST declare typed input/output contracts via `StepResult` | P0 | ✅ Implemented | `IPipelineStep` with `StepResult`, `DependsOn`, `FailurePolicy` |
| REQ-SC-02 | Every step MUST declare `DependsOn` explicitly — no implicit dependencies (AD-020 Risk 3) | P0 | ✅ Implemented | Step dependency enforcement |
| REQ-SC-03 | Step outputs MUST include manifest file (`step-{id}-manifest.json`) with file list, sizes, checksums | P2 | ❌ Not started | Morgan flagged scope creep: requires threading through all 6 steps |
| REQ-SC-04 | Steps MUST produce structured `step-{id}-result.json` — no regex parsing of stdout (AD-020 Risk 2) | P1 | ✅ Implemented | #210 CLOSED — replaced regex parsing |
| REQ-SC-05 | Steps consuming AI output MUST validate response schema before writing to disk | P0 | 🔄 Partial | Step 4 validates; Steps 2, 3, 6 do not (REQ-AI-03) |
| REQ-SC-06 | Step contracts MUST include `FailurePolicy` enum (`Fatal`, `Warn`, `HumanReview`) with consistent enforcement | P0 | ✅ Implemented | FailurePolicy enum in step contracts |
| REQ-SC-07 | Inter-step data exchange via filesystem with documented directory conventions only | P1 | ✅ Implemented | Documented directory conventions |

---

### 4. Failure handling

Requirements for graceful failure, retry, and recovery semantics.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-FH-01 | Distinguish `Fatal`/`Warning`/`HumanReview` at artifact level, not just step level | P1 | ✅ Implemented | FailurePolicy enum |
| REQ-FH-02 | All AI steps MUST be idempotent — re-run must not corrupt successful outputs (AD-020 Risk 1) | **P0** | ✅ Implemented | Verified via tests; Morgan confirmed |
| REQ-FH-03 | Retry semantics configurable per step (currently only Step 4 has `maxRetries:2`) | P1 | ✅ Implemented | Step 4 pattern extensible to other steps |
| REQ-FH-04 | After max retries, produce `CriticalFailureRecord` JSON + human-readable summary | P2 | ❌ Not started | — |
| REQ-FH-05 | Partial namespace failure MUST NOT block other namespaces | **P0** | ✅ Implemented | `start.sh` tracks per-namespace success/failure |
| REQ-FH-06 | Roll-up failure report at pipeline completion | P2 | ❌ Not started | — |
| REQ-FH-07 | File I/O errors reported as infrastructure failures distinct from content failures | P2 | ❌ Not started | — |

---

### 5. Parallel execution

Requirements for concurrent namespace processing.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-PE-01 | Namespace-level parallelism with configurable `--max-parallel N` | P2 | ❌ Not started | Quinn: PowerShell job parallelism feasible |
| REQ-PE-02 | Each namespace writes to isolated output directory | P0 | ✅ Implemented | `generated-{namespace}/` pattern |
| REQ-PE-03 | Centralized rate limiter (semaphore/token bucket) for shared AI endpoints | P2 | ❌ Not started | Quinn: C# layer responsibility |
| REQ-PE-04 | Bootstrap (Step 0) remains serial global step completing before namespace work | P0 | ✅ Implemented | Bootstrap is serial by design |
| REQ-PE-05 | Post-assembly merge as barrier — merge-what-succeeded strategy | P1 | ✅ Implemented | `merge-namespaces.sh`, `NamespaceMerger.cs` |
| REQ-PE-06 | Console output tagged with namespace context (`[storage]`, `[compute]`) | P2 | ❌ Not started | — |

---

### 6. Data flow integrity

Requirements ensuring data consistency across pipeline stages.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-DI-01 | Each step validates output file counts against expected counts | P2 | 🔄 Partial | `verify-quantity` for final output; not per-step |
| REQ-DI-02 | Pipeline-level manifest (`pipeline-manifest.json`) at completion | P2 | ❌ Not started | — |
| REQ-DI-03 | Tool count consistency validation across Steps 1–3 | P1 | ✅ Implemented | `verify-quantity`, cross-reference checks |
| REQ-DI-04 | Machine-validated frontmatter (required fields present and well-formed) | P1 | ✅ Implemented | Frontmatter validation in post-processors |
| REQ-DI-05 | No silent tool drops — report missing tools with reasons | P1 | ✅ Implemented | Step dependency enforcement |
| REQ-DI-06 | Template token leak validation in all output files | P0 | ✅ Implemented | Validators scan for `<<`, `{{`, `}}` tokens |

---

### 7. AI step quality

Requirements specific to steps that call Azure OpenAI (Steps 2, 3, 4, 6).

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-AI-01 | Dynamic token budgets based on content size, not hardcoded | P1 | ✅ Implemented | 1000 tokens/tool + overhead scaling |
| REQ-AI-02 | Timeouts proportional to token budget | P1 | 🔄 Partial | Sage: verify Step 6 timeout ≥ 2 min for 24K budget |
| REQ-AI-03 | Structural AND semantic AI response validation for Steps 2, 3, 6 (parity with Step 4) | P1 | ❌ Not started | Only Step 4 has full validation — **top-3 unimplemented** |
| REQ-AI-04 | Prompt versioning with version identifiers | P1 | ✅ Implemented | #211 → PR #331, SHA256 hashing |
| REQ-AI-05 | Per-call logging: prompt version, tokens, model, latency, truncation flag | P1 | 🔄 In progress | #212 OPEN — token tracking |
| REQ-AI-06 | Model fallback support (`FallbackDeployment` in `GenerativeAIOptions`) | P2 | ❌ Not started | — |
| REQ-AI-07 | Fabrication marker checks — invented tool names, hallucinated parameters, wrong URLs | P2 | ❌ Not started | Morgan: new validator needed in Steps 2, 3, 6 |
| REQ-AI-08 | Raw AI response persisted alongside processed output for audit | **P0** | ❌ Not started | **Top-3 unimplemented** — every team member flagged this |
| REQ-AI-09 | Token truncation retry with larger budget or input splitting | P1 | ⏸️ Deferred | Sage: also need input-splitting strategy when input+prompt exceeds max |

**Sage's additions:**
- REQ-AI-08 is effectively P0: every AI call should store `{step}-{namespace}-{tool-id}-raw.json` (prompt + response + usage) for debugging, auditing, and reprocessing without re-calling the API.
- REQ-AI-05 version identifier should include BOTH prompt hash AND model version.
- If input+prompt exceeds max budget, need input-splitting strategy — split tools into batches, parallelize within a namespace.

---

### 8. Post-assembly validation

Requirements for quality gates after content assembly.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-PA-01 | Cross-namespace consistency check (formatting, frontmatter schema, section ordering) | P2 | ❌ Not started | — |
| REQ-PA-02 | Merge validation — `tool_count` in merged article = sum of constituent counts (AD-011) | P1 | ✅ Implemented | `MergeGroupValidator.cs` |
| REQ-PA-03 | Link integrity check — all internal cross-references must resolve | P2 | ❌ Not started | — |
| REQ-PA-04 | Branding consistency check across all namespaces | P1 | ✅ Implemented | `brand-to-server-mapping.json` validation |
| REQ-PA-05 | Parameter coverage roll-up report | P2 | ❌ Not started | — |
| REQ-PA-06 | Acrolinx-readiness pre-check | P2 | 🔄 Partial | Full prompt review (#294 → PRs #323, #330) |
| REQ-PA-07 | File size anomaly detection (>50% deviation from namespace median) | P3 | ❌ Not started | — |

---

### 9. Pipeline observability

Requirements for monitoring, logging, and cost tracking.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-OB-01 | Step-level timing as structured JSON | P2 | ❌ Not started | Quinn: feasible with existing subprocess capture |
| REQ-OB-02 | AI token usage tracked per namespace per step | P1 | 🔄 In progress | #212 OPEN — assigned to Quinn + Sage |
| REQ-OB-03 | Pipeline summary report at completion (duration, tokens, warnings, failures, cost) | P2 | ⏸️ Deferred | — |
| REQ-OB-04 | Error aggregation — group similar failures across namespaces | P3 | ❌ Not started | — |
| REQ-OB-05 | Trend tracking — compare current vs previous run metrics | P3 | ❌ Not started | — |
| REQ-OB-06 | Per-namespace log files from subprocess generators | P2 | ❌ Not started | Quinn: route logs to `generated-{ns}/logs/step-{id}.log` |
| REQ-OB-07 | Baseline fingerprinting — hash output for delta detection across runs | P1 | ✅ Implemented | #209 → PR #324 |
| REQ-OB-08 | CI integration documentation | P1 | ✅ Implemented | #213 → PR #328 |

---

### 10. Configuration management

Requirements for centralized, validated configuration.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-CM-01 | All AI config flows through `GenerativeAIOptions` — no direct env var reads | P1 | ✅ Implemented | Centralized in C# layer |
| REQ-CM-02 | Per-namespace config overrides via `brand-to-server-mapping.json` | P1 | ✅ Implemented | Three-tier filename resolution |
| REQ-CM-03 | Feature flags for AI steps per namespace | P3 | ❌ Not started | — |
| REQ-CM-04 | Token budget overrides per namespace in config (not code constants) | P3 | ❌ Not started | — |
| REQ-CM-05 | `.env` search path hierarchy documented and logged at startup | P1 | ❌ Not started | Quinn P1: `preflight.ps1` should log resolution chain |
| REQ-CM-06 | Config validation at pipeline startup before any generation | P0 | ✅ Implemented | `preflight.ps1` validates `.env` |
| REQ-CM-07 | Model deployment and API version configurable without code changes | P1 | ✅ Implemented | `FOUNDRY_MODEL_NAME`, `FOUNDRY_MODEL_API_VERSION` env vars |

---

### 11. Recovery and resumption

Requirements for safe re-runs and incremental generation.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-RR-01 | `--skip-deps` MUST NOT trigger `ResetOutputDirectory` (AD-020 Risk 1) | **P0** | ✅ Implemented | Bootstrap skip behavior fixed |
| REQ-RR-02 | Step-level re-run consuming previous step outputs | P1 | ✅ Implemented | `--skip-deps` + `--steps N` flags |
| REQ-RR-03 | Tool-level re-run within a namespace | P2 | ❌ Not started | — |
| REQ-RR-04 | Resume-from-failure using `pipeline-manifest.json` checkpoint | P2 | ❌ Not started | Depends on REQ-DI-02 |
| REQ-RR-05 | Dry-run mode showing execution plan and estimated token cost | P2 | ❌ Not started | Quinn estimated 2h effort |
| REQ-RR-06 | Output archival before destructive operations | P2 | ❌ Not started | Quinn: back up `generated-{ns}/` to retention bucket |
| REQ-RR-07 | Incremental generation — skip unchanged namespaces | P3 | ❌ Not started | Hash-based cache invalidation needed |

**Quinn's blocker note (resolved):** `start.sh advisor 4 --skip-deps` previously triggered global Bootstrap `ResetOutputDirectory`, wiping Steps 1–3 outputs. Fix was in `PipelineRunner.cs` — skip Bootstrap entirely when `--skip-deps` is set and output directory exists.

**Morgan's question (answered):** REQ-RR-01 requires two code paths in Bootstrap — "fresh run" (destroy) vs "incremental after fix" (preserve). This is now implemented via the `--skip-deps` flag.

---

### 12. Testing and validation

Requirements from Parker's QA review, covering gaps in test infrastructure.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-TV-01 | TDD for all pipeline code (AD-007, AD-010, AD-019) | P0 | ✅ Implemented | Enforced via PR review process |
| REQ-TV-02 | Template regression tests | P0 | ✅ Implemented | AD-019, PRs #200, #201 |
| REQ-TV-03 | E2E integration test harness for full pipeline runs (~10 representative namespaces) | P1 | ❌ Not started | **Top-3 unimplemented** — #342 open |
| REQ-TV-04 | Documented test data corpus — 52-namespace shapes, parameter cardinality, token budget breakpoints | P2 | ❌ Not started | — |
| REQ-TV-05 | Behavioral tests for each `IPostValidator` proving it catches its target failure | P1 | ✅ Implemented | `ParameterCoverageChecker` edge case tests |
| REQ-TV-06 | Recovery/resumption test scenarios (fail Step 4, retry, verify Steps 1–3 untouched) | P1 | ❌ Not started | — |
| REQ-TV-07 | Parallel execution stress tests (race conditions, namespace isolation, rate limiter backoff) | P2 | ❌ Not started | — |
| REQ-TV-08 | AI failure path coverage — mock responses for truncation, JSON parse failures, hallucinated params | P1 | ❌ Not started | — |
| REQ-TV-09 | Configuration-driven test matrix generated from `brand-to-server-mapping.json` | P2 | ❌ Not started | — |
| REQ-TV-10 | Baseline corpus fingerprinting for regression detection | P1 | ✅ Implemented | #209 → PR #324 |
| REQ-TV-11 | Validate-only mode — run validators without re-generating (faster feedback) | P2 | ❌ Not started | — |

**Parker's recommendation:** Draft a companion test strategy document (test data corpus, E2E scenarios, validator coverage checklist, recovery test matrix) before implementation sprints begin.

---

### 13. Documentation

Requirements from Reeve's documentation review.

| ID | Description | Priority | Status | Evidence / Cross-reference |
|---|---|---|---|---|
| REQ-DC-01 | Operator runbooks — token truncation detected, then what? Troubleshooting guide | P1 | ❌ Not started | — |
| REQ-DC-02 | Failure mode documentation — AD-020's seven risks as "Known Issues & Mitigations" | P1 | ❌ Not started | — |
| REQ-DC-03 | Recovery procedure documentation — when to use incremental mode, how to re-run safely | P1 | ❌ Not started | — |
| REQ-DC-04 | Configuration audit trail — how to determine what config was used for a run | P2 | ❌ Not started | — |
| REQ-DC-05 | Validation report interpretation guide — who reads structured errors and how | P2 | ❌ Not started | — |
| REQ-DC-06 | Human review SLA — expected response time for confidence-scored outputs | P3 | ❌ Not started | — |
| REQ-DC-07 | New namespace onboarding checklist — workflow for adding a new service area | P2 | ❌ Not started | — |
| REQ-DC-08 | Prompt versioning documentation | P1 | ✅ Implemented | #333 → PR #336 |

---

## Scorecard

| Status | Count | Percentage |
|---|---|---|
| ✅ Implemented | ~35 | ~64% |
| 🔄 In progress / Partial | ~5 | ~9% |
| ❌ Not started | ~25 | ~24% |
| ⏸️ Deferred | ~4 | ~4% |

---

## Top unimplemented requirements (highest impact)

These three gaps were flagged by multiple team members as the highest-impact remaining work:

1. **REQ-AI-08 — Raw AI response persistence** (P0). Every team member flagged this. Zero audit trail for AI calls. Enables debugging, reprocessing without re-calling the API, and responding to fabrication complaints. Morgan assessed this as low effort.

2. **REQ-AI-03 — Validator parity for Steps 2, 3, 6** (P1). Only Step 4 has full structural + semantic validation. Steps 2, 3, and 6 can produce invalid AI output that passes unchecked. This is the biggest quality gap remaining.

3. **REQ-TV-03 — E2E integration test harness** (P1). No way to regression-test a full pipeline run across representative namespaces. Unit tests pass but pipeline-level regressions hide. Parker recommended ~10 representative namespaces covering fast (advisor), slow (compute), and edge cases (foundryextensions). Tracked as #342.

**Honorable mention:** REQ-RR-05 (Dry-run mode) — Quinn estimated 2 hours of effort; provides cost estimation before expensive AI runs.

---

## Priority summary

| Priority | Requirements | Rationale |
|---|---|---|
| **P0** | REQ-RR-01, REQ-FH-02, REQ-FH-05, REQ-CC-01–03, REQ-CC-05–06, REQ-DT-06–07, REQ-DT-10, REQ-SC-01–02, REQ-SC-06, REQ-PE-02, REQ-PE-04, REQ-DI-06, REQ-CM-06, REQ-TV-01–02, REQ-AI-08 | Data-destroying bugs, silent corruption, foundational contracts |
| **P1** | REQ-AI-01–05, REQ-AI-09, REQ-CC-04, REQ-CC-07, REQ-DT-03, REQ-DT-08–09, REQ-SC-04–05, REQ-SC-07, REQ-FH-01, REQ-FH-03, REQ-DI-03–05, REQ-PA-02, REQ-PA-04, REQ-OB-02, REQ-OB-07–08, REQ-CM-01–02, REQ-CM-05, REQ-CM-07, REQ-RR-01–02, REQ-TV-03, REQ-TV-05–06, REQ-TV-08, REQ-TV-10, REQ-DC-01–03, REQ-DC-08 | Fragile error detection, prompt versioning, truncation risk, cost visibility, documentation |
| **P2** | REQ-PE-01, REQ-PE-03, REQ-PE-05–06, REQ-DI-01–02, REQ-PA-01, REQ-PA-03, REQ-PA-05–06, REQ-OB-01, REQ-OB-03, REQ-OB-06, REQ-AI-06–07, REQ-SC-03, REQ-FH-04, REQ-FH-06–07, REQ-RR-03–06, REQ-TV-04, REQ-TV-07, REQ-TV-09, REQ-TV-11, REQ-DC-04–05, REQ-DC-07 | Parallelism, data integrity, merge validation, observability |
| **P3** | REQ-DT-01–02, REQ-DT-05, REQ-OB-04–05, REQ-CM-03–04, REQ-PA-07, REQ-RR-07, REQ-DC-06 | Determinism tuning, trends, feature flags |

---

## Open questions

These questions were raised in the issue discussion and remain unresolved:

1. **Issue lifecycle** — Should #202 be closed and fanned out into individual tracking issues, or kept open as the master requirements document? (Comment 9 recommends close + fan out; awaiting team vote.)

2. **Canary namespace pattern** — Sage proposed running the first 3 AI steps on `{advisor, monitor, compute}` (fast/dense/sparse) before the full 52-namespace run as a cheap early signal. Not yet evaluated for feasibility.

3. **Prompt change impact assessment** — Sage noted that AD-005 requires PR review for prompt changes, but there is no automation to flag which namespaces need regeneration. Proposed: PR template includes regeneration script for reviewer (`./start.sh --regenerate-prompts --sample advisor monitor compute`).

4. **Post-processing as compensation** — Morgan cautioned that the 9-stage post-processing chain fixes *style violations* but cannot fix structural problems (missing sections, wrong parameter counts). Requirements should not imply that post-processing salvages bad AI output.

5. **Step manifest scope creep** — REQ-SC-03 (manifest files with checksums per step) requires threading through all 6 steps. Morgan flagged this as high-effort with diminishing returns compared to the existing typed contract pattern.

6. **Human review SLA** — Confidence scoring and exit code 2 identify outputs needing human review, but no expected response time or escalation path is defined.

7. **Parallel execution CI testing** — Quinn recommended adding `--max-parallel 1,2,4` runs to CI to catch race conditions early. No CI pipeline changes have been scoped yet.

---

## Spawned issues

| Issue | Title | Status | Relates to |
|---|---|---|---|
| #209 | Baseline fingerprinting | ✅ Merged (PR #324) | REQ-OB-07, REQ-TV-10 |
| #210 | Structured step-result.json | ✅ Closed | REQ-SC-04 |
| #211 | Prompt versioning | ✅ Merged (PR #331) | REQ-DT-08, REQ-AI-04 |
| #212 | Token usage tracking | 🔄 Open | REQ-OB-02, REQ-AI-05 |
| #213 | CI integration documentation | ✅ Merged (PR #328) | REQ-OB-08 |
| #214 | Prompt regression testing | ✅ Merged (PR #329) | REQ-DT-09 |
| #294 | Full prompt review | ✅ Merged (PRs #323, #330) | REQ-AI-04, REQ-PA-06 |
| #333 | Prompt versioning docs | ✅ Merged (PR #336) | REQ-DC-08 |
| #340 | AI audit trail (response archival) | ✅ Merged | REQ-AI-08 (partial) |
| #341 | Step 3 validator | ✅ Merged | REQ-AI-03 (partial) |
| #342 | E2E integration tests | 🔄 Open | REQ-TV-03 |
| #345 | Package rationalization | 🔄 Open | — |

---

## Architectural decisions referenced

| Decision | Title | Relevance |
|---|---|---|
| AD-005 | Prompt change governance | PR review gates for prompt modifications |
| AD-007 | Test-Driven Development | Mandatory TDD for all pipeline code |
| AD-010 | Behavioral test depth | Tests must catch the bug on regression |
| AD-011 | Multi-namespace merge | Post-assembly merge pattern for multi-namespace services |
| AD-019 | Template separation | `.hbs` templates, `.txt` prompts, `.json` config — independent and versionable |
| AD-020 | Pipeline risk register | Seven identified risks driving P0 requirements |

---

## Revision history

| Date | Author | Change |
|---|---|---|
| 2026-03-24 | Avery, Riley | Initial requirements filed as issue #202 |
| 2026-03-24 | Sage, Parker, Reeve, Morgan, Quinn | Team reviews (comments 4–8) |
| 2026-03-29 | Team | Status cross-reference and vote summary (comment 9) |
| 2026-04-01 | Ralph (Work Monitor) | Status updates noting #340, #341, #342 |
| 2026-04-01 | Reeve | Consolidated into formal requirements document |
