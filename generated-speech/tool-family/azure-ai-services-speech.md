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

Recognizes speech from an audio file with the Azure Speech service. Provide an Azure Speech endpoint and a path to the audio file. Supported audio formats include `WAV`, `MP3`, `OPUS/OGG`, `FLAC`, `ALAW`, `MULAW`, `MP4`, `M4A`, and `AAC`. Compressed formats require GStreamer on the system. Optional parameters specify language, phrase hints to improve accuracy, output format (`simple` or `detailed`), and profanity filtering. For example, provide endpoint `https://your-service.cognitiveservices.azure.com/` and file path `C:\recordings\meeting.wav`.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli speech stt recognize -->

Example prompts include:

- "Convert this audio file to text using Azure Speech Services with endpoint 'https://speechdemo.cognitiveservices.azure.com/' and file 'recordings/interview1.mp3'."
- "Recognize speech from my audio file with language detection using endpoint 'https://speechdemo.cognitiveservices.azure.com/' and file 'audio/unknown-language.wav'."
- "Transcribe speech from file 'meetings/meeting2.wav' with profanity filtering 'masked' using endpoint 'https://speechdemo.cognitiveservices.azure.com/'."
- "Convert speech to text from file 'podcasts/episode5.mp3' using endpoint 'https://speechdemo.cognitiveservices.azure.com/' and language 'es-ES'."
- "Transcribe the audio file 'voice-notes/note1.m4a' in Spanish language with endpoint 'https://speechspanish.cognitiveservices.azure.com/'."
- "Convert speech to text with detailed output format 'detailed' from file 'call-recordings/call123.flac' using endpoint 'https://speechdemo.cognitiveservices.azure.com/'."
- "Recognize speech from file 'interviews/jane_doe.opus' with phrase hints 'product launch,release notes' using endpoint 'https://speechdemo.cognitiveservices.azure.com/'."
- "Transcribe audio using multiple phrase hints 'Azure,cognitive services,machine learning' from file 'training/session1.mp3' with endpoint 'https://speechdemo.cognitiveservices.azure.com/'."
- "Convert speech to text with comma-separated phrase hints 'Azure,cognitive services,API' from file 'lectures/lecture3.m4a' using endpoint 'https://speechdemo.cognitiveservices.azure.com/'."
- "Transcribe audio with raw profanity output 'raw' from file 'surveys/response1.aac' using endpoint 'https://speechdemo.cognitiveservices.azure.com/'."

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

Converts input text to natural-sounding speech with the Speech service in Azure AI Services, and saves the result to an audio file. Specify an Azure AI Services endpoint (for example, `https://your-service.cognitiveservices.azure.com/`), the text to convert, and the output file path. Specify the language, voice, audio format, or a custom voice endpoint ID to customize the output. Defaults are `en-US` for language and `Riff24Khz16BitMonoPcm` for audio format. The command supports neural voices and a variety of audio formats for realistic speech synthesis.

#### [MCP Server](#tab/mcp-server)

<!-- @mcpcli speech tts synthesize -->

Example prompts include:

- "Convert text to speech using endpoint 'https://contoso-speech.cognitiveservices.azure.com/' with text 'Hello, welcome to Azure' and output audio 'welcome.wav'."
- "Synthesize speech from text 'Hello world' using endpoint 'https://contoso-speech.cognitiveservices.azure.com/' and save output audio 'hello-world.wav' with voice 'en-US-JennyNeural'."
- "Convert text 'Bienvenido a Azure' to speech with endpoint 'https://contoso-speech.cognitiveservices.azure.com/', language 'es-ES', and output audio 'spanish-audio.wav'."
- "Create MP3 audio from text 'Welcome to Azure' using endpoint 'https://contoso-speech.cognitiveservices.azure.com/' and output audio 'welcome.mp3' with Format 'detailed'."
- "Generate speech from text 'Azure AI Services' using endpoint 'https://contoso-speech.cognitiveservices.azure.com/' and output audio 'azure-ai.wav' with endpoint ID 'custom-endpoint-123'."
- "Convert text 'This is a test' to OGG/Opus using endpoint 'https://contoso-speech.cognitiveservices.azure.com/' and save output audio 'test.opus'."
- "Synthesize long text 'Please listen to the full briefing' using endpoint 'https://contoso-speech.cognitiveservices.azure.com/' and stream to output audio 'briefing.wav'."
- "Create high-quality WAV from text 'Welcome to the product demo' using endpoint 'https://speech-prod.cognitiveservices.azure.com/' and output audio 'demo.wav' with voice 'en-US-GuyNeural'."
- "Generate French speech from text 'Bonjour et bienvenue' using endpoint 'https://contoso-speech.cognitiveservices.azure.com/', language 'fr-FR', and output audio 'french-welcome.wav'."
- "Synthesize announcement text 'Attention all staff, the meeting starts at 10' using endpoint 'https://speech-announcements.cognitiveservices.azure.com/' and output audio 'announcement.wav'."

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Endpoint** |  Required | The Azure AI Services endpoint URL (for example, `https://your-service.cognitiveservices.azure.com/`). |
| **OutputAudio** |  Required | Path where the synthesized audio file is saved. |
| **Text** |  Required | The text to convert to speech. |
| **EndpointId** |  Optional | The endpoint ID of a custom voice model for speech synthesis. |
| **Format** |  Optional | Output format: simple or detailed. |
| **Language** |  Optional | The language for speech recognition (for example, `en-US`, `es-ES`). Default is en-US. |
| **Voice** |  Optional | The voice to use for speech synthesis (for example, `en-US-JennyNeural`). If not specified, the default voice for the language is used. |



Examples

- 'Convert the product overview "Welcome to Contoso, this is the product tour." and save to "overview.wav"'
- 'Synthesize the customer greeting "Hello, and thank you for calling Contoso Support." to "greeting.mp3" using voice "en-US-JennyNeural" and language "en-US"'

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
