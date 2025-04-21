@description('The environment name (dev, test, prod)')
param environmentName string = 'dev'

@description('The Azure region for all resources')
param location string = resourceGroup().location

// Updated to ensure the prefix always includes the environment name explicitly
@description('The name prefix for all resources')
param resourceNamePrefix string = ''

// Set a local value for the prefix, ensuring it's never empty
var actualResourcePrefix = empty(resourceNamePrefix) ? 'eventgrid${environmentName}' : resourceNamePrefix

// Variables for resource naming - use the actualResourcePrefix variable
var appServicePlanName = '${actualResourcePrefix}-plan'
var webAppName = '${actualResourcePrefix}-app'
var appInsightsName = '${actualResourcePrefix}-insights'
var keyVaultName = '${actualResourcePrefix}-kv'
var eventGridTopicName = '${actualResourcePrefix}-topic'

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
    keyVaultName: keyVaultName // Pass the variable directly, not the output
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
    webAppPrincipalId: webApp.outputs.principalId // This dependency is now okay
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
