## 🔍 Parker — QA Review

**Verdict:** Needs follow-up before QA sign-off. The suite is mostly deterministic and isolated, but a few tests are too weak to protect the intended contract.

**Strengths**
- Good method-level naming and organization; the file is easy to scan by behavior area.
- Low flake risk: tests are pure/in-memory with no clock, file, network, or random dependencies.
- Nice coverage of core happy paths: canonicalization, blank-prompt handling, sanitizer interaction, and per-prompt coverage checks.

**Concerns**
- `Repair_InjectedValueDestroyedBySanitizer_AppearsInStillUncovered` is effectively a no-op test right now. It only does `Assert.NotNull(result)`, so it would still pass if `StillUncovered`, `Actions`, and repaired output were all wrong.
- `Integration_KeyVault_CertificateData_RepairThenSanitize_Passes` is labeled as full-flow, but the final assertion only checks that each prompt still contains `certificate`. Those prompts already contained that word before repair, so this does not prove `certificate-data` coverage after sanitize.
- `ValueBank_SharedKeysMatchOriginalGenerator` says “match original generator” but only asserts key existence. It will not catch value drift, ordering drift, or a changed first-choice value.
- `IsValidValue_*` covers only a narrow subset of the safety surface. Missing cases include tabs/other control chars, `\r`, Unicode/emoji, angle-bracket or backtick placeholder-like strings, and SQL-ish payloads. Even if those are allowed, the tests should define that contract explicitly.
- Theory usage is under-applied. The resolve-heuristic cases and punctuation-injection variants would be cleaner and broader as table-driven tests.

**Recommendations**
- Strengthen the sanitizer-destroyed test to assert exact expected behavior: whether the repaired value is rejected up front, whether `StillUncovered` contains the canonical param, and whether the injected value/action list matches expectation.
- Make the Key Vault and Cosmos “integration” cases reuse the same post-sanitize `ParameterCoverageChecker` assertions as the generic integration test, ideally per prompt where the contract is “every non-blank prompt covers every required param.”
- Rename or strengthen `ValueBank_SharedKeysMatchOriginalGenerator` so it verifies actual value parity, not just key presence.
- Expand `IsValidValue` with a true edge-case matrix via `[Theory]`/`[InlineData]` (or `MemberData`) so the safety boundary is explicit and regression-resistant.
