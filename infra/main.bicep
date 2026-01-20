targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

@description('Location for all resources')
param location string = 'westeurope'

@description('Project name for resources')
param projectName string = 'devops-agent'

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
  project: projectName
  environment: environment
}

// Region abbreviation mapping
var regionAbbreviations = {
  westeurope: 'we'
  westeurope2: 'eus2'
  westus: 'wus'
  westus2: 'wus2'
  northeurope: 'ne'
  uksouth: 'uks'
  ukwest: 'ukw'
}
var regionAbbr = contains(regionAbbreviations, location) ? regionAbbreviations[location] : take(location, 3)

// Environment abbreviation mapping
var envAbbreviations = {
  dev: 'dev'
  staging: 'stg'
  prod: 'prd'
}
var envAbbr = envAbbreviations[environment]

// Resource group name (as specified by user)
var resourceGroupName = projectName

// Naming convention: projectname-region-environment-resourcetype
// Resource names (must be globally unique where required)
var uniqueSuffix = uniqueString(subscription().subscriptionId, projectName, environment)
var storageAccountName = '${replace(projectName, '-', '')}${regionAbbr}${envAbbr}st'
var serviceBusNamespaceName = '${projectName}-${regionAbbr}-${envAbbr}-${uniqueSuffix}' // Since this must be unique globally
var keyVaultName = '${projectName}-${regionAbbr}-${envAbbr}-kv'
var appInsightsName = '${projectName}-${regionAbbr}-${envAbbr}-ai'
var logAnalyticsName = '${projectName}-${regionAbbr}-${envAbbr}-log'
var functionAppName = '${projectName}-${regionAbbr}-${envAbbr}-func'
var appServicePlanName = '${projectName}-${regionAbbr}-${envAbbr}-asp'

// Create resource group
resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// Deploy storage account
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment-${uniqueSuffix}'
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
  name: 'appinsights-deployment-${uniqueSuffix}'
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
  name: 'servicebus-deployment-${uniqueSuffix}'
  scope: rg
  params: {
    namespaceName: serviceBusNamespaceName
    location: location
    tags: tags
    sku: 'Standard'
    queueName: 'codereview-requests'
  }
}

// Deploy Key Vault
module keyVault 'modules/key-vault.bicep' = {
  name: 'keyvault-deployment-${uniqueSuffix}'
  scope: rg
  params: {
    keyVaultName: keyVaultName
    location: location
    tags: tags
  }
}

// Deploy Function App
module functionApp 'modules/function-app.bicep' = {
  name: 'functionapp-deployment-${uniqueSuffix}'
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
  name: 'keyvault-access-deployment-${uniqueSuffix}'
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
  name: 'servicebus-access-deployment-${uniqueSuffix}'
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
