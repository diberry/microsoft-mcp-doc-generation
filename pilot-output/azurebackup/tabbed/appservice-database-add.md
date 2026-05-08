### [MCP Server](#tab/mcp-server)

This tool executes `appservice database add` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Add a database connection for an App Service using connection string for an existing database. This command configures database connection
settings for the specified App Service, allowing it to connect to a database server name. You must specify the App Service name, database name,
database type, database server name, connection string, resource group name and subscription.

### Example CLI commands

Basic usage:

```azurecli
azmcp appservice database add
```

With parameters:

```azurecli
azmcp appservice database add --resource-group <resource-group> --app <app> --database-type <database-type> --database-server <database-server> --database <database> --connection-string <connection-string>
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
| `--app` | string | - | The name of the Azure App Service (e.g., my-webapp). |
| `--database-type` | string | - | The type of database (e.g., SqlServer, MySQL, PostgreSQL, CosmosDB). |
| `--database-server` | string | - | The server name or endpoint for the database (e.g., myserver.database.windows.net). |
| `--database` | string | - | The name of the database to connect to (e.g., mydb). |
| `--connection-string` | string | - | The connection string for the database. If not provided, a default will be generated. |

---
