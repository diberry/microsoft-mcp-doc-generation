### [MCP Server](#tab/mcp-server)

This tool executes `sql server firewall-rule create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Creates a firewall rule for a SQL server. Firewall rules control which IP addresses
are allowed to connect to the SQL server. You can specify either a single IP address
(by setting start and end IP to the same value) or a range of IP addresses. Returns
the created firewall rule with its properties.

### Example CLI commands

Basic usage:

```azurecli
azmcp sql server firewall-rule create
```

With parameters:

```azurecli
azmcp sql server firewall-rule create --resource-group <resource-group> --server <server> --firewall-rule-name <firewall-rule-name> --start-ip-address <start-ip-address> --end-ip-address <end-ip-address>
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
| `--firewall-rule-name` | string | - | The name of the firewall rule. |
| `--start-ip-address` | string | - | The start IP address of the firewall rule range. |
| `--end-ip-address` | string | - | The end IP address of the firewall rule range. |

---
