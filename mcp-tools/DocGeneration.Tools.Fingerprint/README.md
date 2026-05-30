# DocGeneration.Tools.Fingerprint

Structural regression tooling for Azure MCP documentation output.

## Commands

```bash
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint -- snapshot
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint -- diff --baseline before.json --candidate after.json
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint -- golden capture --namespace advisor --repo-root .
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint -- golden verify --manifest mcp-tools/DocGeneration.PipelineRunner.Tests/Fixtures/GoldenSnapshot/advisor/golden-manifest.json --output-dir generated-advisor
```

## What it does

- `snapshot` captures high-level fingerprints across generated namespaces.
- `diff` compares two fingerprint snapshots and reports structural drift.
- `golden capture` records per-file golden baselines for the behavioral equivalence CI gate.
- `golden verify` enforces byte-identical deterministic files and structural AI-file tolerances.

## Golden manifest behavior

- Deterministic directories: `annotations/`, `parameters/`, `h2-headings/`, `cli/`, `reports/`, `logs/`, `common-general/`, plus namespace-root files.
- AI directories: `tools/`, `tool-family/`, `horizontal-articles/`, `example-prompts/`, `e2e-test-prompts/`.
- Deterministic files are compared by SHA-256.
- AI files are compared by required top-level keys and `##`/section counts with a tolerance of ±1.
