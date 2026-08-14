# beta.34 critical-failure baseline freeze (issue #813, Step 1)

This document describes the **frozen beta.34 baseline** — an immutable, sanitized snapshot
of the catalog critical-failure records produced by Azure MCP build
`3.0.0-beta.34+eec7acccddab1e16be852a3c3b9503cc9adf7538` (generation run
`generated-20260813T162453`).

> **Scope:** This is **Step 1 only** of issue #813 — it *freezes the evidence*. It adds no
> production behavior and fixes none of the underlying failures. Remediation is later steps.

## Why it exists

Before fixing the beta.34 generation failures, the team froze a known-good snapshot of the
34 logical critical-failure records so that:

- Remediation work can be measured against a stable, unchanging reference.
- The failure set cannot silently shift while fixes are in flight (no moving goalposts).
- The evidence is sanitized, secret-free, and safe to commit to the repository.

## Where the baseline lives

| Artifact | Path | Nature |
|----------|------|--------|
| Freeze script | `scripts/baseline/New-Beta34Baseline.ps1` | Source (deterministic generator + verifier) |
| Classification input | `scripts/baseline/beta34-classification.json` | Source (per-record taxonomy) |
| Classification deriver | `scripts/baseline/classify_beta34.py` | Source |
| Sanitized fixtures | `mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/critical-failures/*.json` | **Generated output — immutable** |
| Provenance manifest | `mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/beta34-baseline-manifest.json` | **Generated output — immutable** |
| Source inventory | `mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/source-inventory.json` | **Generated output — immutable** |
| EOL lock | `mcp-tools/DocGeneration.Baseline.Beta34.Tests/.gitattributes` | Source (hand-authored) |
| Guard tests | `mcp-tools/DocGeneration.Baseline.Beta34.Tests/*.cs` | Source (32 xUnit tests) |

The fixtures, manifest, and inventory are **generated output** — never hand-edit them. To change
them, edit the freeze script and regenerate.

### Committed source inventory (clean-checkout proof)

The source run directory (`generated-20260813T162453/`) is gitignored, so the immutability and
duplicate-copy accounting tests would have nothing to compare against on a fresh clone. To close
that gap, the freeze also emits `Fixtures/source-inventory.json` — a **committed** record of all
**68 physical copies** (34 `catalog` + 34 `namespace`) with each copy's raw SHA-256, its
`logicalIdentity`, `stableId`, and `copyKind`. This makes the whole suite pass on a clean checkout
with no `generated-*` directory present.

### EOL lock (`.gitattributes`)

The 36 hash-pinned artifacts (34 fixtures + `beta34-baseline-manifest.json` +
`source-inventory.json`) are committed with **LF** endings. Because the repo-root `.gitattributes`
only declares `* text=auto`, a Windows clone with `core.autocrlf=true` would rewrite them to CRLF
on checkout, changing their bytes and **breaking the pinned SHA-256 values**. To prevent this,
`mcp-tools/DocGeneration.Baseline.Beta34.Tests/.gitattributes` marks all 36 artifacts **`-text`**
(binary-exact, no EOL conversion on any OS). When the artifact set changes,
`git add --renormalize` the fixture paths so index and attributes stay consistent.

## Immutability and regeneration contract

Every fixture is pinned by SHA-256 in the manifest (`sanitizedSha256` for the sanitized fixture,
`sourceSha256` for the original capture). The guard tests fail if any fixture's bytes change.

Regenerate from the read-only source run (PowerShell 7, from the repo root):

```bash
pwsh -File scripts/baseline/New-Beta34Baseline.ps1
```

Prove the committed baseline is byte-for-byte reproducible (does **not** write to the fixtures):

```bash
pwsh -File scripts/baseline/New-Beta34Baseline.ps1 -VerifyOnly
```

`-VerifyOnly` regenerates the baseline into a throwaway temp directory and compares the
deterministic outputs against the committed baseline: every fixture must be byte-identical
(SHA-256), every manifest record's `sourceSha256` / `sanitizedSha256` must match, and every
`source-inventory.json` physical copy must match on `relativePath`, `sha256`, `copyKind`,
`logicalIdentity`, and `stableId`. Any drift exits non-zero with a per-item report; exit 0 means
the baseline is provably reproducible. Fields that legitimately change per run —
`provenance.captureTimestampUtc`, `provenance.toolVersions`, and the inventory's `generatedAtUtc` —
are excluded from the comparison. The script also fails closed (non-zero, precise message) on
missing source, wrong record count, invalid classification, orphaned records, duplicate-copy
accounting errors, non-idempotent sanitization, an `accounting` block that disagrees with the
pinned expectation, undeliverable AI provenance, or any secret scan hit. See
`scripts/baseline/README.md` for the full exit-code table.

## Classification, chain role, and dependency accounting

Each of the 34 records carries **two independent axes** that the earlier drafts conflated. Keeping
them separate is what stops Steps 2-10 from inheriting a wrong dependency model.

### Axis 1 — `classification` (AD-028 taxonomy, unchanged)

