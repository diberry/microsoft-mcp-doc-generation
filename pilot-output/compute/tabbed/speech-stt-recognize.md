### [MCP Server](#tab/mcp-server)

This tool executes `speech stt recognize` via MCP Server.

See parameters below.

### [CLI](#tab/cli)

Recognize speech from an audio file using Azure AI Services Speech. This command takes an audio file and converts it to text using advanced speech recognition capabilities.
You must provide an Azure AI Services endpoint (e.g., https://your-service.cognitiveservices.azure.com/) and a path to the audio file.
Supported audio formats include WAV, MP3, OPUS/OGG, FLAC, ALAW, MULAW, MP4, M4A, and AAC. Compressed formats require GStreamer to be installed on the system.
Optional parameters include language specification, phrase hints for better accuracy, output format (simple or detailed), and profanity filtering.

### Example CLI commands

Basic usage:

```azurecli
azmcp speech stt recognize
```

With parameters:

```azurecli
azmcp speech stt recognize --endpoint <endpoint> --file <file> --language <language> --phrases <phrases> --format <format> --profanity <profanity>
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
| `--endpoint` | string | - | The Azure AI Services endpoint URL (e.g., https://your-service.cognitiveservices.azure.com/). |
| `--file` | string | - | Path to the audio file to recognize. |
| `--language` | string | - | The language for speech recognition (e.g., en-US, es-ES). Default is en-US. |
| `--phrases` | string | - | Phrase hints to improve recognition accuracy. Can be specified multiple times (--phrases "phrase1" --phrases "phrase2") or as comma-separated values (--phrases "phrase1,phrase2"). |
| `--format` | string | - | Output format: simple or detailed. |
| `--profanity` | string | - | Profanity filter: masked, removed, or raw. Default is masked. |

---
