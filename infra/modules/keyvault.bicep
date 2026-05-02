@description('Name of the Key Vault resource.')
param name string

@description('Location for the resource.')
param location string

@description('Resource ID of the Azure AI Services account whose key will be stored.')
param openAiResourceId string

@description('Name of the secret that stores the API key.')
param secretName string = 'foundry-api-key'

// ── Key Vault ────────────────────────────────────────────────────────────────────

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
  }
}

// ── Reference the OpenAI resource to retrieve its key ────────────────────────────

resource openAi 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: last(split(openAiResourceId, '/'))
}

// ── Store API key as a secret ────────────────────────────────────────────────────

resource apiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: secretName
  properties: {
    value: openAi.listKeys().key1
  }
}

// ── Outputs (safe — no secret values exposed) ────────────────────────────────────

output vaultUri string = keyVault.properties.vaultUri
output vaultName string = keyVault.name
output secretName string = apiKeySecret.name
