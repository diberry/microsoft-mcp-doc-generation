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
| `../../mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures/beta34-baseline-manifest.json` | generated | Provenance + per-record hash manifest (AD-028 schema). |

> The fixtures and manifest are **generated output** — never hand-edit them. To change them,
> fix the script and regenerate.

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
| 9 | `-VerifyOnly` detected drift vs the committed baseline. |

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
3. The regenerated files must pass the secret scan.

Any mismatch ⇒ **exit 9** with a per-item drift report. Exit 0 ⇒ the baseline is provably
reproducible from source.

**Intentionally excluded from the determinism comparison** (they legitimately change each
run and are volatile *provenance*, not baseline content):
`provenance.captureTimestampUtc` and `provenance.toolVersions`. Everything that defines the
frozen artifact (fixtures + record hashes + classification) is deterministic.

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
| `pipeline-runner-step<N>-<32-hex>` | `pipeline-runner-step<N>-<GUID>` |
| `generated-<ns>-YYYY-MM-DD-HH-MM-SS` | `generated-<ns>-<RUNSTAMP>` |
| any remaining drive-letter absolute path `X:\…` | `<PATH>` (safety net) |

**Retained** (semantically meaningful, never redacted): `recordedAtUtc`, the Azure MCP build
version+SHA (`3.0.0-beta.34+eec7…`, retained in its escaped `\u002B` form inside command
strings), `namespace`, `stepId`, `stepName`, `artifactType`, `artifactName`, `summary`,
`details`, `stepWarnings`, `processInvocations`, `validatorResults`, `failurePolicy`.

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

## Provenance / AI values

Model, deployment, and API version are sourced from the **public** template
`mcp-tools/sample.env` (never from `mcp-tools/.env`, which holds secrets and is never read):

- Step 2 (example prompts): `gpt-4.1-mini`, api-version `2025-01-01-preview`
- Step 4 (tool-family cleanup): `gpt-4o`, api-version `2025-01-01-preview`

`temperature` and `seed` are **not configured anywhere in code** (the SDK defaults apply), so
they are recorded as `null` with an explanatory `provenance.ai.note` rather than fabricated.

`promptHashes` / `configHashes` are SHA-256 (uppercase hex) of the actual Step 2 / Step 4
prompt and config files under `mcp-tools/DocGeneration.Steps.*/prompts/` and `mcp-tools/data/`.
