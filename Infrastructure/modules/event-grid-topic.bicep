@description('The name of the Event Grid Topic')
param eventGridTopicName string

@description('The Azure region for the Event Grid Topic')
param location string

@description('The WebApp endpoint URL for event subscription')
param webAppEndpoint string

@description('The WebApp resource ID')
param webAppId string

// Create the Event Grid Topic
resource eventGridTopic 'Microsoft.EventGrid/topics@2023-06-01-preview' = {
  name: eventGridTopicName
  location: location
  properties: {
    inputSchema: 'EventGridSchema'
    publicNetworkAccess: 'Enabled'
    dataResidencyBoundary: 'WithinGeopair'
    disableLocalAuth: true
    minimumTlsVersionRequired: '1.2'
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

// Create role assignments for the WebApp to handle event grid events
@description('Built-in Event Grid Data Sender role')
resource eventGridDataSenderRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: 'd5a91429-5739-47e2-a06b-3470a27159e7' // This is the ID for the 'EventGrid Data Sender' role
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(eventGridTopic.id, webAppId, eventGridDataSenderRole.id)
  scope: eventGridTopic
  properties: {
    roleDefinitionId: eventGridDataSenderRole.id
    principalId: webAppId
    principalType: 'ServicePrincipal'
  }
}

output eventGridTopicEndpoint string = eventGridTopic.properties.endpoint
output eventGridTopicId string = eventGridTopic.id