Where the record sits in the *analysis* taxonomy, sourced from
`scripts/baseline/beta34-classification.json`:

| `classification` | Meaning | beta.34 count |
|------------------|---------|---------------|
| `root` | An originating failure, not caused by another record | 21 |
| `cascade` | A downstream failure caused by an upstream one | 9 |
| `mixed` | Both Class-A **and** Class-B error overlap in one record | 3 |
| `diagnostic` | A diagnostic/observability record rather than a distinct defect | 1 |

**`mixed` describes A+B *error overlap*, not chain position.** It is independent of where the
record sits in a failure chain. For example `loadtesting.04` and `postgres.04` are `mixed` but
`chainRole=root` (no upstream Step-2), while `foundryextensions.04` is `mixed` **and**
`chainRole=cascade`.

### Axis 2 — `chainRole` (chain position only, mechanically derived)

`chainRole` captures *only* chain position and is derived mechanically from the records: a Step-4
record with ≥1 Step-2 record in the **same namespace** is `cascade`; every Step-2 record and every
Step-4 record with no namespace-mate Step-2 is `root`.

| `chainRole` | beta.34 count |
|-------------|---------------|
| `root` | 24 |
| `cascade` | 10 |

Because the two axes measure different things, their `cascade` counts differ on purpose
(classification `cascade` = 9, chainRole `cascade` = 10).

### Error class

The per-record `errorClass` field (single value) categorizes the defect:

| `errorClass` | Meaning | beta.34 count |
|--------------|---------|---------------|
| `A` | Class-A defect | 26 |
| `B` | Class-B defect | 4 |
| `A+B` | Both Class-A and Class-B present | 3 |
| `C` | Class-C defect | 1 |

Each record also exposes an `errorClasses` **array** (`"A+B"` → `["A","B"]`). The manifest's
`errorClassCounts` counts membership in that array, so the 3 `A+B` records are counted under both
`A` and `B`: **A 29, B 7, AB 3, C 1**.

### Dependency accounting (corrected)

Class-D dependency is tracked per record by `hasUpstreamStep2` (boolean) **and** by the
`upstreamStableIds` array (the sorted stableIds of the same-namespace Step-2 records feeding a
Step-4 record). The two summary numbers are **not** the same and must not be conflated:

- **10 dependent records** — Step-4 records that have at least one upstream Step-2 (`dependentRecords`).
- **16 dependency links** — total upstream Step-2 → Step-4 edges (`dependencyLinks`).

They differ because one Step-4 record can have **multiple** upstream Step-2 roots. For example
`storage.04` has **two** upstream roots (`storage.02.account-create.01` +
`storage.02.blob-container-create.02`) and `sreagent.04` has **three**. The earlier "10 dependency
pairs" framing was wrong: there are 10 dependent Step-4 records but 16 upstream Step-2 links.

### `accounting` block (recomputed and pinned)

The manifest carries a top-level `accounting` object. **Every value is recomputed from the records +
inventory** and gated against a pinned expectation (the freeze script fails closed on any
disagreement):

```json
"accounting": {
  "logicalRecords": 34, "physicalCopies": 68,
  "step2Records": 17, "step4Records": 17,
  "dependentRecords": 10, "dependencyLinks": 16,
  "chainRoleCounts": { "root": 24, "cascade": 10 },
  "classificationCounts": { "root": 21, "cascade": 9, "mixed": 3, "diagnostic": 1 },
  "errorClassCounts": { "A": 29, "B": 7, "AB": 3, "C": 1 }
}
```

## Per-record manifest fields

Alongside the unchanged `classification` and `hasUpstreamStep2`, each manifest record adds three
mechanically derived fields that separate chain position from error overlap:

| Field | Meaning |
|-------|---------|
| `chainRole` | `"root"` or `"cascade"` — chain position **only** (see Axis 2 above). Independent of A/B error overlap. |
| `errorClasses` | Array form of `errorClass`: `"A"`→`["A"]`, `"B"`→`["B"]`, `"A+B"`→`["A","B"]`, `"C"`→`["C"]`. |
| `upstreamStableIds` | Sorted stableIds of the same-namespace Step-2 records feeding a Step-4 record (empty for Step-2 records and Step-4 records with no upstream). Every entry references a stableId that exists in the manifest. |

## AI provenance (derived from run logs)

`provenance.ai` in the manifest is derived from the run's **sanitized run logs**
(`source: "run-log"`) — the per-namespace `generated-*/…/logs/example-prompts.log` environment
dumps — **not** from `mcp-tools/sample.env`, and never from `mcp-tools/.env`. The freeze script
scans every `*/logs/*.log` under the source run and extracts the model / API-version identifiers
for Step 2 (`FOUNDRY_MODEL_NAME` / `FOUNDRY_MODEL_API_VERSION`) and Step 4
(`TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME` / `_API_VERSION`).

For this run both steps used identical values (`singleBlock: true`):

- **model**: `gpt-5-mini` (observed in 63 namespaces)
- **apiVersion**: `2025-03-01-preview` (observed in 63 namespaces)
- **temperature** / **seed**: `null` (not configured in code; Azure OpenAI SDK defaults apply)

