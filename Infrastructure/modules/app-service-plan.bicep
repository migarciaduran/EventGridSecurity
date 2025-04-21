@description('The name of the App Service Plan')
param appServicePlanName string

@description('The Azure region for the App Service Plan')
param location string

@description('The SKU of the App Service Plan')
param sku object

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: sku.name
    tier: sku.tier
    size: sku.size
    family: sku.family
    capacity: sku.capacity
  }
  kind: 'app'
  properties: {
    perSiteScaling: false
    elasticScaleEnabled: false
    maximumElasticWorkerCount: 1
    isSpot: false
    reserved: false
    isXenon: false
    hyperV: false
    targetWorkerCount: 0
    targetWorkerSizeId: 0
    zoneRedundant: false
  }
}

output appServicePlanId string = appServicePlan.id
