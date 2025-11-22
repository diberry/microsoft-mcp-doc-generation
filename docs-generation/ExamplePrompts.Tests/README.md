# ExamplePrompts.Tests

Integration tests for the ExamplePrompts library that generates example prompts using Azure OpenAI.

## Prerequisites

Before running these tests, you need to configure Azure OpenAI credentials:

### Option 1: Environment Variables

Set these environment variables:

```bash
export FOUNDRY_API_KEY="your-api-key-here"
export FOUNDRY_ENDPOINT="https://your-resource.openai.azure.com/"
export FOUNDRY_INSTANCE="your-deployment-name"
export FOUNDRY_MODEL_API_VERSION="2024-08-01-preview"
```

### Option 2: .env File

Create a `.env` file in the `ExamplePrompts` directory:

```bash
cd /workspaces/microsoft-mcp-doc-generation/docs-generation/ExamplePrompts
cat > .env << 'EOF'
FOUNDRY_API_KEY=your-api-key-here
FOUNDRY_ENDPOINT=https://your-resource.openai.azure.com/
FOUNDRY_INSTANCE=your-deployment-name
FOUNDRY_MODEL_API_VERSION=2024-08-01-preview
EOF
```

## Running Tests

### Run All Tests

```bash
cd /workspaces/microsoft-mcp-doc-generation/docs-generation
dotnet test ExamplePrompts.Tests
```

### Run with Verbose Output

```bash
dotnet test ExamplePrompts.Tests --verbosity normal
```

### Run Specific Test

```bash
dotnet test ExamplePrompts.Tests --filter "GenerateExamplePrompt_ReturnsValidResponse"
```

## Test Behavior

- **Without Configuration**: Tests are automatically skipped if credentials are not configured
- **With Configuration**: Tests will call the actual Azure OpenAI API and validate responses
- **CI/CD Safe**: Tests won't fail in CI pipelines that don't have credentials configured

## What Gets Tested

The integration tests verify:

1. **Configuration Loading**: Credentials are properly loaded from environment or .env file
2. **API Connection**: Successfully connects to Azure OpenAI endpoint
3. **Prompt Generation**: Generates example prompts based on tool metadata
4. **Response Validation**: Ensures responses are non-empty and contain expected content
5. **Error Handling**: Gracefully handles missing configuration or API errors

## Expected Output

When tests run successfully with valid credentials:

```
Test summary: total: 3, failed: 0, succeeded: 3, skipped: 0
  ✓ ExamplePrompts.Tests.IntegrationTests.GenerateExamplePrompt_ReturnsValidResponse
  ✓ ExamplePrompts.Tests.IntegrationTests.GenerateMultiplePrompts_ReturnsDistinctResults
  ✓ ExamplePrompts.Tests.IntegrationTests.HandleInvalidConfiguration_ThrowsException
```

When credentials are not configured:

```
Test summary: total: 3, failed: 0, succeeded: 0, skipped: 3
  ⊘ ExamplePrompts.Tests.IntegrationTests.GenerateExamplePrompt_ReturnsValidResponse (SKIPPED)
    Reason: Integration test - Azure OpenAI credentials not configured
```

## Troubleshooting

### "Integration test - credentials not configured"

- Verify environment variables are set: `echo $FOUNDRY_API_KEY`
- Check `.env` file exists and has correct values
- Ensure no typos in environment variable names

### "401 Unauthorized"

- Verify API key is correct and not expired
- Check that the endpoint URL matches your Azure resource
- Ensure deployment name exists in your Azure OpenAI resource

### "404 Not Found"

- Verify the deployment name (FOUNDRY_INSTANCE) is correct
- Check that the model is deployed in your Azure OpenAI resource
- Ensure endpoint URL format is correct (should end with `.openai.azure.com/`)

### "Rate limit exceeded"

- Tests make actual API calls, which count against your quota
- Add delays between test runs if needed
- Consider using a test-specific deployment with higher limits

## Notes

- These are **integration tests** that make real API calls
- API calls **consume tokens** and may incur costs
- Test execution time depends on API response latency (~2-5 seconds per test)
- Tests can be run locally or in CI/CD with proper credential management
