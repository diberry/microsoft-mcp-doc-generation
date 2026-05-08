### [MCP Server](#tab/mcp-server)

This tool executes `azurebackup recoverypoint get` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Retrieves recovery point information for a protected item. When --recovery-point is
specified, returns detailed information about a single recovery point including time
and type. When omitted, lists all available recovery points for the protected item.

### Example CLI commands

Basic usage:

```azurecli
azmcp azurebackup recoverypoint get
```

With parameters:

```azurecli
azmcp azurebackup recoverypoint get --resource-group <resource-group> --vault <vault> --vault-type <vault-type> --protected-item <protected-item> --container <container> --recovery-point <recovery-point>
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
| `--vault` | string | - | The name of the backup vault (Recovery Services vault or Backup vault). |
| `--vault-type` | string | - | The type of backup vault: 'rsv' (Recovery Services vault) or 'dpp' (Backup vault / Data Protection). Required for vault create; optional elsewhere (auto-detected if omitted). |
| `--protected-item` | string | - | The name of the protected item or backup instance. |
| `--container` | string | - | The RSV protection container name. Only applicable for Recovery Services vaults. |
| `--recovery-point` | string | - | The recovery point ID. |

---
