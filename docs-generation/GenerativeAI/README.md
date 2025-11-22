# AzureOpenAIClient

Minimal wrapper for Azure OpenAI SDK chat completions using the official Azure.AI.OpenAI SDK v2.0.0.

## Features

- Loads configuration from environment variables or `.env` file
- Uses Azure.AI.OpenAI SDK (v2.0.0) for chat completions
- Simple API: `GetChatCompletionAsync(systemPrompt, userPrompt)`
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
using AzureOpenAIClient;

var client = new AzureOpenAIClient();
var response = await client.GetChatCompletionAsync(
    "You are a helpful assistant.",
    "Say hello in one sentence."
);
Console.WriteLine(response);
```

## Testing

Run the integration test (will skip if not configured):

```bash
dotnet test docs-generation/AzureOpenAIClient.Tests
```

To run with actual Azure OpenAI credentials, remove the `Skip` attribute from the test or set environment variables.

## SDK Details

This wrapper uses:
- `Azure.AI.OpenAI.AzureOpenAIClient` as the top-level client
- `GetChatClient(deploymentName)` to obtain a chat client
- `ChatClient.CompleteChatAsync(messages, options)` for completions
- `SystemChatMessage` and `UserChatMessage` for message construction
