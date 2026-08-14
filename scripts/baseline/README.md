# beta.34 baseline freeze (`scripts/baseline/`)

Deterministically freezes the **34 logical catalog critical-failure records** from run
`generated-20260813T162453` as immutable, sanitized, secret-free test fixtures plus a
provenance manifest. This is **Step 1 of issue #813** — freezing the baseline only; it does
**not** fix the underlying A/B/C/D defects (later steps).

Pinned Azure MCP build: `3.0.0-beta.34+eec7acccddab1e16be852a3c3b9503cc9adf7538`.

## Files

| Path | Owner | Purpose |
|------|-------|---------|
| `New-Beta34Baseline.ps1` | Quinn (DevOps) | The freeze script (generate + `-VerifyOnly`). |
| `beta34-classification.json` | Parker (QA) | Per-record classification (stableId, role, errorClass, hasUpstreamStep2, rationale). **Input** to the script; Quinn does not author it. |
| `evidence/generate-run.txt` | Quinn | Captured transcript of the generation run. |
| `evidence/verify-run.txt` | Quinn | Captured transcript of the determinism proof. |
| `../../mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/critical-failures/*.json` | generated | 34 sanitized fixtures, one per stable ID. |
| `../../mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/beta34-baseline-manifest.json` | generated | Provenance + per-record hash + `accounting` manifest (AD-028 schema). |
| `../../mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/source-inventory.json` | generated | 68-entry physical-copy inventory (committed proof of the 68→34 accounting + source-hash integrity). |
| `../../mcp-tools/DocGeneration.Baseline.Beta34.Tests/.gitattributes` | Quinn (DevOps) | EOL lock (`-text`) for the 36 hash-pinned artifacts so a Windows clone can't CRLF-corrupt the pinned SHAs. |

> The fixtures, manifest, and inventory are **generated output** — never hand-edit them. To change
> them, fix the script and regenerate. The `.gitattributes` is hand-authored and must stay in sync
> with the produced artifact set.

## Regeneration

```bash
# From the repo root (PowerShell 7):
pwsh -File scripts/baseline/New-Beta34Baseline.ps1
```

Parameters (all optional; defaults shown):

| Param | Default | Notes |
|-------|---------|-------|
| `-SourceRunPath` | `generated-20260813T162453` | Read-only source run. Never edited. |
| `-OutputRoot` | `mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures` | Where fixtures + manifest are written. |
| `-ClassificationPath` | `scripts/baseline/beta34-classification.json` | Parker's classification file. |
| `-VerifyOnly` | *(switch)* | Determinism proof — see below. Does **not** write to `-OutputRoot`. |

The script fails **closed** (nonzero exit, precise message) on any of:

| Exit | Meaning |
|------|---------|
| 2 | Source run / catalog dir missing, or catalog record count ≠ 34. |
| 3 | Classification file missing / invalid / not exactly 34 entries. |
| 4 | A source record has no classification entry, or a classification entry is an orphan. |
| 5 | Duplicate-copy accounting failed (a logical record does not have exactly 1 catalog + 1 namespace physical copy). |
| 6 | Sanitization is not idempotent for some record. |
| 7 | `-VerifyOnly` run with no committed baseline present. |
| 8 | Secret scan hit (forbidden literal or credential-shaped pattern) in a produced file. |
| 9 | `-VerifyOnly` detected drift vs the committed baseline (fixtures, manifest hashes, or inventory). |
| 10 | Computed `accounting` disagrees with the pinned expectation, or an `upstreamStableIds` value references a non-existent stableId. |
| 11 | AI provenance could not be derived — no model / api-version identifiers found in the run's `*/logs/*.log`. |

## Determinism / verify contract

```bash
pwsh -File scripts/baseline/New-Beta34Baseline.ps1 -VerifyOnly
```

`-VerifyOnly` regenerates the entire baseline into a throwaway temp directory
(`scripts/baseline/.verify-tmp-<guid>`, auto-deleted) and compares the **deterministic
outputs** against the committed baseline:

1. **Every fixture** must be **byte-identical** (SHA-256) to the committed fixture.
2. **Every manifest record's** `sourceSha256` and `sanitizedSha256` must match the committed
   manifest.
3. **Every `source-inventory.json` physical copy** must match on `relativePath`, `sha256`,
   `copyKind`, `logicalIdentity`, and `stableId` (the `generatedAtUtc` field is excluded).
4. The regenerated files must pass the secret scan.

Any mismatch ⇒ **exit 9** with a per-item drift report. Exit 0 ⇒ the baseline is provably
reproducible from source.

**Intentionally excluded from the determinism comparison** (they legitimately change each
run and are volatile *provenance*, not baseline content):
`provenance.captureTimestampUtc`, `provenance.toolVersions`, and the inventory's
`generatedAtUtc`. Everything that defines the frozen artifact (fixtures + record hashes +
classification + accounting + per-copy inventory hashes) is deterministic. (The
`provenance.ai` block is derived from the committed run logs and is therefore also stable, but
it is not part of the byte-for-byte comparison set.)

