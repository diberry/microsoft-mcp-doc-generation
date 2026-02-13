@description('Name of the Azure OpenAI resource.')
param name string

@description('Location for the resource.')
param location string

@description('Name of the GPT-4o-mini model deployment.')
param gpt4oMiniDeploymentName string

@description('Name of the GPT-4o model deployment.')
param gpt4oDeploymentName string

@description('Capacity for GPT-4o-mini deployment (thousands of TPM).')
param gpt4oMiniCapacity int

@description('Capacity for GPT-4o deployment (thousands of TPM).')
param gpt4oCapacity int

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

// ── GPT-4o-mini Deployment ──────────────────────────────────────────────────────

resource gpt4oMiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: gpt4oMiniDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: gpt4oMiniCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o-mini'
      version: '2024-07-18'
    }
  }
}

// ── GPT-4o Deployment ───────────────────────────────────────────────────────────

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: gpt4oDeploymentName
  dependsOn: [
    gpt4oMiniDeployment
  ]
  sku: {
    name: 'GlobalStandard'
    capacity: gpt4oCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-11-20'
    }
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────────

output endpoint string = openAi.properties.endpoint
output name string = openAi.name
output id string = openAi.id
output apiKey string = openAi.listKeys().key1
