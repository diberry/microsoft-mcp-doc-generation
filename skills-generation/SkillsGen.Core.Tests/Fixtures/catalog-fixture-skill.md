---
name: azure-keyvault
display_name: Azure Key Vault
description: "Work with Azure Key Vault secrets and certificates."
---

# Azure skill for Azure Key Vault

Manage secrets, keys, and certificates stored in Azure Key Vault.

## Use cases

- Retrieve secrets for application configuration
- Rotate encryption keys on a schedule
- Issue and renew TLS certificates

## Negative use cases

- Storing large binary blobs
- Replacing a full PKI infrastructure

## Azure services

| Service | UseWhen |
|---------|---------|
| Azure Key Vault | Manage secrets and keys |

## Prerequisites

- Azure subscription
- Azure CLI installed
- Key Vault Secrets Officer role

### RBAC

| Role | Scope |
|------|-------|
| Key Vault Secrets Officer | Key Vault resource |

## MCP Tools

| Tool | Command | Purpose |
|------|---------|---------|
| keyvault_secret_get | keyvault secret get | Retrieve a secret value |

## Workflow

1. Authenticate with Azure
2. Select the target Key Vault
3. Retrieve or set the secret

## Decision Guidance

### Key vs Secret

| Option | BestFor |
|--------|---------|
| Secret | Passwords and connection strings |
| Key    | Encryption operations |

## Related Skills

- @azure-storage for storing encrypted data
- @azure-monitor for audit logs

## New Section Without Mapping

This heading has no mapping rule defined.
