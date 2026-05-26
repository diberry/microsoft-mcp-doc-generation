# AzureOpenAIClient

Minimal wrapper for Azure OpenAI SDK chat completions using the official Azure.AI.OpenAI SDK v2.0.0.

## Features

- Loads configuration from environment variables or `.env` file
- Uses Azure.AI.OpenAI SDK (v2.0.0) for chat completions
- Simple API: `GetChatCompletionAsync(systemPrompt, userPrompt)`
- Optional pipeline tracing via `IPipelineTracer` for recording prompts, responses, model name, tokens, and latency
- Integration test included (skipped by default unless configured)

## Configuration

Set these environment variables or create a `.env` file:

```bash
FOUNDRY_API_KEY=your-api-key-here
FOUNDRY_ENDPOINT=https://your-resource.openai.azure.com/
FOUNDRY_INSTANCE=your-deployment-name
FOUNDRY_MODEL_API_VERSION=2024-08-01-preview
```

## Usage

```csharp
using DocGeneration.Core.Tracing;
using GenerativeAI;

var tracer = new PipelineTracer("mcp-pipeline");
var client = new GenerativeAIClient(tracer: tracer);
var response = await client.GetChatCompletionAsync(
    "You are a helpful assistant.",
    "Say hello in one sentence.",
    toolOrNamespace: "storage",
    operation: "GenerateExamplePrompts"
);
Console.WriteLine(response);
```

## Testing

Run the integration test (will skip if not configured):

```bash
dotnet test shared/DocGeneration.Core.GenerativeAI.Tests/DocGeneration.Core.GenerativeAI.Tests.csproj
```

To run with actual Azure OpenAI credentials, remove the `Skip` attribute from the test or set environment variables.

## Known Limitations

- **RetryCount always 0:** The retry middleware in `GenerativeAIClient` uses a delegating handler pattern (`ChatClientBuilder.Use(...)`) that encapsulates retries transparently. The retry count is not propagated back to the caller, so `AiInteractionRecord.RetryCount` is always recorded as 0 for MCP pipeline calls. The Skills pipeline (`AzureOpenAiRewriter`) correctly captures retry count because it implements retries inline.

## SDK Details

This wrapper uses:
- `Azure.AI.OpenAI.AzureOpenAIClient` as the top-level client
- `GetChatClient(deploymentName)` to obtain a chat client
- `ChatClient.CompleteChatAsync(messages, options)` for completions
- `SystemChatMessage` and `UserChatMessage` for message construction
