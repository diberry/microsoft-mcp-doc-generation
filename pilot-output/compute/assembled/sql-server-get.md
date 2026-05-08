Show, get, or list Azure SQL servers in a resource group. Shows details for a specific Azure SQL server
by name, or lists all Azure SQL servers in the specified resource group. Use to show, display, or
retrieve Azure SQL server information. Equivalent to 'az sql server show' (show one Azure SQL server) or
'az sql server list' (list all Azure SQL servers in a resource group). Returns server information
including configuration details and current state.

### Example CLI commands

Basic usage:

```azurecli
azmcp sql server get
```

With parameters:

```azurecli
azmcp sql server get --resource-group <resource-group> --server <server>
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
| `--server` | string | - | The Azure SQL Server name. |

