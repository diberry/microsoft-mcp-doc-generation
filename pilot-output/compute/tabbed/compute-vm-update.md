### [MCP Server](#tab/mcp-server)

This tool executes `compute vm update` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Update, modify, or reconfigure an existing Azure Virtual Machine (VM).
Use this to resize a VM, update tags, configure boot diagnostics, or change user data.
Equivalent to 'az vm update'. The VM may need to be deallocated before resizing to certain sizes.
Do not use this to create a new VM (use VM create) or to update Virtual Machine Scale Sets (use VMSS update).

### Example CLI commands

Basic usage:

```azurecli
azmcp compute vm update
```

With parameters:

```azurecli
azmcp compute vm update --resource-group <resource-group> --vm-name <vm-name> --vm-size <vm-size> --tags <tags> --license-type <license-type> --boot-diagnostics <boot-diagnostics> --user-data <user-data>
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
| `--vm-name` | string | The name of the virtual machine |
| `--vm-size` | string | The VM size (e.g., Standard_D2s_v3, Standard_B2s). Defaults to Standard_D2s_v5 if not specified |
| `--tags` | string | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| `--license-type` | string | License type for Azure Hybrid Benefit: 'Windows_Server', 'Windows_Client', 'RHEL_BYOS', 'SLES_BYOS', or 'None' to disable |
| `--boot-diagnostics` | string | Enable or disable boot diagnostics: 'true' or 'false' |
| `--user-data` | string | Base64-encoded user data for the VM. Use to update custom data scripts |

---
