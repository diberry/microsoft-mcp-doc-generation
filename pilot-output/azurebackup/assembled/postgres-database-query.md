Executes a SQL query on an Azure Database for PostgreSQL server to search for specific terms, retrieve records, or perform SELECT operations.

### Example CLI commands

Basic usage:

```azurecli
azmcp postgres database query
```

With parameters:

```azurecli
azmcp postgres database query --resource-group <resource-group> --user <user> --server <server> --database <database> --auth-type <auth-type> --password <password> --query <query>
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
| `--user` | string | - | The user name to access PostgreSQL server. |
| `--server` | string | - | The PostgreSQL server to be accessed. |
| `--database` | string | - | The PostgreSQL database to be accessed. |
| `--auth-type` | string | - | The authentication type to access PostgreSQL server. Supported values are 'MicrosoftEntra' or 'PostgreSQL'. By default 'MicrosoftEntra' is used. |
| `--password` | string | - | The user password to access PostgreSQL server, Only required if 'auth-type' is set to 'PostgreSQL' authentication, not needed for 'MicrosoftEntra' authentication. |
| `--query` | string | - | Query to be executed against a PostgreSQL database. |

