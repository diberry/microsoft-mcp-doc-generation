### [MCP Server](#tab/mcp-server)

This tool executes `keyvault certificate create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create/issue/generate a new certificate in an Azure Key Vault using the default certificate policy. Required: --vault, --certificate, --subscription. Optional: --tenant <tenant>. Returns: name, id, keyId, secretId, cer (base64), thumbprint, enabled, notBefore, expiresOn, createdOn, updatedOn, subject, issuerName. Creates a new certificate version if it already exists.

### Example CLI commands

Basic usage:

```azurecli
azmcp keyvault certificate create
```

With parameters:

```azurecli
azmcp keyvault certificate create --vault <vault> --certificate <certificate>
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `--tenant` | string | The Microsoft Entra ID tenant ID or name. This can be either the GUID identifier or the display name of your Entra ID tenant. |
| `--auth-method` | string | Authentication method to use. Options: 'credential' (Azure CLI/managed identity), 'key' (access key), or 'connectionString'. |
| `--retry-delay` | string | Initial delay in seconds between retry attempts. For exponential backoff, this value is used as the base. |
| `--retry-max-delay` | string | Maximum delay in seconds between retries, regardless of the retry strategy. |
| `--retry-max-retries` | string | Maximum number of retry attempts for failed operations before giving up. |
| `--retry-mode` | string | Retry strategy to use. 'fixed' uses consistent delays, 'exponential' increases delay between attempts. |
| `--retry-network-timeout` | string | Network operation timeout in seconds. Operations taking longer than this will be cancelled. |
| `--subscription` | string | Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name. If not specified, the AZURE_SUBSCRIPTION_ID environment variable will be used instead. |
| `--vault` | string | The name of the Key Vault. |
| `--certificate` | string | The name of the certificate. |

---
