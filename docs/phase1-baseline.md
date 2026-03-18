# Phase 1 baseline

## Snapshot
- **Captured:** 2026-03-15T07:31:47.6183963-07:00
- **Repository:** `microsoft-mcp-doc-generation`
- **Branch:** `main` (pre-migration)
- **Scope:** Build + test baseline for Phase 1 IChatClient migration validation

## Solution discovery
- No solution files were found under `docs-generation\`.
- Baseline build used the repo root solution: `docs-generation.sln`.

## Build baseline
- **Command:** `dotnet build .\docs-generation.sln --nologo --tl:off -clp:Summary -v:minimal`
- **Result:** Success
- **Warnings:** 0
- **Errors:** 0
- **Elapsed:** `00:00:46.02`

## Test baseline
- **Scope:** 15 test projects under `docs-generation\`
- **Result:** Success
- **Total tests:** 638
- **Passed:** 638
- **Failed:** 0
- **Skipped:** 0

### Per-project results
| Test project | Total | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| `AzmcpCommandParser.Tests` | 36 | 36 | 0 | 0 |
| `BrandMapperValidator.Tests` | 6 | 6 | 0 | 0 |
| `CSharpGenerator.Tests` | 176 | 176 | 0 | 0 |
| `E2eTestPromptParser.Tests` | 19 | 19 | 0 | 0 |
| `ExamplePromptGeneratorStandalone.Tests` | 95 | 95 | 0 | 0 |
| `ExamplePromptValidator.Tests` | 4 | 4 | 0 | 0 |
| `GenerativeAI.Tests` | 2 | 2 | 0 | 0 |
| `HorizontalArticleGenerator.Tests` | 62 | 62 | 0 | 0 |
| `PipelineRunner.Tests` | 50 | 50 | 0 | 0 |
| `Shared.Tests` | 59 | 59 | 0 | 0 |
| `SkillsRelevance.Tests` | 39 | 39 | 0 | 0 |
| `TemplateEngine.Tests` | 10 | 10 | 0 | 0 |
| `TextTransformation.Tests` | 29 | 29 | 0 | 0 |
| `ToolFamilyValidator.Tests` | 9 | 9 | 0 | 0 |
| `ToolGeneration_Improved.Tests` | 42 | 42 | 0 | 0 |

## Existing warnings or known failures
- No build warnings were observed in the baseline run.
- No failing or skipped tests were observed in the baseline run.
- This baseline was intentionally captured from `main` so Amos can compare post-migration behavior against the pre-migration state.
