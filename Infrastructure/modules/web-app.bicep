@description('The name of the Web App')
param webAppName string

@description('The Azure region for the Web App')
param location string

@description('The resource ID of the App Service Plan')
param appServicePlanId string

@description('The Application Insights connection string')
param appInsightsConnectionString string

@description('The name of the Key Vault')
param keyVaultName string

@description('The environment name (dev, test, prod)')
param environmentName string

resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      http20Enabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName
        }
        {
          name: 'ApplicationInsights__ConnectionString'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVaultName'
          value: keyVaultName
        }
        {
          name: 'Authentication--Authority'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/AuthorityUrl/)'
        }
        {
          name: 'Authentication--Audience'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/AudienceUrl/)'
        }
        {
          name: 'Authentication--ClientId'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/ClientId/)'
        }
        {
          name: 'Authentication--ClientSecret'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/ClientSecret/)'
        }
        {
          name: 'EventGrid--ValidationKey'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/EventGridValidationKey/)'
        }
        {
          name: 'EventGrid--AllowedTopics--0'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/EventGridAllowedTopic0/)'
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output webAppHostName string = webApp.properties.defaultHostName
output principalId string = webApp.identity.principalId
output webAppId string = webApp.id
