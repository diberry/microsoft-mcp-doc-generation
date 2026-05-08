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
| `--name` | string | - | The AMLFS resource name. Must be DNS-friendly (letters, numbers, hyphens). Example: --name amlfs-001 |
| `--maintenance-day` | string | - | Preferred maintenance day. Allowed values: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday.
 |
| `--maintenance-time` | string | - | Preferred maintenance time in UTC. Format: HH:MM (24-hour). Examples: 00:00, 23:00.
 |
| `--no-squash-nid-list` | string | - | Comma-separated list of NIDs (network identifiers) not to squash. Example: '10.0.2.4@tcp;10.0.2.[6-8]@tcp'.
 |
| `--squash-uid` | string | - | Numeric UID to squash root to. Required in case root squash mode is not None. Example: --squash-uid 1000.
 |
| `--squash-gid` | string | - | Numeric GID to squash root to.  Required in case root squash mode is not None. Example: --squash-gid 1000.
 |
| `--root-squash-mode` | string | - | Root squash mode. Allowed values: All, RootOnly, None.
 |
