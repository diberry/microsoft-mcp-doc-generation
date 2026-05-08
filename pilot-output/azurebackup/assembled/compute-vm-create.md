Create, deploy, or provision a single Azure Virtual Machine (VM).
Use this to launch a new Linux or Windows VM with SSH key or password authentication.
Automatically creates networking resources (VNet, subnet, NSG, NIC, public IP) when not specified.
Equivalent to 'az vm create'. Defaults to Standard_D2s_v5 size and Ubuntu 24.04 LTS if not specified.
For Linux VMs with SSH, read the user's public key file (e.g., ~/.ssh/id_rsa.pub) and pass its content.
Do not use this for creating Virtual Machine Scale Sets with multiple identical instances (use VMSS create instead).

### Example CLI commands

Basic usage:

```azurecli
azmcp compute vm create
```

With parameters:

```azurecli
azmcp compute vm create --resource-group <resource-group> --vm-name <vm-name> --location <location> --admin-username <admin-username> --admin-password <admin-password> --ssh-public-key <ssh-public-key> --vm-size <vm-size> --image <image> --os-type <os-type> --virtual-network <virtual-network> --subnet <subnet> --public-ip-address <public-ip-address> --network-security-group <network-security-group> --no-public-ip <no-public-ip> --source-address-prefix <source-address-prefix> --zone <zone> --os-disk-size-gb <os-disk-size-gb> --os-disk-type <os-disk-type>
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
| `--vm-name` | string | - | The name of the virtual machine |
| `--location` | string | - | The Azure region/location. Defaults to the resource group's location if not specified. |
| `--admin-username` | string | - | The admin username for the VM. Required for VM creation |
| `--admin-password` | string | - | The admin password for Windows VMs or when SSH key is not provided for Linux VMs |
| `--ssh-public-key` | string | - | SSH public key for Linux VMs. Can be the key content or path to a file |
| `--vm-size` | string | - | The VM size (e.g., Standard_D2s_v3, Standard_B2s). Defaults to Standard_D2s_v5 if not specified |
| `--image` | string | - | The OS image to use. Can be URN (publisher:offer:sku:version) or alias like 'Ubuntu2404', 'Win2022Datacenter'. Defaults to Ubuntu 24.04 LTS |
| `--os-type` | string | - | The Operating System type of the disk. Accepted values: Linux, Windows. |
| `--virtual-network` | string | - | Name of an existing virtual network to use. If not specified, a new one will be created |
| `--subnet` | string | - | Name of the subnet within the virtual network |
| `--public-ip-address` | string | - | Name of the public IP address to use or create |
| `--network-security-group` | string | - | Name of the network security group to use or create |
| `--no-public-ip` | string | - | Do not create or assign a public IP address |
| `--source-address-prefix` | string | - | Source IP address range for NSG inbound rules (e.g., '203.0.113.0/24' or a specific IP). Defaults to '*' (any source) |
| `--zone` | string | - | Availability zone into which to provision the resource. |
| `--os-disk-size-gb` | string | - | OS disk size in GB. Defaults based on image requirements |
| `--os-disk-type` | string | - | OS disk type: 'Premium_LRS', 'StandardSSD_LRS', 'Standard_LRS'. Defaults based on VM size |

