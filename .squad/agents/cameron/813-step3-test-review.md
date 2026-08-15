# Cameron — Deterministic Gate / Mutation Adequacy Review

**PR:** #816 (branch `squad/813-step3-canonical-parameter-contract`)  
**HEAD:** `ee3ca02` | **Base:** `b58431f`  
**Reviewed:** 2026-08-15  
**Verdict:** **APPROVE WITH NOTES**

---

## 1. Test-First Discipline

Commit order from `git log --oneline --stat b58431f..HEAD`:

1. `e34981f` — **tests first** (2,062 insertions, 13 files: all `*Tests*` files + RED evidence)
2. `6461ad9` — RED-verified evidence file
3. `50b5f35` — **implementation** (1,146 insertions in production code)
4. `65e8180` — mutation proof evidence
5. `186b942` — Round 2 (tests + impl together for 5 new defects)
6. `458f2a3` — Round 3 RED tests (177 lines, tests only)
7. `345ab85` — Round 3 fix (implementation)
8. `abf7286` / `84cdacb` / `ee3ca02` — evidence consolidation + docs

**Finding:** TDD discipline upheld for the primary round. Round 2 (`186b942`) mixed tests and implementation in one commit — acceptable given the compile-RED shape (tests reference new signatures that can only compile with the implementation present). RED evidence was captured pre-implementation in `813-step3-red-round2.txt`.

---

## 2. Gate Coverage Matrix

| Required Gate Item | Test(s) Covering It |
|---|---|
| Identity/coverage matrix for required parameters | `CanonicalCoverageEvaluatorTests.EvaluateParameterCoverage_AllRequiredCovered_ReturnsTrue`, `..._MissingRequired_ReturnsFalse`, 16 `RealisticIdentity_ExactPlaceholderMatch` InlineData cases |
| Canonical/alias/placeholder collisions and ambiguity | `Load_AliasCollision_ThrowsAliasCollision`, `Load_AliasShadowsCanonical_ThrowsAliasShadowsCanonical`, `Load_NormalizationCollision_ThrowsNormalizationCollision`, `Load_PlaceholderMultiBind_ThrowsPlaceholderMultiBind`, `Load_DuplicateCanonical_ThrowsDuplicateCanonical`, `EvaluateSingleParameter_ContainsSubstring_NeverAuthorizes` |
| Stale manifest fails closed | `Load_StaleBuild_ThrowsSourceStale` (M4g PROVEN) |
| Malformed manifest fails closed | `Load_MalformedJson_ThrowsMalformed` (M4b PROVEN) |
| Incompatible manifest fails closed | `Load_UnknownSchemaVersion_ThrowsSchemaUnknown` (M4c PROVEN) |
| Missing manifest fails closed | `Load_MissingFile_ThrowsNotFound` (M4a PROVEN), `ExamplePromptsStep_LoadRequiredOptions_MissingManifest_MustThrow` (M19 PROVEN) |
| Shared resolver at repair seam | `CanonicalRepairTests.Repair_WithManifest_*` (8 tests) |
| Shared resolver at retry/Step 2 seam | `ExamplePromptsStepManifestFailureTests` (5 tests) |
| Shared resolver at Step-4-applicable seam | `ParameterCrossCheckCanonicalLoaderTests` (3 tests) |
| Prompt count/order/scenario preservation | `Repair_WithManifest_PreservesPromptCountAndOrder` |
| Bounded repair | `Repair_WithManifest_MissingCoverage_AppendsOneBoundedClause` |
| Idempotent repair | `Repair_WithManifest_Idempotent_SecondPassIsByteIdentical` |
| RED→GREEN | Evidence files: `813-step3-red-run.txt` (exit 1), `813-step3-green-run.txt` (exit 0 minus tolerated) |
| Mutation/revert | `813-step3-mutation-proof.txt`: 19/24 PROVEN rows |

