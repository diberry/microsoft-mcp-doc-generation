# ExamplePrompts

Lightweight library to generate example prompts using a Foundry-style model endpoint.

Configuration (environment variables or `.env`):

- `FOUNDRY_API_KEY`
- `FOUNDRY_INSTANCE`
- `FOUNDRY_ENDPOINT`
- `FOUNDRY_MODEL`
- `FOUNDRY_MODEL_API_VERSION`

Usage:

```csharp
var opts = ExamplePromptsOptions.LoadFromEnvironmentOrDotEnv();
var client = new ExamplePromptsClient(opts);
var resp = await client.GenerateAsync("system.txt", "user.txt");
Console.WriteLine(resp);
```
