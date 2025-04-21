@description('The name of the Event Grid Topic')
param eventGridTopicName string

@description('The Azure region for the Event Grid Topic')
param location string

@description('The WebApp endpoint URL for event subscription')
param webAppEndpoint string

@description('The WebApp resource ID')
param webAppId string

@description('The WebApp principal ID (managed identity)')
param webAppPrincipalId string

@description('Whether to create role assignments (requires elevated permissions)')
param createRoleAssignments bool = true

// Create the Event Grid Topic
resource eventGridTopic 'Microsoft.EventGrid/topics@2023-06-01-preview' = {
  name: eventGridTopicName
  location: location
  properties: {
    inputSchema: 'EventGridSchema'
    publicNetworkAccess: 'Enabled'
    dataResidencyBoundary: 'WithinGeopair'
    disableLocalAuth: true // Using RBAC exclusively
    minimumTlsVersionAllowed: '1.2'
  }
}

// Create an event subscription to deliver events to the WebApp
resource eventSubscription 'Microsoft.EventGrid/topics/eventSubscriptions@2023-06-01-preview' = {
  parent: eventGridTopic
  name: 'WebAppSubscription'
  properties: {
    deliveryWithResourceIdentity: {
      destination: {
        endpointType: 'WebHook'
        properties: {
          endpointUrl: webAppEndpoint
          maxEventsPerBatch: 1
          preferredBatchSizeInKilobytes: 64
        }
      }
      identity: {
        type: 'SystemAssigned'
      }
    }
    eventDeliverySchema: 'EventGridSchema'
    filter: {
      includedEventTypes: [
        'All'
      ]
      enableAdvancedFilteringOnArrays: true
    }
    retryPolicy: {
      maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}

// RBAC role definitions for Event Grid
@description('Built-in Event Grid Data Sender role')
resource eventGridDataSenderRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: 'd5a91429-5739-47e2-a06b-3470a27159e7' // This is the ID for the 'EventGrid Data Sender' role
}

@description('Built-in Event Grid Data Receiver role')
resource eventGridDataReceiverRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde' // This is the ID for the 'EventGrid Data Receiver' role
}

// Assign EventGrid Data Sender role to allow sending events to the topic
resource senderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (createRoleAssignments) {
  name: guid(eventGridTopic.id, webAppId, 'sender')
  scope: eventGridTopic
  properties: {
    roleDefinitionId: eventGridDataSenderRole.id
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Assign EventGrid Data Receiver role to allow receiving events 
resource receiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (createRoleAssignments) {
  name: guid(eventGridTopic.id, webAppId, 'receiver')
  scope: eventGridTopic
  properties: {
    roleDefinitionId: eventGridDataReceiverRole.id
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output eventGridTopicEndpoint string = eventGridTopic.properties.endpoint
output eventGridTopicId string = eventGridTopic.id
