Update properties and metadata of an Azure managed file share snapshot, such as tags or retention policies.

### Example CLI commands

Basic usage:

```azurecli
azmcp fileshares fileshare snapshot update
```

With parameters:

```azurecli
azmcp fileshares fileshare snapshot update --resource-group <resource-group> --file-share-name <file-share-name> --snapshot-name <snapshot-name> --metadata <metadata>
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
| `--resource-group` | string | - | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--file-share-name` | string | - | The name of the parent file share |
| `--snapshot-name` | string | - | The name of the snapshot |
| `--metadata` | string | - | Custom metadata for the snapshot as a JSON object (e.g., {"key1":"value1","key2":"value2"}) |

