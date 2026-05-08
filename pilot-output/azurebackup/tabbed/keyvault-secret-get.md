### [MCP Server](#tab/mcp-server)

This tool executes `keyvault secret get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

List all secrets in your Key Vault or get a specific secret by name. Shows all secret names in the vault (without values), or retrieves the secret value and full details including enabled status and expiration dates.

### Example CLI commands

Basic usage:

```azurecli
azmcp keyvault secret get
```

With parameters:

```azurecli
azmcp keyvault secret get --vault <vault> --secret <secret>
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
| `--secret` | string | The name of the secret. |

---
