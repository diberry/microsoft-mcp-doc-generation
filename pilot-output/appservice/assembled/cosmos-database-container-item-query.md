List items from a Cosmos DB container by specifying the account name, database name, and container name, optionally providing a custom SQL query to filter results.

### Example CLI commands

Basic usage:

```azurecli
azmcp cosmos database container item query
```

With parameters:

```azurecli
azmcp cosmos database container item query --account <account> --database <database> --container <container> --query <query>
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
| `--account` | string | - | The name of the Cosmos DB account to query (e.g., my-cosmos-account). |
| `--database` | string | - | The name of the database to query (e.g., my-database). |
| `--container` | string | - | The name of the container to query (e.g., my-container). |
| `--query` | string | - | SQL query to execute against the container. Uses Cosmos DB SQL syntax. |

