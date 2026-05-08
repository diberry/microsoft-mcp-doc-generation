### [MCP Server](#tab/mcp-server)

This tool executes `keyvault certificate get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

List all certificates in your Key Vault or get a specific certificate by name. Shows all certificate names in the vault, or retrieves full certificate details including key ID, secret ID, thumbprint, and policy information.

### Example CLI commands

Basic usage:

```azurecli
azmcp keyvault certificate get
```

With parameters:

```azurecli
azmcp keyvault certificate get --vault <vault> --certificate <certificate>
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

---
