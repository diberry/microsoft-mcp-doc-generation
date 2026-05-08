### [MCP Server](#tab/mcp-server)

This tool executes `mysql database query` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Executes a safe, read-only SQL SELECT query against a database on Azure Database for MySQL Flexible Server. Use this tool to explore or retrieve table data without modifying it. Rejects non-SELECT statements (INSERT/UPDATE/DELETE/REPLACE/MERGE/TRUNCATE/ALTER/CREATE/DROP), multi-statements, comments hiding writes, transaction control (BEGIN/COMMIT/ROLLBACK), INTO OUTFILE, and other destructive keywords. Only a single SELECT is executed to ensure data integrity. Best practices: List needed columns (avoid SELECT *), add WHERE filters, use LIMIT/OFFSET for paging, ORDER BY for deterministic results, and avoid unnecessary sensitive data. Example: SELECT id, name, status FROM customers WHERE status = 'Active' ORDER BY name LIMIT 50;

### Example CLI commands

Basic usage:

```azurecli
azmcp mysql database query
```

With parameters:

```azurecli
azmcp mysql database query --resource-group <resource-group> --user <user> --server <server> --database <database> --query <query>
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
| `--user` | string | - | The user name to access MySQL server. |
| `--server` | string | - | The MySQL server to be accessed. |
| `--database` | string | - | The MySQL database to be accessed. |
| `--query` | string | - | Query to be executed against a MySQL database. |

---