The script also self-checks idempotency inline: every record is sanitized twice and the two
passes must be byte-identical (exit 6 otherwise).

## Sanitization contract

**Strategy: pure string replacement** on the raw record text (not re-serialization), so the
JSON structure — including escaped forms `\u0027`, `\u0022`, `\u002B` and field ordering — is
preserved byte-for-byte apart from the redactions. Only line endings are normalized to **LF**
and the **BOM is stripped** on write (UTF-8, no BOM).

Redactions (applied in order, case-insensitive, matching both `\\`-escaped and raw/`/` forms):

| From | To |
|------|----|
| repo root absolute path (derived dynamically) | `<REPO>` |
| `C:\Users\<name>\AppData\Local\Temp` | `<TEMP>` |
| other `C:\Users\<name>` | `<USER_HOME>` |
| literal username `diberry` | `<USER>` |
| host / `$env:COMPUTERNAME` | `<HOST>` |
| `pipeline-runner-step<N>-<32-hex>` | `<GUID>` (inside `pipeline-runner-step<N>-<GUID>`) |
| `generated-<ns>-YYYY-MM-DD-HH-MM-SS` | `<RUNSTAMP>` (inside `generated-<ns>-<RUNSTAMP>`) |
| any remaining drive-letter absolute path `X:\…` | `<PATH>` (safety net) |

### Placeholder token vocabulary (Q5 — 8 tokens, aligned with the C# allowlist)

The sanitizer's **complete** approved placeholder vocabulary is exactly these **eight** tokens,
kept in lock-step with `ApprovedPlaceholders` in
`DocGeneration.Baseline.Beta34.Tests/BaselineContext.cs`:

`<REPO>` `<TEMP>` `<USER>` `<USER_HOME>` `<HOST>` `<RUNSTAMP>` `<GUID>` `<PATH>`

Of these, only **`<REPO>`, `<TEMP>`, `<RUNSTAMP>`, and `<GUID>`** actually occur in the current
beta.34 fixtures. The other four (`<USER>`, `<USER_HOME>`, `<HOST>`, `<PATH>`) are **defensive**
rules that would fire on a different capture environment but produce no match in this run's data.
The `T22` sanitization test rejects any angle-bracket token outside this eight-token set.

**Retained** (semantically meaningful, never redacted): `recordedAtUtc`, the Azure MCP build
version+SHA (`3.0.0-beta.34+eec7…`, retained in its escaped `\u002B` form inside command
strings), `namespace`, `stepId`, `stepName`, `artifactType`, `artifactName`, `summary`,
`details`, `stepWarnings`, `processInvocations`, `validatorResults`, `failurePolicy`.

## EOL lock (`.gitattributes`) — hash-pinned artifact protection

The 36 hash-pinned artifacts (34 fixtures + `beta34-baseline-manifest.json` +
`source-inventory.json`) are committed with **LF** endings, but the repo-root `.gitattributes`
only declares `* text=auto`. With `core.autocrlf=true` (the Windows default) a fresh clone would
rewrite them to CRLF on checkout, changing their bytes and **breaking the pinned SHA-256 values**
(proven: `eventhubs.04` `5d13fbba…` → `3187f4e9…` under CRLF).

`mcp-tools/DocGeneration.Baseline.Beta34.Tests/.gitattributes` marks all 36 artifacts **`-text`**
(binary-exact, no EOL conversion on any OS). Proof after regeneration:

```bash
git ls-files --eol -- <the 36 paths>     # every row: i/lf  w/lf  attr/-text
git check-attr text -- <a fixture>       # -> text: unset
git checkout -- <a fixture>              # SHA-256 unchanged after checkout
```

When the artifact set changes, `git add --renormalize` the fixture paths (and `git add` any new
artifact) so the index and attributes stay consistent.

## Classification file schema (input contract)

The script accepts **either** of these shapes for `beta34-classification.json` (matched to
source records robustly, so ordering never matters):

