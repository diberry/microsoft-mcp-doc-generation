# Decision: Acrolinx Compliance Strategy for Tool-Family Articles

**Date:** 2026-03-25
**Author:** Sage (AI / Prompt Engineer)
**Status:** Proposed
**Triggered by:** Research task from Dina Berry; issues #142-#146; 7/10 tool-family articles failing Acrolinx (below 80)

## Context

Acrolinx is a mandatory quality gate on `MicrosoftDocs/azure-dev-docs-pr` — articles must score 80+ to merge. Our generated tool-family articles currently pass at only 30% rate (3/10). The worst performers (Deploy: 61, Postgres: 64, Cloud Architect: 67) are blocked from shipping.

## Decision

Implement a **6-priority remediation plan** combining prompt changes (P0) and deterministic post-processors (P1-P4):

1. **P0 — System prompt update:** Add explicit Acrolinx compliance rules to `tool-family-cleanup-system-prompt.txt` (sentence length ≤25 words, present tense, active voice, contractions, introductory commas, wordy phrase avoidance).
2. **P1 — JsonSchemaCollapser:** New post-processor to collapse inline JSON schema parameter descriptions into human-readable summaries. Expected +15-20 pts for Deploy.
3. **P1 — ContractionFixer extension:** Add positive contractions ("it is"→"it's", "you are"→"you're", etc.) to existing ContractionFixer.
4. **P2 — WordyPhraseFixer + static replacements:** Deterministic removal of "in order to", "due to the fact that", deprecated Microsoft terms ("Azure AD"→"Microsoft Entra ID"), and ableist language ("simply", "just").
5. **P3 — TenseFixer + AcronymExpander:** Present tense enforcement ("will list"→"lists") and multi-acronym first-use expansion.
6. **P4 — SentenceLengthWarner:** Diagnostic logging for sentences exceeding 25 words (inform, not auto-fix).

## Impact

- **Sage:** Owns prompt change (P0) and all post-processor implementations (P1-P3).
- **Morgan:** May need to adjust FamilyFileStitcher call order if new post-processors are added.
- **Parker:** Must write tests for each new post-processor per AD-007 and AD-010.
- **All namespaces:** Changes apply universally across all 52 namespaces — no service-specific logic.

## Rationale

Post-processing is preferred over prompt-only fixes because it's **deterministic** — a regex that converts "it is" to "it's" always works, while an AI prompt instruction may be ignored 20% of the time. The prompt changes (P0) are still valuable as first-line defense to reduce the volume of issues that post-processors must catch.

Full details in `docs/acrolinx-compliance-strategy.md`.
