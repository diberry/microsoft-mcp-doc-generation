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
| Guard tests | `mcp-tools/DocGeneration.Baseline.Beta34.Tests/*.cs` | Source (24 xUnit tests) |

The fixtures and manifest are **generated output** — never hand-edit them. To change them, edit
the freeze script and regenerate.

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
(SHA-256), and every manifest record's `sourceSha256` / `sanitizedSha256` must match. Any drift
exits non-zero with a per-item report; exit 0 means the baseline is provably reproducible. Two
provenance fields that legitimately change per run — `provenance.captureTimestampUtc` and
`provenance.toolVersions` — are excluded from the comparison. The script also fails closed
(non-zero, precise message) on missing source, wrong record count, invalid classification,
orphaned records, duplicate-copy accounting errors, non-idempotent sanitization, or any secret
scan hit. See `scripts/baseline/README.md` for the full exit-code table.

## Classification taxonomy

Each of the 34 records carries two independent classifications (see
`scripts/baseline/beta34-classification.json`):

**Role** — where the record sits in a failure chain:

| Role | Meaning | beta.34 count |
|------|---------|---------------|
| `root` | An originating failure, not caused by another record | 21 |
| `cascade` | A downstream failure caused by an upstream one | 9 |
| `mixed` | Exhibits both root and cascade characteristics | 3 |
| `diagnostic` | A diagnostic/observability record rather than a distinct defect | 1 |

**Error class** — the defect category:

| Error class | Meaning | beta.34 count |
|-------------|---------|---------------|
| `A` | Class-A defect | 26 |
| `B` | Class-B defect | 4 |
| `A+B` | Both Class-A and Class-B present | 3 |
| `C` | Class-C defect | 1 |

**Class-D dependency accounting** is tracked separately from role via the boolean
`hasUpstreamStep2` (10 records have an upstream Step 2 dependency). This is reconciled
independently of `role=cascade` (9) so the two accountings can be asserted separately.

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
stripped (UTF-8, no BOM). Replacements are idempotent — a second pass is byte-identical.

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

The 24 tests build and run as part of the solution — they are covered by CI via
`dotnet test mcp-doc-generation.sln`. To run them in isolation:

```bash
dotnet test mcp-tools/DocGeneration.Baseline.Beta34.Tests/DocGeneration.Baseline.Beta34.Tests.csproj --configuration Release
```

## References

- `scripts/baseline/README.md` — freeze-script usage, parameters, and exit codes
- `mcp-tools/DocGeneration.Baseline.Beta34.Tests/README.md` — test map (T1–T24) and manifest schema
- `.squad/decisions.md` — **AD-028** (baseline fixture freeze architecture)
