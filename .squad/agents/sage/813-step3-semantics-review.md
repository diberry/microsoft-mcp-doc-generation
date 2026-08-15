# Sage — Semantics Review: PR #816 (Step 3 Canonical Parameter Contract)

**Branch:** `squad/813-step3-canonical-parameter-contract` | **Head:** `ee3ca02` | **Base:** `b58431f`

---

## Verdict: **APPROVE WITH NOTES**

| Severity | Count |
|----------|-------|
| Blocking | 0 |
| High | 2 |
| Medium | 3 |
| Low | 2 |

---

## 1. Source-Intent Protection — Adversarial Prompt Analysis

### Test cases and code-derived outputs

| # | Input Prompt | Missing Param (canonical=`account`, display=`Account name`) | Code-Derived Output |
|---|---|---|---|
| A1 | `"Do **not** delete the configuration store."` | account is Missing | `"Do **not** delete the configuration store for account 'contoso-account-01'."` — verb/negation preserved ✅ |
| A2 | `"List entries in <app_config_store_name> scoped to label 'prod'."` | `app_config_store_name` is NOT in placeholderAliases (`["account","account_name","account-name","account_name"]`), so account remains Missing | Appends clause. Intent (scoped to label) preserved ✅, unknown placeholder preserved ✅ |
| A3 | `"Show account settings for subscription 'sub1'."` | `CheckConcreteValue` word-boundary finds "account" in stripped prose → Concrete ✅ | Byte-identical (no repair). ✅ |
| A4 | `"List all config settings."` (already ends with period) | Missing | `"List all config settings for account 'contoso-account-01'."` — period spliced correctly via `InjectParameter` logic (period is final char, clause inserted before it) ✅ |
| A5 | `"Show keys where name contains 'account-test'."` | `CheckConcreteValue` matches `account` inside stripped prose at word-boundary? Let's trace: strippedPrompt = `"Show keys where name contains 'account-test'."`. Pattern `(?<!\w)account(?!\w)` does NOT match `account-test` because hyphen `-` is `\w`=false but position after "account" is `-"` which is not `\w`. Actually in regex `\w` = `[a-zA-Z0-9_]`, so `-` is NOT `\w`. So `(?!\w)` at the position after "account" (before `-`) succeeds → **false positive: reports Concrete when the word "account" is part of a compound in a literal value.** |
| A6 | Multi-clause: `"Fetch config keys, then export them to a JSON file."` | Missing | `"Fetch config keys, then export them to a JSON file for account 'contoso-account-01'."` — multi-clause preserved ✅ |
| A7 | Idempotence: Run A4 output through `Repair` again | `CheckConcreteValue` finds "account" (word boundary) in injected clause `"for account 'contoso-account-01'"` → Concrete | Byte-identical on second pass ✅ |
| A8 | Prompt already ending in `"!": "Deploy now!"` | Missing | `"Deploy now for account 'contoso-account-01'!"` — punctuation transplanted ✅ |

**Judgment:** Source-intent protection is sound. Prompt count, order, scenario, action/verb, negation, and scope are all preserved. The only concern is A5 (see Finding H-1).

---

## 2. Byte-Identity When Covered & Idempotence

The manifest-aware overload (`DeterministicPromptRepairer.Repair(prompts, manifest)` at line 80) evaluates coverage first, and only if `missingParams.Count > 0` does it modify prompts. After repair, re-evaluation finds the injected clause via `CheckConcreteValue` word-boundary → `Concrete`, so a second pass finds no missing params. **Idempotence holds.**

For the legacy overload (still the production call), `GetEffectiveCoverage` is checked first, and if true, `continue` skips injection. Same idempotence guarantee.

---

## 3. Boundedness

- Manifest overload: iterates `missingParams` (subset of `manifest.Parameters`), each injects exactly one clause. No unbounded growth across runs (idempotence proved above).
- Legacy overload: iterates `requiredParameters` once, skips covered ones.
- ✅ Bounded.

---

## 4. Unknown Upstream Placeholders

`CheckAuthorizedPlaceholder` at line 147: looks up `rawToken` in `placeholderAliasIndex`, and only if the result maps to the current parameter's canonical name does it report a match. Tokens not in the index are ignored. ✅

They cannot satisfy coverage (only `Concrete` or `AuthorizedPlaceholder` count). ✅

---

## 5. Semantic Soundness of the Alias Contract

Working through requested identities:

