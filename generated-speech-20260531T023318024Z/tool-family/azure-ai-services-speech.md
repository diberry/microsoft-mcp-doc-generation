---

title: Azure MCP Server tools for Azure Speech in Foundry
description: Use Azure MCP Server tools to manage Azure AI Speech resources for speech-to-text and text-to-speech with natural language prompts from your IDE.
ms.date: 05/31/2026
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 2
mcp-cli.version: 3.0.0-beta.13+cd8d1e8f9924440b33e3e908c390c1599700ccba
author: diberry
ms.author: diberry
ms.reviewer: mbaldwin
ai-usage: ai-generated
ms.custom: build-2025
content_well_notification:
  - AI-contribution
---

# Azure MCP Server tools for Azure Speech in Foundry

The Azure MCP Server lets you manage Azure Speech in Foundry resources, including: recognize and synthesize, with natural language prompts.

Azure Speech in Foundry is an Azure service that provides cloud-based capabilities for your applications. For more information, see [Azure Speech in Foundry documentation](/azure/speech/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]


## Stt: Recognize

Recognizes speech in an audio file and converts it to text using the Azure Speech service. Requires an Azure AI Services endpoint, for example `https://your-service.cognitiveservices.azure.com/`, and the path to the audio file. Supports common audio formats: WAV, MP3, OPUS/OGG, FLAC, ALAW, MULAW, MP4, M4A, and AAC. Compressed formats require GStreamer to be installed on the system. Specify the language, provide phrase hints to improve accuracy, choose an output format (`simple` or `detailed`), and enable profanity filtering.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli speech stt recognize -->

Example prompts include:

