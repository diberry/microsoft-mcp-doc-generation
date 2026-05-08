### [MCP Server](#tab/mcp-server)

This tool executes `keyvault certificate import` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Imports/uploads an existing certificate (PFX or PEM with private key) into an Azure Key Vault without generating a new certificate or key material. This command accepts either a file path to a PFX/PEM file, a base64 encoded PFX, or raw PEM text starting with -----BEGIN. If the certificate is a password-protected PFX, a password must be provided. Required: --vault <vault>, --certificate <certificate>, --certificate-data <certificate-data>, --subscription <subscription>. Optional: --password <password-for-PFX>, --tenant <tenant>. Returns: name, id, keyId, secretId, cer (base64), thumbprint, enabled, notBefore, expiresOn, createdOn, updatedOn, subject, issuer. Creates a new certificate version if it already exists.

### Example CLI commands

Basic usage:

```azurecli
azmcp keyvault certificate import
```

With parameters:

```azurecli
azmcp keyvault certificate import --vault <vault> --certificate <certificate> --certificate-data <certificate-data> --password <password>
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `--tenant` | string | - | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | - | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | - | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | - | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | - | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | - | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | - | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--subscription` | string | - | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--vault` | string | - | The name of the Key Vault. |
| `--certificate` | string | - | The name of the certificate. |
| `--certificate-data` | string | - | The certificate content: path to a PFX/PEM file, a base64 encoded PFX, or raw PEM text beginning with -----BEGIN. |
| `--password` | string | - | Optional password for a protected PFX being imported. |

---
