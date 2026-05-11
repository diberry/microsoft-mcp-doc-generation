Add a cloud endpoint to a sync group by connecting an Azure File Share. Cloud endpoints represent the Azure storage side of the sync relationship.

### Example CLI commands

Basic usage:

```azurecli
azmcp storagesync cloudendpoint create
```

With parameters:

```azurecli
azmcp storagesync cloudendpoint create --resource-group <resource-group> --name <name> --sync-group-name <sync-group-name> --cloud-endpoint-name <cloud-endpoint-name> --storage-account-resource-id <storage-account-resource-id> --azure-file-share-name <azure-file-share-name>
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
| `--resource-group` | string | The name of the Azure resource group. This is a logical container for Azure resources. |
| `--name` | string | The name of the storage sync service |
| `--sync-group-name` | string | The name of the sync group |
| `--cloud-endpoint-name` | string | The name of the cloud endpoint |
| `--storage-account-resource-id` | string | The resource ID of the Azure storage account |
| `--azure-file-share-name` | string | The name of the Azure file share |

