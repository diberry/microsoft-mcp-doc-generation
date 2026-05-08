 Provides the Bicep schema definition of any Azure resource type (latest service version). Use this to get the schema needed to write Bicep IaC (infrasturcture as code) for Azure resources such as AI models, storage accounts, databases, virtual machines, app services, key vaults, and more. Do not use this tool for resource deployment, deployment guidelines, or getting best practices.

### Example CLI commands

Basic usage:

```azurecli
azmcp bicepschema get
```

With parameters:

```azurecli
azmcp bicepschema get --resource-type <resource-type>
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
| `--resource-type` | string | - | The name of the Bicep Resource Type and must be in the full Azure Resource Manager format '{ResourceProvider}/{ResourceType}'. (e.g., 'Microsoft.KeyVault/vaults', 'Microsoft.Storage/storageAccounts', 'Microsoft.Compute/virtualMachines')(e.g., Microsoft.Storage/storageAccounts). |

