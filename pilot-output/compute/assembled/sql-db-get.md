Show, get, or list Azure SQL databases in a SQL Server. Shows details for a specific Azure SQL database
by name, or lists all Azure SQL databases in the specified SQL Server. Use to show or retrieve Azure SQL
database information. Equivalent to 'az sql db show' (show one Azure SQL database) or 'az sql db list'
(list all Azure SQL databases in a server). Returns database information including configuration details
and current status.

### Example CLI commands

Basic usage:

```azurecli
azmcp sql db get
```

With parameters:

```azurecli
azmcp sql db get --resource-group <resource-group> --server <server> --database <database>
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
| `--server` | string | The Azure SQL Server name. |
| `--database` | string | The Azure SQL Database name. |

