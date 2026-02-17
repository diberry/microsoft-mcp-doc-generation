# ExamplePromptGeneratorStandalone — Top 3 Improvement Plan

## 1. Eliminate duplicated models by moving Tool/Option/CliOutput to Shared

**Problem**: `Tool.cs`, `Option.cs`, and `CliOutput.cs` exist in both `CSharpGenerator/Models/` and `ExamplePromptGeneratorStandalone/Models/` as near-identical copies. `FrontmatterUtility.cs` also exists in both packages with different method sets but the same pattern. When a CLI schema change happens (e.g., a new property on `Tool`), two files in two namespaces must be updated in lockstep — and forgetting one is silent because each package deserializes independently.

**Fix**: Move the superset `Tool`, `Option`, and `CliOutput` models into the existing `Shared` project (which both packages already reference). The CSharpGenerator versions are supersets of the ExamplePromptGeneratorStandalone versions, so the standalone package can use the shared models and simply ignore extra properties during `JsonSerializer.Deserialize`. Merge both `FrontmatterUtility` classes into a single shared class in the same project.

**Steps**:
1. Add `Tool.cs`, `Option.cs`, `CliOutput.cs` to `Shared/Models/` using the CSharpGenerator superset definitions (namespace `Shared.Models`)
2. Add a unified `FrontmatterUtility.cs` to `Shared/Utilities/` containing methods from both packages
3. Delete `ExamplePromptGeneratorStandalone/Models/` and `ExamplePromptGeneratorStandalone/Utilities/FrontmatterUtility.cs`
4. Update `using` directives in both packages to reference `Shared.Models` and `Shared.Utilities`
5. Delete `CSharpGenerator/Models/Tool.cs`, `Option.cs`, `CliOutput.cs`, `CSharpGenerator/Generators/FrontmatterUtility.cs`
6. Verify all other packages that consume these models (`ToolGeneration_Raw`, `ToolGeneration_Composed`, etc.) can also switch to the shared versions
7. Build, test, verify 0 warnings

**Impact**: Eliminates 4 duplicate files, reduces future maintenance risk, makes schema changes atomic.

---

## 2. Add unit tests for JSON extraction and prompt parsing

**Problem**: The most critical and fragile code paths — `ExtractJsonFromLLMResponse`, `ParseJsonResponse`, `CleanAIGeneratedText`, and `ExtractJsonFromResponse` (the Program.cs copy) — have zero unit tests. These are pure functions that parse unpredictable LLM output using three fallback strategies (code-block extraction, brace matching, last-block heuristic). Any regression here silently produces empty output across 200+ tools. The code is also duplicated: `ExtractJsonFromLLMResponse` in the generator and `ExtractJsonFromResponse` in `Program.cs` implement the same logic with subtly different behavior (the Program.cs version returns the raw input on failure; the generator version returns empty string).

**Fix**: Create `ExamplePromptGeneratorStandalone.Tests` with targeted test cases for the parsing logic, and consolidate the two duplicate extraction methods into one.

**Test cases to cover**:

| Category | Input | Expected |
|---|---|---|
| Clean JSON | `{"tool": ["p1","p2"]}` | Parsed directly |
| ` ```json ` block | Preamble + ` ```json {...} ``` ` | Extracts from code block |
| Last ` ``` ` block | Reasoning + ` ``` {...} ``` ` | Extracts from last block |
| Brace matching | `Step 1: ... Step 2: ... {"tool": [...]}` | Extracts last `{...}` |
| Nested braces | JSON with nested objects | Correct brace matching |
| Smart quotes | `\u201Cvalue\u201D` | Replaced with straight quotes |
| HTML entities | `&quot;value&quot;` | Replaced with plain characters |
| Trailing commas | `{"tool": ["p1", "p2",]}` | Parsed with `AllowTrailingCommas` |
| Empty response | `""` | Returns null/empty |
| No JSON | `"Here is some reasoning"` | Returns null/empty |
| Multiple JSON blocks | Two code blocks | Extracts last one |

**Steps**:
1. Make `ExtractJsonFromLLMResponse`, `ParseJsonResponse`, and `CleanAIGeneratedText` `internal` (add `InternalsVisibleTo`)
2. Consolidate `ExtractJsonFromResponse` (Program.cs) to call the generator's version
3. Create `ExamplePromptGeneratorStandalone.Tests` project
4. Write tests for the matrix above
5. Add to solution and CI workflow

**Impact**: Catches regressions before they silently affect 200+ generated files. Removes the duplicated extraction method.

---

## 3. Add structured progress tracking and resumability

**Problem**: A full run processes 200+ tools sequentially, taking 15-30 minutes. If the process fails at tool 180 (network blip, rate limit exhaustion, crash), all progress is lost — there's no way to resume from where it left off. The only feedback during the run is per-tool console lines, with no structured tracking of what succeeded, what failed, or why.

**Fix**: Add a progress manifest (`progress.json`) that tracks each tool's generation status, and skip tools that already have successful output on disk. Add a summary report at the end.

**Design**:

```jsonc
// progress.json (written to outputDir after each tool)
{
  "startedAt": "2026-02-17T13:00:00Z",
  "cliVersion": "2.0.0-beta.19",
  "tools": {
    "aks cluster get": { "status": "success", "file": "azure-kubernetes-service-cluster-get-example-prompts.md", "duration": 2.3 },
    "aks node-pool get": { "status": "failed", "error": "Rate limit", "duration": 16.1 },
    "storage account list": { "status": "skipped", "reason": "output exists" }
  }
}
```

**Behavior**:
- On start, load `progress.json` if it exists and skip tools whose output files already exist on disk (resumability)
- Add a `--force` flag to regenerate all tools regardless of existing output
- After each tool, append to `progress.json` (crash-safe incremental writes)
- At completion, write a summary: total/success/failed/skipped counts, total duration, list of failures with error reasons
- Track per-tool duration for performance profiling (identify slow tools or rate-limit patterns)

**Steps**:
1. Add `ProgressTracker` class with `Load`, `RecordSuccess`, `RecordFailure`, `RecordSkip`, `Save` methods
2. In `Program.cs` main loop, check if output file exists → skip (log as "skipped")
3. Wrap each tool generation in a `Stopwatch` for duration tracking
4. Write `progress.json` after each tool (not buffered)
5. Add `--force` CLI flag to bypass skip logic
6. Write `generation-summary.md` at end with pass/fail/skip statistics

**Impact**: A 25-minute run that fails at 90% can resume in 2 minutes instead of restarting from scratch. Failures are explicitly tracked for debugging. Duration data reveals performance bottlenecks.
