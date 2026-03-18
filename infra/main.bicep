targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment used to generate a unique resource name.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string

@description('Name of the GPT-5-mini model deployment.')
param gpt5MiniDeploymentName string = 'gpt-5-mini'

@description('Capacity (in thousands of tokens-per-minute) for GPT-5-mini deployment.')
param gpt5MiniCapacity int = 50

@description('Secondary location for OpenAI resources.')
param secondaryLocation string = 'swedencentral'

@description('Azure OpenAI API version.')
param openAiApiVersion string = '2025-03-01-preview'

// ── Resource Group ──────────────────────────────────────────────────────────────

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
}

// ── Azure OpenAI — Primary (location) ───────────────────────────────────────────

module openAiPrimary 'modules/openai.bicep' = {
  name: 'openai-primary'
  scope: rg
  params: {
    name: 'oai-${environmentName}'
    location: location
    gpt5MiniDeploymentName: gpt5MiniDeploymentName
    gpt5MiniCapacity: gpt5MiniCapacity
  }
}

// ── Azure OpenAI — Secondary (swedencentral) ────────────────────────────────────

module openAiSecondary 'modules/openai.bicep' = {
  name: 'openai-secondary'
  scope: rg
  params: {
    name: 'oai-${environmentName}-sec'
    location: secondaryLocation
    gpt5MiniDeploymentName: gpt5MiniDeploymentName
    gpt5MiniCapacity: gpt5MiniCapacity
  }
}

// ── Outputs (map to .env variables) — default to primary ────────────────────────

output FOUNDRY_API_KEY string = openAiPrimary.outputs.apiKey
output FOUNDRY_ENDPOINT string = openAiPrimary.outputs.endpoint
output FOUNDRY_INSTANCE string = openAiPrimary.outputs.name
output FOUNDRY_MODEL_NAME string = gpt5MiniDeploymentName
output FOUNDRY_MODEL_API_VERSION string = openAiApiVersion
output TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME string = gpt5MiniDeploymentName
output TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION string = openAiApiVersion
output ENDPOINT string = openAiPrimary.outputs.endpoint

// ── Secondary outputs (for manual .env override or failover) ────────────────────

output SECONDARY_FOUNDRY_API_KEY string = openAiSecondary.outputs.apiKey
output SECONDARY_FOUNDRY_ENDPOINT string = openAiSecondary.outputs.endpoint
output SECONDARY_FOUNDRY_INSTANCE string = openAiSecondary.outputs.name