| canonicalName | displayName | placeholderAliases (derived) | Judgment |
|---|---|---|---|
| `account` | `Account name` | `["account", "account_name", "account-name", "account_name"]` (deduped: `["account","account_name","account-name"]`) | ✅ Defensible — covers common placeholder forms |
| `resource-group` | `Resource group` | `["resource-group","resource_group","resource-group","resource_group"]` → `["resource-group","resource_group"]` | ✅ |
| `vmss-name` | `Virtual machine scale set (VMSS) name` | `["vmss-name","vmss_name","virtual-machine-scale-set-vmss-name","virtual_machine_scale_set_vmss_name"]` | ⚠️ Normalizer strips `(` and `)`, resulting in `vmss` being joined with `name`. Long but not harmful — would match `<vmss-name>` and `<vmss_name>`. Acceptable. |
| `auth-method` | `Authentication method` | `["auth-method","auth_method","authentication-method","authentication_method"]` | ✅ |
| `param` | `Parameter` | `["param","param","parameter","parameter"]` → `["param","parameter"]` | ✅ |
| `test-run-id` | (assume `Test run ID`) | `["test-run-id","test_run_id","test-run-id","test_run_id"]` → `["test-run-id","test_run_id"]` | ✅ Won't confuse with `old-test-run-id` (different canonical, different placeholder aliases) |
| `old-test-run-id` | `Old test run ID` | `["old-test-run-id","old_test_run_id","old-test-run-id","old_test_run_id"]` → `["old-test-run-id","old_test_run_id"]` | ✅ Distinct from `test-run-id` |
| `hostpool-resource-id` | `Host pool resource ID` | `["hostpool-resource-id","hostpool_resource_id","host-pool-resource-id","host_pool_resource_id"]` | ✅ |
| `knowledge-base` | `Knowledge base` | `["knowledge-base","knowledge_base","knowledge-base","knowledge_base"]` → `["knowledge-base","knowledge_base"]` | ✅ |
| `directory-path` | `Directory path` | `["directory-path","directory_path","directory-path","directory_path"]` → `["directory-path","directory_path"]` | ✅ |
| `cloud-endpoint-name` | `Cloud endpoint name` | `["cloud-endpoint-name","cloud_endpoint_name","cloud-endpoint-name","cloud_endpoint_name"]` → `["cloud-endpoint-name","cloud_endpoint_name"]` | ✅ |

**Judgment:** The alias derivation is semantically sound. No over-authorization of different concepts; no under-authorization of same-concept tokens.

---

## 6. No Unknown-Failure Fallback

The manifest-aware overload's post-repair verification (line 160-168) re-evaluates every required param and appends to `stillUncovered` if not covered. This list is returned in the `RepairResult`. No silent success. ✅

---

## 7. Repair Telemetry Honesty

The original defect: heuristic reports `actions: []` and `stillUncovered: []` while coverage is absent.

- **Legacy overload (still production):** `GetEffectiveCoverage` at line 237 still uses `ParameterCoverageChecker.GetConcretePromptCoverage` with `Covered || PlaceholderDetected`. The original defect (`account` ⊂ `app_config_store_name` via Contains-matching) is **not fixed** in the legacy overload — it depends on what `ParameterCoverageChecker` does internally. See Finding H-2.
- **Manifest overload:** Telemetry is honest. `stillUncovered` is populated via canonical evaluator re-check. ✅

---

## 8. Production Routing: Legacy vs Canonical

**Finding H-2 (High):** The production call site (`Program.cs:358`) still invokes `DeterministicPromptRepairer.Repair(prompts, requiredOptions)` — the legacy `Option`-based overload. This overload uses `ParameterCoverageChecker` (heuristic), NOT `CanonicalCoverageEvaluator`. The manifest-aware overload exists but is not wired into the production pipeline.

The AD-030 §8 seam map says this is a known disposition: the manifest overload is introduced as a **seam** that Step 3 proves via unit tests, and production wiring is deferred. The semantic consequence is that **the original defect (false-positive coverage for `account`) remains exploitable in production until the manifest overload is wired in.** This is acceptable for Step 3's scope (proving the contract), but must be tracked.

---

## Findings Table

