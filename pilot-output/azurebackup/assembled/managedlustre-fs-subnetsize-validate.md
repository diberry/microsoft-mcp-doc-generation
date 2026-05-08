Validates that the provided subnet can host an Azure Managed Lustre filesystem for the given SKU and size.

### Example CLI commands

Basic usage:

```azurecli
azmcp managedlustre fs subnetsize validate
```

With parameters:

```azurecli
azmcp managedlustre fs subnetsize validate --sku <sku> --size <size> --subnet-id <subnet-id> --location <location>
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
| `--sku` | string | The AMLFS SKU. Exact allowed values: AMLFS-Durable-Premium-40, AMLFS-Durable-Premium-125, AMLFS-Durable-Premium-250, AMLFS-Durable-Premium-500. |
| `--size` | string | The AMLFS size in TiB as an integer (no unit). Examples: 4, 12, 128. |
| `--subnet-id` | string | Full subnet resource ID. Required format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{subnet}.
Example: --subnet-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.Network/virtualNetworks/vnet-001/subnets/subnet-001 |
| `--location` | string | Azure region/region short name (use Azure location token, lowercase). Examples: uaenorth, swedencentral, eastus. |

