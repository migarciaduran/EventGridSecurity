@description('The name of the Key Vault')
param keyVaultName string

@description('The Azure region for the Key Vault')
param location string

@description('The principal ID of the Web App managed identity')
param webAppPrincipalId string

// Key Vault resource with RBAC authorization
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    tenantId: subscription().tenantId
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enableRbacAuthorization: true
    sku: {
      family: 'A'
      name: 'standard'
    }
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Assign Key Vault Secrets User built-in role to the web app's managed identity
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webAppPrincipalId, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User role
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Create secret placeholders (these will be filled with actual values later)
resource authorityUrlSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'AuthorityUrl'
  properties: {
    value: '${environment().authentication.loginEndpoint}${subscription().tenantId}/' // Use environment() function
  }
}

resource audienceUrlSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'AudienceUrl'
  properties: {
    value: 'YOUR_APP_ID_URI'
  }
}

resource clientIdSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'ClientId'
  properties: {
    value: 'YOUR_CLIENT_ID'
  }
}

resource clientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'ClientSecret'
  properties: {
    value: 'YOUR_CLIENT_SECRET'
  }
}

resource validationKeySecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'EventGridValidationKey'
  properties: {
    value: 'YOUR_EVENT_GRID_VALIDATION_KEY'
  }
}

resource allowedTopicSecret 'Microsoft.KeyVault/vaults/secrets@2022-07-01' = {
  parent: keyVault
  name: 'EventGridAllowedTopic0'
  properties: {
    value: '/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.EventGrid/topics/{topic-name}'
  }
}

output keyVaultName string = keyVault.name