| # | Severity | File:Line | Finding |
|---|---|---|---|
| H-1 | High | `CanonicalCoverageEvaluator.cs:117` | `CheckConcreteValue` word-boundary pattern `(?<!\w)account(?!\w)` false-positives on hyphenated compound tokens like `account-test` in prose because `-` is not `\w`. A prompt containing the *literal string* `'account-test'` (a value for a different param) would falsely satisfy coverage for `account`. Mitigant: after `PlaceholderRegex.Replace`, delimited tokens are removed, so `<account-test>` won't match. But non-delimited compounds in natural language prose (e.g. "the account-level setting") would false-match. In practice unlikely for required params, but semantically imprecise. |
| H-2 | High | `Program.cs:358` | Production still routes through legacy heuristic. Original defect remains exploitable until manifest overload is wired. Expected per AD-030 §9 non-goals, but must be a tracked item. |
| M-1 | Medium | `DeterministicPromptRepairer.cs:110-115` | The `needsPrepend` heuristic checks if any covered param's canonical name appears as a word in any prompt to decide prepend vs append strategy. This is fragile — a prompt containing "for account 'foo'" (already covered) would flip all prompts to prepend mode. Result: `"For missing-param 'val': original prompt."` which reverses clause order relative to append mode. Not meaning-altering but format-inconsistent across runs depending on prompt content. |
| M-2 | Medium | `CanonicalAliasDeriver.cs:45` | `NonAlphaNumHyphen` regex in normalizer replaces unrecognized chars with `-` (line 45 of normalizer). For displayName `"Virtual machine scale set (VMSS) name"`, parentheses become hyphens before collapse → `"virtual-machine-scale-set-vmss-name"`. This inflates placeholderAliases with long tokens unlikely to appear in prompts. Not harmful but adds noise to the index. |
| M-3 | Medium | `DeterministicPromptRepairer.cs:62-63` | In the legacy overload, `CredentialSanitizer.Sanitize` is applied AFTER repair but post-repair `GetStillUncovered` re-evaluates on sanitized prompts. If sanitization strips an injected value (e.g., a value that looks like a credential), `stillUncovered` correctly reports it. However the **returned `RepairedPrompts`** are the pre-sanitized versions (line 72). The caller must sanitize again before writing to disk. This is a latent integrity gap if a future caller forgets. |
| L-1 | Low | `CanonicalCoverageEvaluator.cs:147-148` | Raw token lookup uses dictionary default comparer (likely ordinal case-sensitive). If `PlaceholderAliasIndex` was built case-insensitively by the loader, this is fine. But if it's case-sensitive, `<Account>` raw token won't match key `"account"`. The normalized fallback at line 154-158 covers this, so functional impact is nil — just redundant code path. |
| L-2 | Low | `DeterministicPromptRepairer.cs:324` | When prompt has no final punctuation, `InjectParameter` appends `". Specify {name} '{value}'."` — a different template from the clause-before-punctuation case (`" for {name} '{value}'"` + original punctuation). The two formats are inconsistent, though both achieve coverage. |

---

## 9. Rubric — Semantic Identity Coverage and Repair Safety

### Dimensions

| Dimension | Weight | Pass Threshold | Measurement |
|---|---|---|---|
| **Coverage Accuracy** | 30% | ≥95% true-positive rate; 0 false-negatives on required params | Evaluate 20+ adversarial prompts per namespace sample (5 namespaces min). Count: correct Concrete/AuthorizedPlaceholder/Missing verdicts vs ground truth. |
| **False-Positive Resistance** | 25% | ≤2% false-positive rate | Prompts containing parameter names as substrings of unrelated words/values must NOT satisfy coverage. Test with compound words, quoted values containing param slugs, and prose descriptions. |
| **Repair Preservation** | 20% | 0 semantic inversions; 0 prompt count changes; 0 order changes | Diff pre-repair vs post-repair: assert verb, negation, scope, action, clause count (excluding appended clause), and prompt array length unchanged. |
| **Idempotence** | 10% | Byte-identity on double-pass for 100% of cases | Run `Repair(Repair(prompts, manifest), manifest)` for every test case; assert `.SequenceEqual`. |
| **Boundedness** | 10% | Clause count ≤ missing param count per prompt; total injected chars ≤ (missingCount × 80) | Measure post-repair prompt length delta; assert bounded by formula. |
| **Telemetry Honesty** | 5% | `stillUncovered` = re-evaluated missing set; `actions` = exactly the set injected | Assert `stillUncovered.Count + actions.Count(covered) == requiredParams.Count` invariant. |

### Scoring

- Each dimension scored 0-100.
- Weighted composite ≥ 90 → **Pass**
- Any single dimension < 70 → **Fail** (regardless of composite)
- Coverage Accuracy or False-Positive Resistance < 80 → **Fail**

### Application

This rubric applies to:
1. Unit test assertions for `CanonicalCoverageEvaluator`
2. Integration test assertions when manifest overload is wired into production
3. Nondeterministic AI output gate (post-AI-generation, pre-disk-write validation)

---

## Summary

The canonical evaluator, normalizer, and alias deriver are **semantically sound**. The bounded-repair contract in the manifest-aware overload correctly preserves prompt intent, enforces idempotence, honestly reports uncovered params, and caps growth. The alias derivation algorithm produces defensible identity sets for all tested Azure parameters.

The two High findings are: (1) a word-boundary false-positive edge case in `CheckConcreteValue` that could be tightened, and (2) the production pipeline not yet consuming the canonical evaluator (expected per AD-030 scope). Neither is blocking for Step 3's stated goal of *proving the contract*.

---

**File:** `.squad/agents/sage/813-step3-semantics-review.md`

↩︎ Responding to: "You are **Sage**, AI / Prompt Engineer. You hold the **semantics / bounded-repair / rubric approval seat** for Step 3 of issue diberry/microsoft-mcp-doc-generation#813..."
