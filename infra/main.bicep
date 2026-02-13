targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment used to generate a unique resource name.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string

@description('Name of the GPT-4o-mini model deployment.')
param gpt4oMiniDeploymentName string = 'gpt-4o-mini'

@description('Name of the GPT-4o model deployment.')
param gpt4oDeploymentName string = 'gpt-4o'

@description('Capacity (in thousands of tokens-per-minute) for GPT-4o-mini deployment.')
param gpt4oMiniCapacity int = 30

@description('Capacity (in thousands of tokens-per-minute) for GPT-4o deployment.')
param gpt4oCapacity int = 30

@description('Azure OpenAI API version.')
param openAiApiVersion string = '2025-01-01-preview'

// ── Resource Group ──────────────────────────────────────────────────────────────

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
}

// ── Azure OpenAI (Cognitive Services) ───────────────────────────────────────────

module openAi 'modules/openai.bicep' = {
  name: 'openai'
  scope: rg
  params: {
    name: 'oai-${environmentName}'
    location: location
    gpt4oMiniDeploymentName: gpt4oMiniDeploymentName
    gpt4oDeploymentName: gpt4oDeploymentName
    gpt4oMiniCapacity: gpt4oMiniCapacity
    gpt4oCapacity: gpt4oCapacity
  }
}

// ── Outputs (map to .env variables) ─────────────────────────────────────────────

output FOUNDRY_API_KEY string = openAi.outputs.apiKey
output FOUNDRY_ENDPOINT string = openAi.outputs.endpoint
output FOUNDRY_INSTANCE string = openAi.outputs.name
output FOUNDRY_MODEL_NAME string = gpt4oMiniDeploymentName
output FOUNDRY_MODEL_API_VERSION string = openAiApiVersion
output TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME string = gpt4oDeploymentName
output TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION string = openAiApiVersion
output ENDPOINT string = openAi.outputs.endpoint
