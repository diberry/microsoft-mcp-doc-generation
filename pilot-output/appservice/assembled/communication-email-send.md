Send emails to one or multiple recipients to the given email-address. The emails can be plain text or HTML formatted. You can include a subject, custom sender name, CC and BCC recipients, and reply-to addresses.

### Example CLI commands

Basic usage:

```azurecli
azmcp communication email send
```

With parameters:

```azurecli
azmcp communication email send --endpoint <endpoint> --from <from> --sender-name <sender-name> --to <to> --cc <cc> --bcc <bcc> --subject <subject> --message <message> --is-html <is-html> --reply-to <reply-to>
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
| `--endpoint` | string | - | The Communication Services URI endpoint (e.g., https://myservice.communication.azure.com). Required for credential authentication. |
| `--from` | string | - | The email address to send from (must be from a verified domain) |
| `--sender-name` | string | - | The display name of the sender |
| `--to` | string | - | The recipient email address(es) to send the email to. |
| `--cc` | string | - | CC recipient email addresses |
| `--bcc` | string | - | BCC recipient email addresses |
| `--subject` | string | - | The email subject |
| `--message` | string | - | The email message content to send to the recipient(s). |
| `--is-html` | string | - | Flag indicating whether the message content is HTML |
| `--reply-to` | string | - | Reply-to email addresses |

