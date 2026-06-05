# Skills Content Architecture Audit Report

Generated: 2026-06-04 17:01:12 -07:00

## Summary

| Check | Status | Details |
|-------|--------|---------|
| A. Inventory Coverage | ⚠️ | 24 generated, 1 pending, 190 source folders not in inventory |
| B. Identity Completeness | ✅ | 25/25 fully complete, version coverage 8% |
| C. User-Facing Coverage | ❌ | 22/24 pass all required sections |
| D. Internal Leakage | ⚠️ | 5 leakage findings |
| E. Conditional Rendering | ✅ | 0 conditional sections found, 0 mismatches (presence/absence only; Tier 1 assumed offline) |
| F. Unmapped Headings | ⚠️ | 40 unmapped headings across 6 skills |
| G. Source Sufficiency | ✅ | 6/6 sources sufficient, 6 prompt-source warnings |

## Conformance Matrix

| Skill | Identity ✓ | Source ✓ | Article ✓ | Coverage ✓ | No Leakage ✓ | Conditional ✓ | Unmapped |
|-------|---|---|---|---|---|---|---|
| azure-ai | ✓ | ✓ | ✓ | ✗ | ✓ | ✓ | 8 |
| azure-aigateway | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-hosted-copilot-sdk | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| microsoft-foundry | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-kusto | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-messaging | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-storage | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-deploy | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | 7 |
| azure-prepare | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-upgrade | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-validate | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| airunway-aks-setup | ✓ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | ⏸️ | n/a |
| azure-compute | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-enterprise-infra-planner | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-kubernetes | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | 9 |
| azure-cloud-migrate | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| appinsights-instrumentation | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-cost | ✓ | ✓ | ✓ | ✗ | ⚠️ | ✓ | 8 |
| azure-diagnostics | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-quotas | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | 1 |
| azure-resource-lookup | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-resource-visualizer | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-compliance | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |
| azure-rbac | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | 7 |
| entra-app-registration | ✓ | ⏸️ | ✓ | ✓ | ✓ | ✓ | n/a |

## Detailed Findings

### C. User-Facing Coverage (failures only)

- **azure-ai**
  - ## Example prompts missing or empty
- **azure-cost**
  - ## Example prompts missing or empty

### D. Internal Leakage (findings only)

- **azure-cost**
  - MCP tool name: azure__documentation
  - MCP tool name: azure__extension_cli_generate
  - MCP tool name: azure__get_azure_bestpractices
  - MCP tool name: azure__extension_azqr
  - MCP tool name: azure__aks

### E. Conditional Rendering (mismatches only)

> No conditional rendering mismatches detected. Offline validation is limited to section presence/absence.

### F. Unmapped Headings

- **azure-ai**: Troubleshooting, Best Practices, Decision Making, Limits & Quotas, Security, Configuration, Integrations & Coding Patterns, Deployment
- **azure-deploy**: Troubleshooting, Best Practices, Limits & Quotas, Security, Configuration, Integrations & Coding Patterns, Deployment
- **azure-kubernetes**: Troubleshooting, Best Practices, Decision Making, Architecture & Design Patterns, Limits & Quotas, Security, Configuration, Integrations & Coding Patterns, Deployment
- **azure-cost**: Troubleshooting, Best Practices, Decision Making, Limits & Quotas, Security, Configuration, Integrations & Coding Patterns, Deployment
- **azure-quotas**: Limits & Quotas
- **azure-rbac**: Troubleshooting, Best Practices, Decision Making, Limits & Quotas, Security, Configuration, Integrations & Coding Patterns

**Mapping warnings**
- No matching source folder found for 'azure-aigateway'
- No matching source folder found for 'azure-hosted-copilot-sdk'
- No matching source folder found for 'microsoft-foundry'
- No matching source folder found for 'azure-kusto'
- No matching source folder found for 'azure-messaging'
- No matching source folder found for 'azure-storage'
- No matching source folder found for 'azure-prepare'
- No matching source folder found for 'azure-upgrade'
- No matching source folder found for 'azure-validate'
- No matching source folder found for 'airunway-aks-setup'
- No matching source folder found for 'azure-compute'
- No matching source folder found for 'azure-enterprise-infra-planner'
- No matching source folder found for 'azure-cloud-migrate'
- No matching source folder found for 'appinsights-instrumentation'
- No matching source folder found for 'azure-diagnostics'
- No matching source folder found for 'azure-resource-lookup'
- No matching source folder found for 'azure-resource-visualizer'
- No matching source folder found for 'azure-compliance'
- No matching source folder found for 'entra-app-registration'

### G. Source Sufficiency (warnings only)

- **azure-ai**
  - No example-prompt source reference detected (expected triggers.test.ts or curated JSON reference)
- **azure-deploy**
  - No example-prompt source reference detected (expected triggers.test.ts or curated JSON reference)
- **azure-kubernetes**
  - No example-prompt source reference detected (expected triggers.test.ts or curated JSON reference)
- **azure-cost**
  - No example-prompt source reference detected (expected triggers.test.ts or curated JSON reference)
- **azure-quotas**
  - No example-prompt source reference detected (expected triggers.test.ts or curated JSON reference)
- **azure-rbac**
  - No example-prompt source reference detected (expected triggers.test.ts or curated JSON reference)
