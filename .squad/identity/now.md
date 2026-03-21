---
updated_at: 2026-03-20T23:45:00.000Z
focus_area: Content correctness across all pipeline stages
active_issues: []
---

# What We're Focused On

**Mission:** Consistently correct content across all 52 namespaces at every pipeline stage (Steps 0-6).

## Pipeline Overview (6 stages, 4 use AI)

| Step | Name | AI? | Key Risk |
|------|------|-----|----------|
| 0 | Bootstrap (global) | No | Build/CLI extraction failures |
| 1 | Annotations + Parameters + Raw Tools | No | Malformed annotations, missing params |
| 2 | Example Prompts | **Yes** | Token truncation, JSON parse failures |
| 3 | Compose + AI Improve | **Yes** | Leaked template tokens, truncation |
| 4 | Tool-Family Article | **Yes** | Validation failures, AI non-compliance |
| 5 | Skills Relevance | No (GitHub API) | Rate limiting (non-fatal) |
| 6 | Horizontal Articles | **Yes** | Parse errors, fabricated content |

## Team Assignments

- **Avery:** Pipeline architecture, cross-stage contracts, quality gate design
- **Morgan:** C# generators, templates, config files, leaked token fixes
- **Quinn:** Scripts, Docker, CI/CD, preflight validation, error reporting
- **Sage:** AI prompts, fabrication detection, content validation rules
- **Parker:** Test coverage, regression detection, 52-namespace validation
- **Scribe:** Decision logging, session history, knowledge preservation