The **endpoint host and API key are never emitted** — only the non-secret model and api-version
identifiers. If no model/api-version identifier is found in the logs, the script fails closed
(exit 11).

## Stable-ID scheme

Every record gets a deterministic, path- and timestamp-independent stable ID derived only from
record content:

```
{namespace}.{stepId:D2}.{artifactSlug}.{ordinal:D2}
```

For example `storage.02.account-create.01`. Because the ID is content-derived (kebab-cased
artifact name + per-tool ordinal), it is stable across regenerations and does not depend on the
source filename or run timestamp. All 34 IDs are proven collision-free.

## Sanitization placeholder set

Sanitization is a **pure string replacement** on the raw record text (not re-serialization), so
JSON structure, escaped forms (`\u0027`, `\u0022`, `\u002B`), and field ordering are preserved
byte-for-byte apart from the redactions. Line endings are normalized to LF and the BOM is
stripped (UTF-8, no BOM). Replacements are idempotent — a second pass is byte-identical. The
approved placeholder vocabulary is exactly **eight** tokens (`<REPO>`, `<TEMP>`, `<USER>`,
`<USER_HOME>`, `<HOST>`, `<RUNSTAMP>`, `<GUID>`, `<PATH>`), kept in lock-step with
`BaselineContext.ApprovedPlaceholders`; the secret-scan test rejects any angle-bracket token
outside this set.

| Redacted value | Placeholder |
|----------------|-------------|
| Repo root absolute path | `<REPO>` |
| `C:\Users\<name>\AppData\Local\Temp` | `<TEMP>` |
| Other `C:\Users\<name>` | `<USER_HOME>` |
| Username `diberry` | `<USER>` |
| Host / machine name | `<HOST>` |
| `pipeline-runner-step<N>-<32-hex>` | `pipeline-runner-step<N>-<GUID>` |
| `generated-<ns>-YYYY-MM-DD-HH-MM-SS` | `generated-<ns>-<RUNSTAMP>` |
| Any remaining drive-letter absolute path `X:\…` | `<PATH>` (safety net) |

**Retained** (semantically meaningful, never redacted): `recordedAtUtc`, the Azure MCP build
version+SHA, `namespace`, `stepId`, `stepName`, `artifactType`, `artifactName`, `summary`,
`details`, `stepWarnings`, `processInvocations`, `validatorResults`, `failurePolicy`. A
secret-scan test asserts that only the approved placeholder tokens remain and no credential-
shaped literal survives.

## Running the guard tests

The suite is **32 tests** (T1–T24 plus the new `ChainAndAccountingTests` and provenance/inventory
contract tests). On a clean checkout / CI it is GREEN as **31 passed, 1 skipped** — the one skip is
the opt-in deep source-run verify (see below), reported as a *visible* skip, never a silent pass.
The tests build and run as part of the solution — covered by CI via
`dotnet test mcp-doc-generation.sln`. To run them in isolation:

```bash
dotnet test mcp-tools/DocGeneration.Baseline.Beta34.Tests/DocGeneration.Baseline.Beta34.Tests.csproj --configuration Release
```

### Ground-truth guarantees: T3, T4, and T4b

To read the suite correctly, note which test carries which guarantee:

- **T3** (`sanitizedSha256`) — the true **immutability** guarantee: the committed fixture bytes must
  match the `sanitizedSha256` pinned in the manifest.
- **T4** (`sourceSha256` == inventory catalog `sha256`) — a manifest ↔ inventory
  **internal-consistency** check: it proves the two committed artifacts agree, not that either
  matches the live source run.
- **T4b** (`T4b_DeepVerify_LiveSourceRun_Hashes_Match_Inventory`) — the true **ground-truth**
  guarantee: an opt-in deep check that hashes the live (gitignored) source run and matches every
  byte against `source-inventory.json`.

### Opt-in deep source-run verification (`BETA34_VERIFY_SOURCE_RUN`)

T4b cannot run on CI (the source run is gitignored), so it is an explicit opt-in. Without the env
var it reports as a **visible skip**; with it, a missing source run or any hash mismatch **fails**:

```bash
# bash
BETA34_VERIFY_SOURCE_RUN=1 dotnet test mcp-tools/DocGeneration.Baseline.Beta34.Tests/DocGeneration.Baseline.Beta34.Tests.csproj --configuration Release --filter FullyQualifiedName~T4b_DeepVerify
```

```powershell
# Windows PowerShell
$env:BETA34_VERIFY_SOURCE_RUN = "1"; dotnet test mcp-tools/DocGeneration.Baseline.Beta34.Tests/DocGeneration.Baseline.Beta34.Tests.csproj --configuration Release --filter FullyQualifiedName~T4b_DeepVerify
```

## References

- `scripts/baseline/README.md` — freeze-script usage, parameters, and exit codes
- `mcp-tools/DocGeneration.Baseline.Beta34.Tests/README.md` — test map (T1–T24 + new contract tests) and manifest schema
- `.squad/decisions.md` — **AD-028** (baseline fixture freeze architecture)
