targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

@description('Location for all resources')
param location string = 'eastus'

@description('Base name for resources')
param baseName string = 'DevOpsCodeReviewer'

@description('Azure DevOps organization URL')
param adoOrganizationUrl string

@description('Azure OpenAI endpoint URL')
param llmEndpoint string

@description('Azure OpenAI deployment name')
param llmDeploymentName string = 'gpt-4o'

@description('Webhook secret for Azure DevOps service hook')
@secure()
param webhookSecret string = newGuid()

@description('Tags to apply to all resources')
param tags object = {
  project: 'DevOpsCodeReviewer'
  environment: environment
}

// Generate unique suffix based on subscription
var uniqueSuffix = uniqueString(subscription().subscriptionId, baseName, environment)
var resourceGroupName = 'rg-${baseName}-${environment}'

// Resource names (must be globally unique where required)
var storageAccountName = 'st${take(replace(baseName, '-', ''), 11)}${take(uniqueSuffix, 8)}'
var serviceBusNamespaceName = 'sb-${baseName}-${environment}-${take(uniqueSuffix, 4)}'
var keyVaultName = 'kv-${baseName}-${environment}'
var appInsightsName = 'ai-${baseName}-${environment}'
var logAnalyticsName = 'log-${baseName}-${environment}'
var functionAppName = 'func-${baseName}-${environment}-${take(uniqueSuffix, 4)}'
var appServicePlanName = 'asp-${baseName}-${environment}'

// Create resource group
resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// Deploy storage account
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  scope: rg
  params: {
    storageAccountName: storageAccountName
    location: location
    tags: tags
    sku: environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS'
  }
}

// Deploy Application Insights and Log Analytics
module appInsights 'modules/app-insights.bicep' = {
  name: 'appinsights-deployment'
  scope: rg
  params: {
    appInsightsName: appInsightsName
    logAnalyticsName: logAnalyticsName
    location: location
    tags: tags
    retentionInDays: environment == 'prod' ? 90 : 30
  }
}

// Deploy Service Bus
module serviceBus 'modules/service-bus.bicep' = {
  name: 'servicebus-deployment'
  scope: rg
  params: {
    namespaceName: serviceBusNamespaceName
    location: location
    tags: tags
    sku: environment == 'prod' ? 'Standard' : 'Standard'
    queueName: 'codereview-requests'
  }
}

// Deploy Key Vault
module keyVault 'modules/key-vault.bicep' = {
  name: 'keyvault-deployment'
  scope: rg
  params: {
    keyVaultName: keyVaultName
    location: location
    tags: tags
  }
}

// Deploy Function App
module functionApp 'modules/function-app.bicep' = {
  name: 'functionapp-deployment'
  scope: rg
  params: {
    functionAppName: functionAppName
    appServicePlanName: appServicePlanName
    location: location
    tags: tags
    storageConnectionString: storage.outputs.connectionString
    appInsightsConnectionString: appInsights.outputs.connectionString
    serviceBusConnectionString: serviceBus.outputs.connectionString
    serviceBusQueueName: serviceBus.outputs.queueName
    keyVaultUri: keyVault.outputs.keyVaultUri
    adoOrganizationUrl: adoOrganizationUrl
    llmEndpoint: llmEndpoint
    llmDeploymentName: llmDeploymentName
    webhookSecret: webhookSecret
    sku: 'Y1'
  }
}

// Grant Function App access to Key Vault (after function app is created)
module keyVaultAccess 'modules/key-vault.bicep' = {
  name: 'keyvault-access-deployment'
  scope: rg
  params: {
    keyVaultName: keyVaultName
    location: location
    tags: tags
    principalId: functionApp.outputs.principalId
  }
}

// Grant Function App access to Service Bus
module serviceBusAccess 'modules/service-bus.bicep' = {
  name: 'servicebus-access-deployment'
  scope: rg
  params: {
    namespaceName: serviceBusNamespaceName
    location: location
    tags: tags
    principalId: functionApp.outputs.principalId
  }
}

// Outputs
@description('Resource group name')
output resourceGroupName string = rg.name

@description('Function App name')
output functionAppName string = functionApp.outputs.functionAppName

@description('Function App URL')
output functionAppUrl string = 'https://${functionApp.outputs.defaultHostname}'

@description('Webhook URL for Azure DevOps')
output webhookUrl string = functionApp.outputs.webhookUrl

@description('Key Vault name')
output keyVaultName string = keyVault.outputs.keyVaultName

@description('Key Vault URI')
output keyVaultUri string = keyVault.outputs.keyVaultUri

@description('Service Bus namespace')
output serviceBusNamespace string = serviceBus.outputs.namespaceName

@description('Application Insights name')
output appInsightsName string = appInsights.outputs.logAnalyticsName
