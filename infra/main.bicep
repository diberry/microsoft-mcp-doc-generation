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

@description('Secondary location for AI Services resources.')
param secondaryLocation string = 'swedencentral'

@description('Azure AI Services API version.')
param openAiApiVersion string = '2025-03-01-preview'

// ── Resource Group ──────────────────────────────────────────────────────────────

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
}

// ── Azure AI Services — Primary (location) ──────────────────────────────────────
// Uses AIServices kind which supports Microsoft Foundry and Azure OpenAI
// Resource name kept as 'oai-' prefix for deployment continuity (legacy naming)

module openAiPrimary 'modules/openai.bicep' = {
  name: 'foundry-primary'
  scope: rg
  params: {
    name: 'oai-${environmentName}'
    location: location
    gpt5MiniDeploymentName: gpt5MiniDeploymentName
    gpt5MiniCapacity: gpt5MiniCapacity
  }
}

// ── Azure AI Services — Secondary (swedencentral) ───────────────────────────────

module openAiSecondary 'modules/openai.bicep' = {
  name: 'foundry-secondary'
  scope: rg
  params: {
    name: 'oai-${environmentName}-sec'
    location: secondaryLocation
    gpt5MiniDeploymentName: gpt5MiniDeploymentName
    gpt5MiniCapacity: gpt5MiniCapacity
  }
}

// ── Key Vault — Secure secret storage for API keys ──────────────────────────────

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    name: 'kv-${environmentName}'
    location: location
    openAiResourceId: openAiPrimary.outputs.id
  }
}

// ── Outputs (map to .env variables) — default to primary ────────────────────────

output FOUNDRY_ENDPOINT string = openAiPrimary.outputs.endpoint
output FOUNDRY_INSTANCE string = openAiPrimary.outputs.name
output FOUNDRY_MODEL_NAME string = gpt5MiniDeploymentName
output FOUNDRY_MODEL_API_VERSION string = openAiApiVersion
output FOUNDRY_USE_DEFAULT_CREDENTIAL string = 'true'
output TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME string = gpt5MiniDeploymentName
output TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_API_VERSION string = openAiApiVersion
output ENDPOINT string = openAiPrimary.outputs.endpoint
output KEYVAULT_URI string = keyVault.outputs.vaultUri
output KEYVAULT_SECRET_NAME string = keyVault.outputs.secretName

// ── Secondary outputs (for manual .env override or failover) ────────────────────

output SECONDARY_FOUNDRY_ENDPOINT string = openAiSecondary.outputs.endpoint
output SECONDARY_FOUNDRY_INSTANCE string = openAiSecondary.outputs.name
