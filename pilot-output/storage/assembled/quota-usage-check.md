This tool will check the usage and quota information for Azure resources in a region.

### Example CLI commands

Basic usage:

```azurecli
azmcp quota usage check
```

With parameters:

```azurecli
azmcp quota usage check --region <region> --resource-types <resource-types>
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
| `--region` | string | The valid Azure region where the resources will be deployed. E.g. 'eastus', 'westus', etc. |
| `--resource-types` | string | The valid Azure resource types that are going to be deployed(comma-separated). E.g. 'Microsoft.App/containerApps,Microsoft.Web/sites,Microsoft.CognitiveServices/accounts', etc. |