- "Convert this file to text using Azure Speech Services with endpoint 'https://speech-prod.cognitiveservices.azure.com/' and file 'recordings/meeting.wav'."
- "Recognize speech from file 'podcast_episode.mp3' using endpoint 'https://speech-eu.cognitiveservices.azure.com/' and language 'en-US'."
- "Transcribe speech from file 'interview.mp3' using endpoint 'https://speech-us.cognitiveservices.azure.com/' with profanity 'removed'."
- "Convert speech to text from file 'lecture.m4a' using endpoint 'https://speech-asia.cognitiveservices.azure.com/' and file 'lecture.m4a'."
- "Transcribe the audio file using endpoint 'https://speech-es.cognitiveservices.azure.com/' and file 'spanish_interview.wav' with language 'es-ES'."
- "Convert speech to text from file 'meeting_recording.opus' using endpoint 'https://speech-prod.cognitiveservices.azure.com/' with format 'detailed'."
- "Recognize speech from file 'call_center.ogg' using endpoint 'https://speech-support.cognitiveservices.azure.com/' with phrases 'account number, support ticket'."
- "Transcribe audio from file 'tech_podcast.mp3' using endpoint 'https://speech-dev.cognitiveservices.azure.com/' with phrases 'Azure, cognitive services, machine learning'."
- "Convert speech to text from file 'demo.aac' using endpoint 'https://speech-samples.cognitiveservices.azure.com/' with phrases 'Azure, cognitive services, API' and format 'simple'."
- "Transcribe audio from file 'raw_audio.flac' using endpoint 'https://speech-raw.cognitiveservices.azure.com/' with profanity 'raw'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Endpoint** |  Required | The Azure AI Services endpoint URL (for example, `https://your-service.cognitiveservices.azure.com/`). |
| **File** |  Required | Path to the audio file to recognize. |
| **Format** |  Optional | Output format: simple or detailed. |
| **Language** |  Optional | The language for speech recognition (for example, `en-US`, `es-ES`). Default is en-US. |
| **Phrases** |  Optional | Phrase hints to improve recognition accuracy. Can be specified multiple times (`--phrases` &quot;phrase1&quot; `--phrases` &quot;phrase2&quot;) or as comma-separated values (`--phrases` &quot;phrase1,phrase2&quot;). |
| **Profanity** |  Optional | Profanity filter: masked, removed, or raw. Default is masked. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp speech stt recognize \
  --endpoint <endpoint> \
  --file <file> \
  [--language <language>] \
  [--phrases <phrases>] \
  [--format <format>] \
  [--profanity <profanity>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--endpoint` | string | Yes | The Azure AI Services endpoint URL (e.g., https://your-service.cognitiveservices.azure.com/). |
| `--file` | string | Yes | Path to the audio file to recognize. |
| `--language` | string | No | The language for speech recognition (e.g., en-US, es-ES). Default is en-US. |
| `--phrases` | string | No | Phrase hints to improve recognition accuracy. Can be specified multiple times (--phrases "phrase1" --phrases "phrase2") or as comma-separated values (--phrases "phrase1,phrase2"). |
| `--format` | string | No | Output format: simple or detailed. |
| `--profanity` | string | No | Profanity filter: masked, removed, or raw. Default is masked. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ✅

## Tts: Synthesize

Converts text to speech using Azure AI Services Speech. Provide `Endpoint`, `Text`, and `OutputAudio`. Optional settings include language (default `en-US`), voice selection, audio format (default `Riff24Khz16BitMonoPcm`), and a custom voice endpoint ID. The command generates audio files in multiple formats and supports neural voices for natural-sounding speech.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli speech tts synthesize -->

Example prompts include:

- "Convert text to speech using endpoint 'https://my-speech-service.cognitiveservices.azure.com/' with text 'Hello, welcome to Azure' and output audio 'output.wav'."
- "Synthesize speech with endpoint 'https://eastus-speech.cognitiveservices.azure.com/' from text 'Hello, welcome to Azure' and output audio 'welcome.wav'."
- "Generate speech audio using endpoint 'https://speech-prod.cognitiveservices.azure.com/' with text 'Hello world' and output audio 'hello-world.wav'."
- "Convert text to speech using endpoint 'https://my-speech-service.cognitiveservices.azure.com/' with language 'es-ES', text 'Bienvenido a Azure', and output audio 'spanish-audio.wav'."
- "Synthesize speech using endpoint 'https://my-speech-service.cognitiveservices.azure.com/' with voice 'en-US-JennyNeural', text 'Azure AI Services', and output audio 'jenny.wav'."
- "Create audio using endpoint 'https://my-speech-service.cognitiveservices.azure.com/' with format 'detailed', text 'Welcome to Azure', and output audio 'welcome.mp3'."
- "Generate speech with custom voice model using endpoint 'https://custom-speech.cognitiveservices.azure.com/' and endpoint ID 'custom-endpoint-123' with text 'Hello from custom voice' and output audio 'custom-voice.wav'."
- "Convert text to OGG/Opus using endpoint 'https://my-speech-service.cognitiveservices.azure.com/' with text 'Audio in OGG format' and output audio 'audio.ogg'."
- "Synthesize long text with endpoint 'https://speech-stream.cognitiveservices.azure.com/' with text 'This is a long narration intended for streaming playback and chaptered audio' and output audio 'long-narration.wav'."
- "Create audio from text using endpoint 'https://fr-speech.cognitiveservices.azure.com/' with language 'fr-FR', voice 'fr-FR-HenriNeural', text 'Bienvenue sur Azure', and output audio 'french-voice.wav'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Endpoint** |  Required | The Azure AI Services endpoint URL (for example, `https://your-service.cognitiveservices.azure.com/`). |
| **OutputAudio** |  Required | Path where the synthesized audio file is saved. |
| **Text** |  Required | The text to convert to speech. |
| **EndpointId** |  Optional | The endpoint ID of a custom voice model for speech synthesis. |
| **Format** |  Optional | Output format: simple or detailed. |
| **Language** |  Optional | The language for speech recognition (for example, `en-US`, `es-ES`). Default is en-US. |
| **Voice** |  Optional | The voice to use for speech synthesis (for example, `en-US-JennyNeural`). If not specified, the default voice for the language is used. |

#### [Azure MCP CLI](#tab/azure-mcp-cli)

**Example CLI command**

```console
azmcp speech tts synthesize \
  --endpoint <endpoint> \
  --text <text> \
  --outputAudio <outputAudio> \
  [--language <language>] \
  [--voice <voice>] \
  [--format <format>] \
  [--endpointId <endpointId>]
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `--endpoint` | string | Yes | The Azure AI Services endpoint URL (e.g., https://your-service.cognitiveservices.azure.com/). |
| `--text` | string | Yes | The text to convert to speech. |
| `--outputAudio` | string | Yes | Path where the synthesized audio file will be saved. |
| `--language` | string | No | The language for speech recognition (e.g., en-US, es-ES). Default is en-US. |
| `--voice` | string | No | The voice to use for speech synthesis (e.g., en-US-JennyNeural). If not specified, the default voice for the language will be used. |
| `--format` | string | No | Output format: simple or detailed. |
| `--endpointId` | string | No | The endpoint ID of a custom voice model for speech synthesis. |

---

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ✅

## Related content

- [What are the Azure MCP Server tools?](index.md)
- [Get started using Azure MCP Server](../get-started.md)
- [Azure AI Speech documentation](/azure/ai-services/speech-service/)
