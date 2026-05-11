### [MCP Server](#tab/mcp-server)

This tool executes `managedlustre fs create` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Create an Azure Managed Lustre (AMLFS) file system using the specified network, capacity, maintenance window and availability zone.
Optionally provides possibility to define Blob Integration, customer managed key encryption and root squash configuration.

### Example CLI commands

Basic usage:

```azurecli
azmcp managedlustre fs create
```

With parameters:

```azurecli
azmcp managedlustre fs create --resource-group <resource-group> --name <name> --location <location> --sku <sku> --size <size> --subnet-id <subnet-id> --zone <zone> --maintenance-day <maintenance-day> --maintenance-time <maintenance-time> --hsm-container <hsm-container> --hsm-log-container <hsm-log-container> --import-prefix <import-prefix> --root-squash-mode <root-squash-mode> --no-squash-nid-list <no-squash-nid-list> --squash-uid <squash-uid> --squash-gid <squash-gid> --custom-encryption <custom-encryption> --key-url <key-url> --source-vault <source-vault> --user-assigned-identity-id <user-assigned-identity-id>
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
| `--name` | string | The AMLFS resource name. Must be DNS-friendly (letters, numbers, hyphens). Example: --name amlfs-001 |
| `--location` | string | Azure region/region short name (use Azure location token, lowercase). Examples: uaenorth, swedencentral, eastus. |
| `--sku` | string | The AMLFS SKU. Exact allowed values: AMLFS-Durable-Premium-40, AMLFS-Durable-Premium-125, AMLFS-Durable-Premium-250, AMLFS-Durable-Premium-500. |
| `--size` | string | The AMLFS size in TiB as an integer (no unit). Examples: 4, 12, 128. |
| `--subnet-id` | string | Full subnet resource ID. Required format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{subnet}.
Example: --subnet-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.Network/virtualNetworks/vnet-001/subnets/subnet-001 |
| `--zone` | string | Availability zone identifier. Use a single digit string matching the region's AZ labels (e.g. '1').
Example: --zone 1 |
| `--maintenance-day` | string | Preferred maintenance day. Allowed values: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday.
 |
| `--maintenance-time` | string | Preferred maintenance time in UTC. Format: HH:MM (24-hour). Examples: 00:00, 23:00.
 |
| `--hsm-container` | string | Full blob container resource ID for HSM integration. HPC Cache Resource Provider must have before deployment Storage Blob Data Contributor and Storage Account Contributor roles on parent Storage Account.Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{account}/blobServices/default/containers/{container}.
Example: --hsm-container /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stacc/blobServices/default/containers/hsm-container
 |
| `--hsm-log-container` | string | Full blob container resource ID for HSM logging. HPC Cache Resource Provider must have before deployment Storage Blob Data Contributor and Storage Account Contributor roles on parent Storage Account. Same format as --hsm-container.
Example: --hsm-log-container /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/stacc/blobServices/default/containers/hsm-logs
 |
| `--import-prefix` | string | Optional HSM import prefix (path prefix inside the container starting with /). Examples: '/ingest/', '/archive/2019/'.
 |
| `--root-squash-mode` | string | Root squash mode. Allowed values: All, RootOnly, None.
 |
| `--no-squash-nid-list` | string | Comma-separated list of NIDs (network identifiers) not to squash. Example: '10.0.2.4@tcp;10.0.2.[6-8]@tcp'.
 |
| `--squash-uid` | string | Numeric UID to squash root to. Required in case root squash mode is not None. Example: --squash-uid 1000.
 |
| `--squash-gid` | string | Numeric GID to squash root to.  Required in case root squash mode is not None. Example: --squash-gid 1000.
 |
| `--custom-encryption` | string | Enable customer-managed encryption using a Key Vault key. When true, --key-url and --source-vault required, with a user-assigned identity already configured for Key Vault key access. |
| `--key-url` | string | Full Key Vault key URL. Format: https://{vaultName}.vault.azure.net/keys/{keyName}/{keyVersion}.
Example: --key-url https://kv-amlfs-001.vault.azure.net/keys/key-amlfs-001/a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p
 |
| `--source-vault` | string | Full Key Vault resource ID. Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.KeyVault/vaults/{vaultName}.
Example: --source-vault /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv-amlfs-001
 |
| `--user-assigned-identity-id` | string | User-assigned managed identity resource ID (full resource ID) to use for Key Vault access when custom encryption is enabled. The identity must have RBAC role to access the encryption key
Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{name}.
Example: --user-assigned-identity-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/identity1
 |

---
