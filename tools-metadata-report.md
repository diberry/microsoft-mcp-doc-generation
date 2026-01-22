# Azure MCP Tools Metadata Report

**Generated:** 2026-01-22 23:49:13 UTC
**Total Tools:** 221

This comprehensive report provides detailed metadata analysis for all Azure MCP tools, including security requirements, operational characteristics, and safety considerations.

## Executive Summary

| Characteristic | Count | Percentage | Description |
|----------------|-------|------------|-------------|
| Secrets Required | 2 | 0.9% | Tools that handle sensitive information |
| Local Consent Required | 5 | 2.3% | Tools requiring explicit user consent |
| Destructive Operations | 58 | 26.2% | Tools that can delete or modify resources |
| Read-Only Operations | 149 | 67.4% | Tools that only read data without modifications |
| Non-Idempotent | 52 | 23.5% | Tools where repeated calls may have different effects |
| High-Risk (Secrets + Consent) | 0 | 0.0% | Tools requiring both secrets and user consent |

## Security Requirements

### Tools Requiring Secrets

**Count:** 2 tools

These tools handle sensitive information like passwords, keys, or tokens and require secure handling.

**Summary by Service Area:**

- **keyvault:** 2 tools

**Detailed List:**

| Command | Area | Description |
|---------|------|-------------|
| `keyvault secret create` | keyvault | Create/set a secret in an Azure Key Vault with the specified name and value. Required: --vault <v... |
| `keyvault secret get` | keyvault | Get/retrieve/show details for a single secret in an Azure Key Vault (latest version). Not for lis... |

