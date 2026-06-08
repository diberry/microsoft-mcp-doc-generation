---

title: Azure MCP Server tools for Azure Key Vault
description: Use Azure MCP Server tools to manage keys, secrets, and certificates in Azure Key Vault with natural language prompts from your IDE.
ms.date: 05/31/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 8
mcp-cli.version: 3.0.0-beta.13+cd8d1e8f9924440b33e3e908c390c1599700ccba
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Key Vault

The Azure MCP Server lets you manage Azure Key Vault resources, including: create, get, and import, with natural language prompts.

Azure Key Vault is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure Key Vault documentation](/azure/keyvault/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Admin settings: Get

Retrieves all account-level settings for a Managed HSM vault in Azure Key Vault, including purge protection and soft-delete retention days. It doesn't return secrets, keys, or certificates and applies only to Managed HSM vaults.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli keyvault admin settings get -->

Example prompts include:

- "Get the account settings for Managed HSM vault 'my-hsm-vault'."
- "Show me the account settings for key vault 'prod-hsm'."
- "What's the value of the 'purge-protection' setting in key vault 'hsm-managed-001'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Vault name** |  Required | The name of the Key Vault. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp keyvault admin settings get \
  --vault <vault>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault` | string | Yes | The name of the Key Vault. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Certificate: Create

Creates and issues a new certificate in Azure Key Vault using the default certificate policy. Requires the vault name, certificate name, and subscription. Optionally include a tenant. Returns certificate properties such as name, id, keyId, secretId, cer (base64), thumbprint, enabled, notBefore, expiresOn, createdOn, updatedOn, subject, and issuerName. If a certificate with the same name exists, it creates a new certificate version.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli keyvault certificate create -->

Example prompts include:

- "Create a new certificate 'webapp-cert' in key vault 'prod-kv'."
- "Generate a certificate 'dbserver-cert' in key vault 'db-kv'."
- "Request creation of certificate 'api-cert-v2' in the key vault 'api-kv'."
- "Provision a new key vault certificate 'tls-cert' in vault 'security-kv'."
- "Issue certificate 'client-auth-cert' in key vault 'auth-kv'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Certificate name** |  Required | The name of the certificate. |
| **Vault name** |  Required | The name of the Key Vault. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp keyvault certificate create \
  --vault <vault> \
  --certificate <certificate>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault` | string | Yes | The name of the Key Vault. |
| `--certificate` | string | Yes | The name of the certificate. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Certificate: Get

Lists all certificates in an Azure Key Vault, or retrieves a specific certificate by name. Returns either the list of certificate names in the vault, or full certificate details, including key ID, secret ID, thumbprint, and certificate policy.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli keyvault certificate get -->

Example prompts include:

- "Show me the certificate 'web-ssl' in key vault 'prod-kv'."
- "Show the details of certificate 'db-cert' in key vault 'my-keyvault'."
- "Get certificate 'api-client-cert' from key vault 'app-secrets-kv'."
- "Display certificate details for 'tls-cert' in key vault 'dev-kv'."
- "Retrieve certificate metadata for 'backup-cert' in key vault 'corp-keyvault'."
- "List all certificates in key vault 'prod-kv'."
- "Show me the certificates in key vault 'my-keyvault'."
- "What certificates are in key vault 'shared-kv'?"
- "List certificate names in vault 'app-secrets-kv'."
- "Enumerate certificates in key vault 'dev-kv'."
- "Show certificate names in key vault 'corp-keyvault'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Vault name** |  Required | The name of the Key Vault. |
| **Certificate name** |  Optional | The name of the certificate. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp keyvault certificate get \
  --vault <vault> \
  [--certificate <certificate>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault` | string | Yes | The name of the Key Vault. |
| `--certificate` | string | No | The name of the certificate. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Certificate: Import

Imports an existing certificate into Azure Key Vault from a `PFX` or `PEM` file that includes the private key. Accepts a file path, a base64-encoded `PFX`, or raw `PEM` text beginning with `-----BEGIN`. If the `PFX` is password-protected, provide the password. Requires the `Vault name`, `Certificate name`, `Certificate data`, and subscription. Optional values include the password for a `PFX` and tenant. The command returns the certificate name, id, keyId, secretId, and the `cer` value (base64). It also returns the thumbprint, enabled state, notBefore, expiresOn, createdOn, updatedOn, subject, and issuer. If a certificate with the same name exists, the command creates a new certificate version.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli keyvault certificate import -->

Example prompts include:

- "Import certificate data 'certs/prod-app.pfx' into vault 'kv-prod' with certificate name 'prod-app-cert'."
- "Upload certificate data 'certs/staging.pem' to vault 'my-keyvault' as certificate 'staging-app-cert'."
- "Import base64 certificate data 'MIIFqjABBgkqhkiG9w0BAQEFAAOCAQ8A' into vault 'prod-kv' with certificate name 'prod-ssl-cert'."
- "Add raw PEM certificate data '-----BEGIN CERTIFICATE-----MIIC...-----END CERTIFICATE-----' into vault 'webapp-kv' as certificate 'webapp-tls'."
- "Import password-protected PFX certificate data 'certs/secure.pfx' into vault 'secure-kv' with certificate name 'secure-cert' and password '\<secure-password\>'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Certificate data** |  Required | The certificate content: path to a PFX/PEM file, a base64 encoded PFX, or raw PEM text beginning with -----BEGIN. |
| **Certificate name** |  Required | The name of the certificate. |
| **Vault name** |  Required | The name of the Key Vault. |
| **Password** |  Optional | Optional password for a protected PFX being imported. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp keyvault certificate import \
  --vault <vault> \
  --certificate <certificate> \
  --certificate-data <certificate-data> \
  [--password <password>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault` | string | Yes | The name of the Key Vault. |
| `--certificate` | string | Yes | The name of the certificate. |
| `--certificate-data` | string | Yes | The certificate content: path to a PFX/PEM file, a base64 encoded PFX, or raw PEM text beginning with -----BEGIN. |
| `--password` | string | No | Optional password for a protected PFX being imported. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ✅

## Key: Create

Creates a new key in an Azure Key Vault. Creates the key with the specified name and key type in the specified vault. Supports key types `RSA`, `RSA-HSM`, `EC`, and `EC-HSM`. `RSA-HSM` and `EC-HSM` require a premium SKU vault. If a key with the same name already exists, creates a new key version. Returns key metadata, including name, id, keyId, keyType, enabled, notBefore, expiresOn, createdOn, and updatedOn.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli keyvault key create -->

Example prompts include:

- "Create key 'app-signing-key' with key type 'RSA' in vault 'prod-kv'."
- "Generate key 'backup-key' with key type 'RSA-HSM' in key vault 'secure-kv'."
- "In key vault 'auth-kv', create key 'ec-auth-key' with key type 'EC'."
- "Create key 'hsm-ec-key' with key type 'EC-HSM' in vault 'payments-kv'."
- "Can you create key 'service-enc-key' with key type 'RSA' in key vault 'service-kv'?"

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Key name** |  Required | The name of the key to retrieve/modify from the Key Vault. |
| **Key type** |  Required | The type of key to create. Valid values: `RSA`, `RSA`-HSM, EC, EC-HSM. Note: RSA-HSM and EC-HSM require a premium SKU vault. |
| **Vault name** |  Required | The name of the Key Vault. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp keyvault key create \
  --vault <vault> \
  --key <key> \
  --key-type <key-type>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault` | string | Yes | The name of the Key Vault. |
| `--key` | string | Yes | The name of the key to retrieve/modify from the Key Vault. |
| `--key-type` | string | Yes | The type of key to create. Valid values: RSA, RSA-HSM, EC, EC-HSM. Note: RSA-HSM and EC-HSM require a premium SKU vault. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌

## Key: Get

Lists all keys in the specified Azure Key Vault, or retrieves a specific key by name. Returns key details such as key type, whether it's enabled, and expiration dates. Include managed keys to show keys that Azure manages. For example, list keys in vault 'contoso-kv', or get key 'backup-key'.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli keyvault key get -->

Example prompts include:

- "Show me the key 'app-signing-key' in the key vault 'my-keyvault'."
- "Show the details of key 'db-encryption' in the key vault 'prod-kv'."
- "Get the key 'api-key' from vault 'security-kv'."
- "Display the key details for 'backup-key' in vault 'backup-kv'."
- "Retrieve key metadata for 'token-signing' in vault 'auth-kv'."
- "List all keys in the key vault 'my-keyvault'."
- "Show me the keys in the key vault 'prod-kv'."
- "What keys are in the key vault 'security-kv'?"
- "List key names in vault 'backup-kv'."
- "Enumerate keys in key vault 'audit-kv' with include-managed 'true'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Vault name** |  Required | The name of the Key Vault. |
| **Include managed** |  Optional | Whether or not to include managed keys in results. |
| **Key name** |  Optional | The name of the key to retrieve/modify from the Key Vault. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp keyvault key get \
  --vault <vault> \
  [--key <key>] \
  [--include-managed <include-managed>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault` | string | Yes | The name of the Key Vault. |
| `--key` | string | No | The name of the key to retrieve/modify from the Key Vault. |
| `--include-managed` | string | No | Whether or not to include managed keys in results. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌

## Secret: Create

Create or update a secret in an Azure Key Vault. Creates a new secret version when a secret with the same name already exists. Requires the vault name, secret name, and subscription. Optionally, specify the tenant.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli keyvault secret create -->

Example prompts include:

- "Create a new secret called 'db-password' with value '\<secure-password\>' in the vault 'prod-kv'."
- "Set secret 'api-key' with value '\<api-key\>' in key vault 'appsettings-kv'."
- "Store secret 'storage-conn' with value 'DefaultEndpointsProtocol=https;AccountName=storage1' in key vault 'storage-kv'."
- "Add a new version of secret 'cert-password' with value '\<secure-password\>' in vault 'certs-kv'."
- "Update secret 'stripe-secret' to value 'rk_test_987xyz' in the key vault 'payments-kv'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Secret name** |  Required | The name of the secret. |
| **Value** |  Required | The value to set for the secret. |
| **Vault name** |  Required | The name of the Key Vault. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp keyvault secret create \
  --vault <vault> \
  --secret <secret> \
  --value <value>
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault` | string | Yes | The name of the Key Vault. |
| `--secret` | string | Yes | The name of the secret. |
| `--value` | string | Yes | The value to set for the secret. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ✅ | Local Required: ❌

## Secret: Get

Lists all secrets in an Azure Key Vault, or retrieves a specific secret by name. Shows all secret names in the vault without values, or returns the secret value and full details, including enabled status and expiration dates.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli keyvault secret get -->

Example prompts include:

- "Show me the secret 'db-conn-string' in the key vault 'prod-kv'."
- "Show me the details of the secret 'api-key' in the key vault 'my-keyvault'."
- "Get the secret 'payment-certificate' from vault 'finance-kv'."
- "Display the secret details for 'ssh-key' in vault 'dev-kv'."
- "Retrieve secret metadata for 'tls-cert' in vault 'web-kv'."
- "List all secrets in the key vault 'prod-kv'."
- "Show me the secrets in the key vault 'my-keyvault'."
- "What secrets are in the key vault 'security-kv'?"
- "List secret names in vault 'ops-kv'."
- "Enumerate secrets in key vault 'archive-kv'."
- "Show secret names in the key vault 'backup-kv'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Vault name** |  Required | The name of the Key Vault. |
| **Secret name** |  Optional | The name of the secret. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp keyvault secret get \
  --vault <vault> \
  [--secret <secret>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--vault` | string | Yes | The name of the Key Vault. |
| `--secret` | string | No | The name of the secret. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ✅ | Local Required: ❌

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure Key Vault documentation](/azure/key-vault/)
