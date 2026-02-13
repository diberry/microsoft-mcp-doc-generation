---
title: Use Azure MCP Server with Keyvault
description: Learn how to use Azure Model Context Protocol (MCP) Server to interact with Keyvault using natural language commands through AI assistants.
ms.date: 2026-02-13 19:17:29 UTC
ms.topic: how-to
ms.custom: mcp-integration, devx-track-ai
ms.service: keyvault
---

# Use Azure MCP Server with Keyvault

Azure Model Context Protocol (MCP) Server enables AI assistants like GitHub Copilot, Claude Desktop, and others to interact with Keyvault through natural language commands. This integration allows you to manage securely manage keys, secrets, and certificates. without writing code or remembering complex CLI syntax.

## Overview

Keyvault is a cloud service that safeguards cryptographic keys and secrets used by cloud applications and services. It provides centralized management, access control, and secure storage for sensitive data and cryptographic keys, ensuring your applications remain secure.

With Azure MCP Server integration, you can use natural language to:

- Retrieve and list secrets, certificates, and keys.
- Create and import secrets, certificates, and keys.
- Manage access policies for secure operations.

## Prerequisites

Before using Azure MCP Server with Keyvault, ensure you have:

- **Azure MCP Server installed and running** - Follow the [Azure MCP Server setup guide](https://github.com/microsoft/azure-mcp-server)
- **AI assistant configured** - GitHub Copilot, Claude Desktop, or another MCP-compatible client
- **Azure credentials** - Authentication configured for your Azure subscription
- **Existing Key Vault resource** - You need a provisioned Key Vault to manage keys and secrets.

## Available MCP tools

Azure MCP Server provides the following tools for Keyvault:

- **[keyvault admin settings get](../parameters/keyvault-admin-settings-get-parameters.md)** - Retrieve settings for Managed HSM account in a vault.
- **[keyvault certificate create](../parameters/keyvault-certificate-create-parameters.md)** - Generate a new certificate in Azure Key Vault.
- **[keyvault certificate get](../parameters/keyvault-certificate-get-parameters.md)** - List certificates or get details of a specific certificate.
- **[keyvault certificate import](../parameters/keyvault-certificate-import-parameters.md)** - Upload an existing certificate into Azure Key Vault.
- **[keyvault key create](../parameters/keyvault-key-create-parameters.md)** - Create a new key with specified parameters in Key Vault.
- **[keyvault key get](../parameters/keyvault-key-get-parameters.md)** - List keys or retrieve details of a specific key.
- **[keyvault secret create](../parameters/keyvault-secret-create-parameters.md)** - Create a new secret with a specified value in Key Vault.
- **[keyvault secret get](../parameters/keyvault-secret-get-parameters.md)** - List all secrets or retrieve a specific secret value.

For detailed parameter information and usage examples, see the [Keyvault MCP tools reference](../tools/keyvault.md).

## Common scenarios

### Scenario 1: Create a new secret for credentials.

You need to securely store a new database password for your application.

**Example commands you can ask your AI assistant:**

- "Create a new secret called 'db-password' in Key Vault 'prod-kv'."
- "Set the secret value for 'db-password' in Key Vault 'prod-kv'."
- "Store the database API key in the key vault named 'production-secrets'."

**Expected outcome:**  
You should see the new secret 'db-password' created in 'prod-kv'.

### Scenario 2: Import an existing certificate into Key Vault.

You have a certificate file that you need to securely store in Key Vault.

**Example commands you can ask your AI assistant:**

- "Import my SSL certificate from path 'C:/certs/mycert.pfx' to vault 'webapp-kv'."
- "Upload the PFX certificate to Key Vault 'secure-certificates'."
- "Load the existing certificate 'my-cert' into Key Vault 'prod-certs'."

**Expected outcome:**  
The existing certificate should now be available in 'webapp-kv'.

### Scenario 3: List all keys in Key Vault.

You want to review all keys that are currently stored in your Key Vault.

**Example commands you can ask your AI assistant:**

- "Get the list of all keys from Key Vault 'test-kv'."
- "Show the keys available in the vault 'production-keys'."
- "Retrieve details of specific key 'encryption-key' in Key Vault 'secure-kv'."

**Expected outcome:**  
A list of all keys should be displayed, including their details.



## Authentication and permissions

To use Keyvault through Azure MCP Server, ensure your Azure identity has appropriate permissions:

**Required Azure RBAC roles:**

- **Key Vault Reader** - Allows you to view secrets, keys, and certificates.
- **Key Vault Contributor** - Allows you to create and manage secrets, keys, and certificates.

**Additional authentication notes:**

Ensure you are authenticated using Azure Active Directory to access Key Vault.

## Troubleshooting

### Common issues

**Access Denied Error.**

You receive an error indicating access is denied when trying to access a secret.

**Resolution:** Check your role assignments and ensure you have permissions on the Key Vault.


## Best practices

- **Use Azure AD for authentication.** - Prefer Azure Active Directory over shared keys for better security.
- **Regularly rotate secrets and keys.** - Enhance security by frequently updating keys and secrets.
- **Enable soft-delete and purge protection.** - Avoid accidental data loss by enabling vault protection features.
- **Use access policies thoughtfully.** - Assign least privilege roles and review them regularly.

## Related content

- [Azure MCP Server documentation](https://github.com/microsoft/azure-mcp-server)
- [Keyvault MCP tools reference](../tools/keyvault.md)
- [Keyvault documentation](https://learn.microsoft.com/en-us/azure/key-vault/general/overview)
- [Key Vault Best Practices](https://learn.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [Quickstart: Create a Key Vault](https://learn.microsoft.com/en-us/azure/key-vault/quick-create-portal)

---

*This article was generated for Azure MCP Server version 2.0.0-beta.19+526b8facdd707f352913f84af0195268a22dea6f on 2026-02-13 19:17:29 UTC*
