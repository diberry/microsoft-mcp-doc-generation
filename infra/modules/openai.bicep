@description('Name of the Azure OpenAI resource.')
param name string

@description('Location for the resource.')
param location string

@description('Name of the GPT-5-mini model deployment.')
param gpt5MiniDeploymentName string

@description('Capacity for GPT-5-mini deployment (thousands of TPM).')
param gpt5MiniCapacity int

// ── Azure OpenAI Account ────────────────────────────────────────────────────────

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
  }
}

// ── GPT-5-mini Deployment ────────────────────────────────────────────────────────

resource gpt5MiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: gpt5MiniDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: gpt5MiniCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5-mini'
      version: '2025-08-07'
    }
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────────

output endpoint string = openAi.properties.endpoint
output name string = openAi.name
output id string = openAi.id
output apiKey string = openAi.listKeys().key1