**Uncovered gate item:** M18 (`ExamplePromptsStep_ManifestError_RecordsArtifactFailure_NotJustWarning`) is `Assert.True(true, ...)` — a tautological test that always passes. See Finding F1.

---

## 3. Mutation Adequacy — Personally Reproduced Rows

| Row | Mutation | Test | My Observed Result |
|---|---|---|---|
| **M1** (PROVEN) | `parameter.CanonicalName` → `parameter.DisplayName` in CheckAuthorizedPlaceholder | `EvaluateSingleParameter_AuthorizedPlaceholder_AccountName_ReturnsAuthorizedPlaceholder` | **FAIL**: `Assert.Equal() Failure: Expected "account_name", Actual "account-name"` ✅ |
| **M7** (PROVEN) | schemaVersion `"2.0"` → `"1.0"` in ParameterGenerator.cs:333 | `EmitsV2ManifestLoadableByCanonicalLoader` | **FAIL**: `ParameterManifestException: unrecognized schemaVersion '1.0'` ✅ |
| **M5** (NOT PROVEN) | `CoverageVerdict.Missing` → `CoverageVerdict.Ambiguous` in EvaluateSingleParameter return | Named test `Ambiguous_NeverTreatedAsCovered` | Named test **PASSES** (confirms NOT PROVEN for that test). However, 14 other evaluator tests **FAIL** (they assert `Missing`). The mutation IS killed by the suite — evidence understates coverage. |
| **M18** (NOT PROVEN) | Any mutation to ExamplePromptsStep catch block | `ExamplePromptsStep_ManifestError_RecordsArtifactFailure_NotJustWarning` | **PASSES** regardless — body is `Assert.True(true, ...)` ✅ Confirmed tautological. |

---

## 4. Anti-Gaming Audit

- `git diff b58431f..HEAD` on test files: **+3,278 insertions, −163 deletions**.
- One `[Fact]` removed: `EnumParam_PromptReferencesAllowedValue_IsValidTrue` in `CodeBasedPromptValidatorTests.cs` — this test validated legacy heuristic enum matching that was replaced by the canonical contract. Two other tests were renamed to canonical-contract equivalents (`UnauthorizedPlaceholder_IsNotCovered`, `ConcreteValue_IsCovered`).
- No `[Fact(Skip=...)]` added. No `#if false`. No test quarantined.
- Net: **+119 tests, 1 removal** (justified by architectural migration).

---

## 5. Pre-Existing Failures

My independent run (`dotnet test mcp-doc-generation.sln --no-build --configuration Release`):
- **1 xUnit failure**: `FamilyMetadataGeneratorTests.GenerateAsync_WhenAiResponseIsTruncated_UsesFallbackDescription` — matches known pre-existing.
- **Pester**: Not re-run (separate infrastructure), evidence claims 8 failures matching known pre-existing. Accepted per `docs/test-strategy.md` §1.5.
- No new failures observed.

---

## 6. Evidence Completeness

| Evidence File | SHA | Env Versions | Commands | Exit Codes | Assessment |
|---|---|---|---|---|---|
| `813-step3-red-run.txt` | `b58431f` ✔ | 10.0.303, 7.6.5, git 2.55 | `dotnet build` | exit 1 | Complete; lists all 119 compile-RED tests |
| `813-step3-red-verified.txt` | `e34981f` ✔ | Same | `dotnet build` | exit 1 | Complete; 74 diagnostics all AD-030 types |
| `813-step3-red-round2.txt` | `65e8180` ✔ | Same | N/A (compile-RED) | N/A | Adequate; explains unavoidable compile-RED |
| `813-step3-red-round3.txt` | `186b942` ✔ | Same | `dotnet build` + `dotnet test --filter` | exit 0, exit 1 | Complete; 3 of 4 fail, 1 structural passes |
| `813-step3-green-run.txt` | `abf7286` ✔ | Same | `dotnet build` + `dotnet test` + Pester | 0, 1, 8 | Complete; includes reconciliation |
| `813-step3-mutation-proof.txt` | `abf7286` ✔ | Same | Protocol stated | N/A | 19 PROVEN/5 NOT PROVEN — honestly reports all NOT PROVEN with rationale |

