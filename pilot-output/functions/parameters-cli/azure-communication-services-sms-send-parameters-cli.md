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
| `--endpoint` | string | - | The Communication Services URI endpoint (e.g., https://myservice.communication.azure.com). Required for credential authentication. |
| `--from` | string | - | The SMS-enabled phone number associated with your Communication Services resource (in E.164 format, e.g., +14255550123). Can also be a short code or alphanumeric sender ID. |
| `--to` | string | - | The recipient phone number(s) in E.164 international standard format (e.g., +14255550123). Multiple numbers can be provided. |
| `--message` | string | - | The SMS message content to send to the recipient(s). |
| `--enable-delivery-report` | string | - | Whether to enable delivery reporting for the SMS message. When enabled, events are emitted when delivery is successful. |
| `--tag` | string | - | Optional custom tag to apply to the SMS message for tracking purposes. |
