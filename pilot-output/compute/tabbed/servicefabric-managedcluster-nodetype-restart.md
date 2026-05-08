### [MCP Server](#tab/mcp-server)

This tool executes `servicefabric managedcluster nodetype restart` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Restart nodes of a specific node type in a Service Fabric managed cluster. Requires the cluster name, node type, and list of node names to restart. Optionally specify the update type (Default or ByUpgradeDomain).

### Example CLI commands

Basic usage:

```azurecli
azmcp servicefabric managedcluster nodetype restart
```

With parameters:

```azurecli
azmcp servicefabric managedcluster nodetype restart --resource-group <resource-group> --cluster <cluster> --node-type <node-type> --nodes <nodes> --update-type <update-type>
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
| `--cluster` | string | Service Fabric managed cluster name. |
| `--node-type` | string | The node type name within the managed cluster. |
| `--nodes` | string | The list of node names to restart. Multiple node names can be provided. |
| `--update-type` | string | The update type for the restart operation. Valid values: Default, ByUpgradeDomain. |

---