**A. Map keyed by catalog filename** (what Parker's file currently uses):

```json
{
  "appconfig--20260813T234620128Z-step-02-tool-appconfig-kv-get-01.json": {
    "stableId": "appconfig.02.kv-get.01",
    "classification": "root",
    "errorClass": "A",
    "hasUpstreamStep2": false,
    "rationale": "…"
  }
}
```

**B. Array of records** (fallback), each with `namespace`, `stepId`, `artifactName`,
`stableId`, `classification`/`role`, `errorClass`, `hasUpstreamStep2`, `rationale`.

Stable-ID scheme (AD-028): `{namespace}.{stepId:D2}.{artifactSlug}.{ordinal:D2}`, e.g.
`storage.02.account-create.01`.

## Manifest per-record chain fields (Q4)

Each manifest record keeps the single AD-028 taxonomy — `classification`
(`root`/`cascade`/`mixed`/`diagnostic`) and `hasUpstreamStep2` — **unchanged**, and adds three
fields that separate *chain position* from *error overlap* (the two were previously conflated):

| Field | Meaning |
|-------|---------|
| `chainRole` | `"root"` or `"cascade"` — chain position **only**, derived mechanically. A Step-4 record with ≥1 Step-2 record in the **same namespace** is `cascade`; every Step-2 record and every Step-4 record with no namespace-mate Step-2 is `root`. Independent of A/B error overlap. |
| `errorClasses` | Array form of `errorClass`: `"A"`→`["A"]`, `"B"`→`["B"]`, `"A+B"`→`["A","B"]`, `"C"`→`["C"]`. |
| `upstreamStableIds` | Sorted stableIds of the same-namespace Step-2 records feeding a Step-4 record (empty for Step-2 records and for Step-4 records with no upstream). Every entry is guaranteed to reference a stableId that exists in the manifest. |

`chainRole` ≠ `classification` on purpose: e.g. `postgres.04` and `loadtesting.04` are `mixed`
(A+B error overlap) but `chainRole=root` (no upstream Step-2), while `foundryextensions.04` is
`mixed` **and** `chainRole=cascade` (it has upstream). All three fields are derived mechanically
in the script from the source records + `beta34-classification.json`; `beta34-classification.json`
was **not** modified (mechanical derivation from `namespace`+`stepId` was sufficient).

## Accounting block (Q4)

A top-level `accounting` object records the reconciled totals. **Every number is computed from the
data**; the script then gates each against a pinned expectation and **fails closed (exit 10)** on
any disagreement rather than forcing a value:

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

`dependentRecords` (10 downstream Step-4 records) and `dependencyLinks` (16 upstream Step-2 links)
differ because one Step-4 record can have multiple upstream Step-2 roots (e.g. `storage.04` ←
`storage.02.account-create.01` + `storage.02.blob-container-create.02`; `sreagent.04` has 3).
`errorClassCounts` counts records whose `errorClasses` contains each class, so `A`+`B` sum to more
than 34 (the 3 `A+B` records are counted in both, and separately as `AB`).

## Source inventory (`source-inventory.json`, Q1)

The source run directory is gitignored, so the 68→34 duplicate accounting and per-copy source-hash
integrity are also emitted as a **committed** artifact provable from the repo alone:

```json
{
  "schemaVersion": "1.0.0",
  "sourceRunDir": "generated-20260813T162453",
  "generatedAtUtc": "…",
  "physicalCopyCount": 68,
  "logicalRecordCount": 34,
  "physicalCopies": [
    {
      "relativePath": "generated-20260813T162453/critical-failures/appconfig--…json",
      "copyKind": "catalog",
      "sha256": "<UPPERCASE HEX of raw source bytes>",
      "logicalIdentity": "appconfig|2|appconfig kv get|2026-08-13T23:46:20.1282129Z",
      "stableId": "appconfig.02.kv-get.01"
    }
  ]
}
```

68 entries (34 `catalog` + 34 `namespace` copies), sorted by `relativePath`. Paths are **relative
only**; the secret scan runs over this file too, so no user name, host name, or absolute path can
leak.

## Provenance / AI values (Q3 — derived from run logs)

Model and API version are **derived from the run's own sanitized logs** — the per-namespace
`generated-*/…/logs/example-prompts.log` environment dumps (`source: "run-log"`) — **not** from
`mcp-tools/sample.env`, and **never** from `mcp-tools/.env`. The generator scans every
`*/logs/*.log` under the source run, extracts the `FOUNDRY_MODEL_NAME` /
`FOUNDRY_MODEL_API_VERSION` (Step 2) and `TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME` /
`TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION` (Step 4) identifiers, and records every distinct
value with its `namespacesObserved` count and sample `evidence` log paths.

For this run both steps used the same values, so `provenance.ai.singleBlock` is `true`:

- **model**: `gpt-5-mini` (observed in 63 namespaces)
- **apiVersion**: `2025-03-01-preview` (observed in 63 namespaces)

Step 2 and Step 4 are still reported in their own sub-objects (`step2ExamplePrompts` /
`step4ToolFamilyCleanup`) with the env keys they were read from; if the two steps ever diverge the
block records the distinct values per step. The **endpoint host and API key are never emitted**
(non-secret model + api-version identifiers only). `temperature`/`seed` are not configured in code
(Azure OpenAI SDK defaults apply) and are recorded `null`. If no model/api-version identifier is
found in the logs, the script fails closed (exit 11).

`promptHashes` / `configHashes` are SHA-256 (uppercase hex) of the actual Step 2 / Step 4
prompt and config files under `mcp-tools/DocGeneration.Steps.*/prompts/` and `mcp-tools/data/`.
