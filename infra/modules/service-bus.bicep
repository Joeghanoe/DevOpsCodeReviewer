@description('Name of the Service Bus namespace')
param namespaceName string

@description('Location for the Service Bus namespace')
param location string = resourceGroup().location

@description('Tags to apply to resources')
param tags object = {}

@description('Service Bus SKU')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param sku string = 'Standard'

@description('Name of the queue for code review requests')
param queueName string = 'codereview-requests'

@description('Principal ID to grant Service Bus access')
param principalId string = ''

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    zoneRedundant: sku == 'Premium'
  }
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    lockDuration: 'PT5M'  // 5 minutes for LLM processing
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    requiresSession: false
    defaultMessageTimeToLive: 'P1D'  // 1 day
    deadLetteringOnMessageExpiration: true
    enableBatchedOperations: true
    maxDeliveryCount: 3
    enablePartitioning: false
    autoDeleteOnIdle: 'P30D'  // 30 days
  }
}

// Azure Service Bus Data Sender role
var serviceBusDataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'

// Azure Service Bus Data Receiver role
var serviceBusDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

resource senderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(serviceBusNamespace.id, principalId, serviceBusDataSenderRoleId)
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource receiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(serviceBusNamespace.id, principalId, serviceBusDataReceiverRoleId)
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiverRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

@description('Service Bus namespace ID')
output namespaceId string = serviceBusNamespace.id

@description('Service Bus namespace name')
output namespaceName string = serviceBusNamespace.name

@description('Service Bus queue name')
output queueName string = queue.name

@description('Service Bus connection string')
output connectionString string = 'Endpoint=sb://${serviceBusNamespace.name}.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=${listKeys('${serviceBusNamespace.id}/AuthorizationRules/RootManageSharedAccessKey', serviceBusNamespace.apiVersion).primaryKey}'

@description('Service Bus endpoint')
output endpoint string = serviceBusNamespace.properties.serviceBusEndpoint