**Prior structural-claim correction:** The evidence explicitly marks M18 as "NOT PROVEN — the test is a structural placeholder that always passes." The earlier "PROVEN (structural)" claim has been corrected.

---

## 7. Test-Total Discrepancy Resolution

| Source | Total | Explanation |
|---|---|---|
| Evidence claim | 3,803 | All 23 test assemblies in `mcp-doc-generation.sln` |
| My independent run | 3,803 (3,802 pass + 1 fail) | Same assemblies, same counts per assembly |
| "Independent reviewer ~3,604" | 3,604 | **Most likely explanation**: excluded `skills-generation/skills-generation.slnx` (which has 152 tests in a separate solution) AND possibly used `--filter` or ran a subset of assemblies. Alternatively: ran at baseline `b58431f` which had 3,684 and confused with a partial run. |

**Resolution:** The true count at HEAD `ee3ca02` is **3,803** (23 assemblies). The `skills-generation` suite (152 tests, separate .slnx) is NOT in `mcp-doc-generation.sln` and accounts for the ~199-test gap if one reviewer counted it and the other didn't, or if the reviewer ran at a prior commit with fewer tests and had build issues with some projects.

---

## 8. Findings

| # | Severity | Finding | Location |
|---|---|---|---|
| F1 | **Medium** | `ExamplePromptsStep_ManifestError_RecordsArtifactFailure_NotJustWarning` is tautological: `Assert.True(true, ...)`. It passes regardless of any mutation. Should be replaced with a behavioral test or removed. | `Round3CanonicalContractTests.cs:153` |
| F2 | **Low** | M5 evidence says "NOT PROVEN" but my reproduction shows 14 other tests kill the mutation. Evidence understates suite-level mutation kill rate. Not a coverage gap, but evidence could be more accurate. | `813-step3-mutation-proof.txt:137-140` |
| F3 | **Low** | M8/M9/M11 (bounded repair, idempotence, prompt order preservation) are NOT PROVEN due to pattern ambiguity between two Repair overloads. Evidence honestly reports this. The behavioral tests exist and pass on green — they just can't be triggered by a single-line mutation due to code structure. Acceptable technical debt. | `813-step3-mutation-proof.txt:158-181` |
| F4 | **Low** | One test removed (`EnumParam_PromptReferencesAllowedValue_IsValidTrue`) without explicit note in evidence. Justified by architecture change but should have been mentioned in green-run evidence. | `CodeBasedPromptValidatorTests.cs` (base vs HEAD diff) |
| F5 | **Low** | Green-run evidence assembly breakdown sums to 3,802 but states "TOTAL: 3803" — off-by-one in the `32` entry (31 pass + 1 skip = 32 total but only 31 counted in pass column). Cosmetic. | `813-step3-green-run.txt:43` |

---

## Verdict

### **APPROVE WITH NOTES**

**0 Blocking, 0 High, 1 Medium, 4 Low**

The test suite demonstrates genuine TDD discipline with compile-RED → GREEN progression. 19 of 24 mutation rows are PROVEN with honest reporting of the 5 NOT PROVEN rows. The one tautological test (F1/M18) is a placeholder that guards nothing today — it should be converted to a behavioral integration test in a follow-up PR, but does not block this change because the gate item it was meant to cover (fail-closed manifest errors) IS covered by M19 and the `ExamplePromptsStepManifestFailureTests` suite.

---

↩︎ Responding to: "You are **Cameron**, Test Lead. You hold the **deterministic-gate / mutation-adequacy approval seat** for Step 3 of issue diberry/microsoft-mcp-doc-generation#813..."
