Update an existing Azure managed file share resource. Allows updating mutable properties like provisioned storage, IOPS, throughput, and network access settings.

### Example CLI commands

Basic usage:

```azurecli
azmcp fileshares fileshare update
```

With parameters:

```azurecli
azmcp fileshares fileshare update --resource-group <resource-group> --name <name> --provisioned-storage-in-gib <provisioned-storage-in-gib> --provisioned-io-per-sec <provisioned-io-per-sec> --provisioned-throughput-mib-per-sec <provisioned-throughput-mib-per-sec> --public-network-access <public-network-access> --nfs-root-squash <nfs-root-squash> --allowed-subnets <allowed-subnets> --tags <tags>
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
| `--name` | string | The name of the file share |
| `--provisioned-storage-in-gib` | string | The desired provisioned storage size of the share in GiB |
| `--provisioned-io-per-sec` | string | The provisioned IO operations per second |
| `--provisioned-throughput-mib-per-sec` | string | The provisioned throughput in MiB per second |
| `--public-network-access` | string | Public network access setting (Enabled or Disabled) |
| `--nfs-root-squash` | string | NFS root squash setting (NoRootSquash, RootSquash, or AllSquash) |
| `--allowed-subnets` | string | Comma-separated list of subnet IDs allowed to access the file share |
| `--tags` | string | Resource tags as JSON (e.g., {"key1":"value1","key2":"value2"}) |

