# Changelog

All notable changes to the Azure MCP Documentation Generator are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- **Prompt regression testing framework** — Runner script (`prompt-regression.sh`) + 5 regression comparison tests + baselines for 5 representative namespaces. Detects quality regressions when prompts change. (PR #NNN, Issue #214)
- **CI integration documentation** — Local development commands, CI pipeline structure, test project inventory, and debugging guide. (PR #328, Issue #213)
- **Baseline fingerprinting tool**(`DocGeneration.Tools.Fingerprint`) — Snapshot and diff generated output for regression detection. Supports `--snapshot` and `--diff` modes with CI-gatable exit codes. 58 tests. (PR #324, Issue #209)
- **Comprehensive README documentation navigation** — 22 documents organized across 8 categories replacing flat 6-link list. (PR #325)
- **Prompt review P0/P1 fixes** — Removed redundancy, dead code, and fixed bugs across pipeline prompts. (PR #323, Issue #294)
- **Shared Acrolinx rules** — Standardized compliance rules across all AI system prompts. (PR #323)
