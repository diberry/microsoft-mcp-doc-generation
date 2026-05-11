List MySQL servers, databases, or tables in your subscription. Returns all servers by default. Specify --server to list databases on that server, or --server and --database to list tables in a specific database.

### Example CLI commands

Basic usage:

```azurecli
azmcp mysql list
```

With parameters:

```azurecli
azmcp mysql list --resource-group <resource-group> --user <user> --server <server> --database <database>
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
| `--user` | string | The user name to access MySQL server. |
| `--server` | string | The MySQL server to list databases from (optional). |
| `--database` | string | The MySQL database to list tables from (optional, requires --server). |

