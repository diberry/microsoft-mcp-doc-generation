---
ms.topic: include
ms.date: 05/08/2026
mcp-cli.version: 1.0.0-pilot
---
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
| `--disk-name` | string | The name of the disk |
| `--size-gb` | string | Size of the disk in GB. Max size: 4095 GB. |
| `--sku` | string | Underlying storage SKU. Accepted values: Premium_LRS, PremiumV2_LRS, Premium_ZRS, StandardSSD_LRS, StandardSSD_ZRS, Standard_LRS, UltraSSD_LRS. |
| `--disk-iops-read-write` | string | The number of IOPS allowed for this disk. Only settable for UltraSSD disks. |
| `--disk-mbps-read-write` | string | The bandwidth allowed for this disk in MBps. Only settable for UltraSSD disks. |
| `--max-shares` | string | The maximum number of VMs that can attach to the disk at the same time. Value greater than one indicates a shared disk. |
| `--network-access-policy` | string | Policy for accessing the disk via network. Accepted values: AllowAll, AllowPrivate, DenyAll. |
| `--enable-bursting` | string | Enable on-demand bursting beyond the provisioned performance target of the disk. Does not apply to Ultra disks. Accepted values: true, false. |
| `--tags` | string | Space-separated tags in 'key=value' format. Use '' to clear existing tags. |
| `--disk-encryption-set` | string | Resource ID of the disk encryption set to use for enabling encryption at rest. |
| `--encryption-type` | string | Encryption type of the disk. Accepted values: EncryptionAtRestWithCustomerKey, EncryptionAtRestWithPlatformAndCustomerKeys, EncryptionAtRestWithPlatformKey. |
| `--disk-access` | string | Resource ID of the disk access resource for using private endpoints on disks. |
| `--tier` | string | Performance tier of the disk (e.g., P10, P15, P20, P30, P40, P50, P60, P70, P80). Applicable to Premium SSD disks only. |
