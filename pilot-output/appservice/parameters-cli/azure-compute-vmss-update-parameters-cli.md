---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
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
| `--vmss-name` | string | - | The name of the virtual machine scale set |
| `--upgrade-policy` | string | - | Upgrade policy mode: 'Automatic', 'Manual', or 'Rolling'. Default is 'Manual' |
| `--capacity` | string | - | Number of VM instances (capacity) in the scale set |
| `--vm-size` | string | - | The VM size (e.g., Standard_D2s_v3, Standard_B2s). Defaults to Standard_D2s_v5 if not specified |
| `--overprovision` | string | - | Enable or disable overprovisioning. When enabled, Azure provisions more VMs than requested and deletes extra VMs after deployment |
| `--enable-auto-os-upgrade` | string | - | Enable automatic OS image upgrades. Requires health probes or Application Health extension |
| `--scale-in-policy` | string | - | Scale-in policy to determine which VMs to remove: 'Default', 'NewestVM', or 'OldestVM' |
| `--tags` | string | - | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
