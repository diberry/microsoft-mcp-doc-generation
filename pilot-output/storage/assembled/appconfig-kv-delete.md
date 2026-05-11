Delete a key-value pair from an App Configuration store. This command removes the specified key-value pair from the store.
If a label is specified, only the labeled version is deleted. If no label is specified, the key-value with the matching
key and the default label will be deleted.

### Example CLI commands

Basic usage:

```azurecli
azmcp appconfig kv delete
```

With parameters:

```azurecli
azmcp appconfig kv delete --account <account> --key <key> --label <label> --content-type <content-type>
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
| `--account` | string | The name of the App Configuration store (e.g., my-appconfig). |
| `--key` | string | The name of the key to access within the App Configuration store. |
| `--label` | string | The label to apply to the configuration key. Labels are used to group and organize settings. |
| `--content-type` | string | The content type of the configuration value. This is used to indicate how the value should be interpreted or parsed. |

