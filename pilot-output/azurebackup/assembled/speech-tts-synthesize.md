Convert text to speech using Azure AI Services Speech. This command takes text input and generates an audio file using advanced neural text-to-speech capabilities.
You must provide an Azure AI Services endpoint (e.g., https://your-service.cognitiveservices.azure.com/), the text to convert, and an output file path.
Optional parameters include language specification (default: en-US), voice selection, audio output format (default: Riff24Khz16BitMonoPcm), and custom voice endpoint ID.
The command supports a wide variety of output formats and neural voices for natural-sounding speech synthesis.

### Example CLI commands

Basic usage:

```azurecli
azmcp speech tts synthesize
```

With parameters:

```azurecli
azmcp speech tts synthesize --endpoint <endpoint> --text <text> --outputAudio <outputAudio> --language <language> --voice <voice> --format <format> --endpointId <endpointId>
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
| `--endpoint` | string | The Azure AI Services endpoint URL (e.g., https://your-service.cognitiveservices.azure.com/). |
| `--text` | string | The text to convert to speech. |
| `--outputAudio` | string | Path where the synthesized audio file will be saved. |
| `--language` | string | The language for speech recognition (e.g., en-US, es-ES). Default is en-US. |
| `--voice` | string | The voice to use for speech synthesis (e.g., en-US-JennyNeural). If not specified, the default voice for the language will be used. |
| `--format` | string | Output format: simple or detailed. |
| `--endpointId` | string | The endpoint ID of a custom voice model for speech synthesis. |

