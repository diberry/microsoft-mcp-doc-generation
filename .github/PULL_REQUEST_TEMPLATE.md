# Pull Request

## Summary

<!-- Brief description of what this PR does -->

## Issue Reference

<!-- Closes #XXX -->

## Change Classification

<!-- Check the boxes that apply -->

- [ ] Pipeline generation logic (Steps 0–6, templates, prompts, scripts)
- [ ] Configuration data (brand-to-server-mapping, common-parameters, etc.)
- [ ] Infrastructure only (CI, docs, tooling — no generation impact)
- [ ] Test-only changes

## Pipeline Regression Evidence

<!-- 
Required for any PR touching generation logic or configuration.
The pipeline-output-regression workflow generates this automatically.
Copy the relevant section from workflow artifacts, or fill manually.
-->

```yaml
regression_evidence:
  fingerprint_gate: # pass | fail | skip
  prompt_regression_gate: # pass | fail | skip
  deterministic_dry_run: # pass | fail | skip
  ai_dry_run: # pass | fail | skip
  namespaces_tested: []
  affected_steps: []
  baseline_date: # YYYY-MM-DD
  notes: ""
```

## Validation Checklist

- [ ] `dotnet build mcp-doc-generation.sln --configuration Release` passes (zero warnings)
- [ ] `dotnet test mcp-doc-generation.sln --configuration Release` passes
- [ ] Pipeline regression workflow passed (or N/A for infrastructure-only changes)
- [ ] CHANGELOG.md updated
- [ ] Documentation updated (if applicable)

## Reviewer Notes

<!-- Any context reviewers need to evaluate this PR -->
