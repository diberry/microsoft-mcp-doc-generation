# Advisor golden snapshot

This folder stores the committed golden manifest for the `advisor` behavioral equivalence gate.

## Pipeline steps reference

| Step | Name | Type |
|------|------|------|
| 0 | Annotations & raw tool list | Deterministic |
| 1 | Parameters extraction | Deterministic |
| 2 | Example prompts generation | AI |
| 3 | Tool article composition | AI |
| 4 | Tool family cleanup | AI |
| 5 | Skills relevance mapping | Deterministic |
| 6 | Horizontal articles | AI |

## Capture the initial baseline

From the repo root:

```bash
./capture-golden-baseline.sh advisor
```

By default, that script runs all deterministic steps (`0,1,5`) for the `advisor` namespace and writes `golden-manifest.json` into this folder. To capture the full pipeline including AI steps, pass the steps explicitly:

```bash
./capture-golden-baseline.sh advisor 0,1,2,3,4,5,6
```

## When to update it

Regenerate the manifest only after an intentional output change is reviewed and approved.

## Manual command

```bash
dotnet run --project mcp-tools/DocGeneration.Tools.Fingerprint -- golden capture \
  --namespace advisor \
  --output mcp-tools/DocGeneration.PipelineRunner.Tests/Fixtures/GoldenSnapshot/advisor \
  --repo-root .
```
