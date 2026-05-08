List all keys in your Key Vault or get a specific key by name. Shows all key names in the vault, or retrieves full key details including type, enabled status, and expiration dates. Use --include-managed to show managed keys.

### Example CLI commands

Basic usage:

```azurecli
azmcp keyvault key get
```

With parameters:

```azurecli
azmcp keyvault key get --vault <vault> --key <key> --include-managed <include-managed>
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
| `--key` | string | The name of the key to retrieve/modify from the Key Vault. |
| `--include-managed` | string | Whether or not to include managed keys in results. |

