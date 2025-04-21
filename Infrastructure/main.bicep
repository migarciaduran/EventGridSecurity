@description('The environment name (dev, test, prod)')
param environmentName string = 'dev'

@description('The Azure region for all resources')
param location string = resourceGroup().location

@description('The name prefix for all resources')
param resourceNamePrefix string = 'eventgrid${environmentName}'

// Variables for resource naming
var appServicePlanName = '${resourceNamePrefix}-plan'
var webAppName = '${resourceNamePrefix}-app'
var appInsightsName = '${resourceNamePrefix}-insights'
var keyVaultName = '${resourceNamePrefix}-kv'
var eventGridTopicName = '${resourceNamePrefix}-topic'

// App Service Plan
module appServicePlan 'modules/app-service-plan.bicep' = {
  name: 'appServicePlanDeployment'
  params: {
    appServicePlanName: appServicePlanName
    location: location
    sku: {
      name: 'P1v2'
      tier: 'PremiumV2'
      size: 'P1v2'
      family: 'Pv2'
      capacity: 1
    }
  }
}

// Web App with Managed Identity
module webApp 'modules/web-app.bicep' = {
  name: 'webAppDeployment'
  params: {
    webAppName: webAppName
    location: location
    appServicePlanId: appServicePlan.outputs.appServicePlanId
    appInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultName: keyVault.outputs.keyVaultName
    environmentName: environmentName
  }
}

// Application Insights for monitoring
module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsightsDeployment'
  params: {
    appInsightsName: appInsightsName
    location: location
  }
}

// Key Vault for secrets
module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVaultDeployment'
  params: {
    keyVaultName: keyVaultName
    location: location
    webAppPrincipalId: webApp.outputs.principalId
  }
}

// Event Grid Topic
module eventGridTopic 'modules/event-grid-topic.bicep' = {
  name: 'eventGridTopicDeployment'
  params: {
    eventGridTopicName: eventGridTopicName
    location: location
    webAppEndpoint: 'https://${webApp.outputs.webAppHostName}/api/standardeventgrid'
    webAppId: webApp.outputs.webAppId
  }
}

// Outputs that may be useful
output webAppName string = webApp.outputs.webAppName
output webAppHostName string = webApp.outputs.webAppHostName
output keyVaultName string = keyVault.outputs.keyVaultName
output eventGridTopicEndpoint string = eventGridTopic.outputs.eventGridTopicEndpoint
