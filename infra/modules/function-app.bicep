@description('Name of the Function App')
param functionAppName string

@description('Name of the App Service Plan')
param appServicePlanName string

@description('Location for resources')
param location string = resourceGroup().location

@description('Tags to apply to resources')
param tags object = {}

@description('Storage account connection string')
@secure()
param storageConnectionString string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Service Bus connection string')
@secure()
param serviceBusConnectionString string

@description('Service Bus queue name')
param serviceBusQueueName string

@description('Key Vault URI')
param keyVaultUri string

@description('Azure DevOps organization URL')
param adoOrganizationUrl string

@description('LLM endpoint URL')
param llmEndpoint string

@description('LLM deployment name')
param llmDeploymentName string = 'gpt-4o'

@description('App Service Plan SKU')
@allowed([
  'Y1'      // Consumption
  'EP1'     // Elastic Premium
  'EP2'
  'EP3'
])
param sku string = 'Y1'

var isConsumptionPlan = sku == 'Y1'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: sku
    tier: isConsumptionPlan ? 'Dynamic' : 'ElasticPremium'
  }
  properties: {
    reserved: false  // Windows
    maximumElasticWorkerCount: isConsumptionPlan ? null : 20
  }
}

resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  tags: union(tags, {
    'azd-service-name': 'functions'
  })
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      functionAppScaleLimit: isConsumptionPlan ? 200 : null
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ServiceBus__ConnectionString'
          value: serviceBusConnectionString
        }
        {
          name: 'ServiceBus__QueueName'
          value: serviceBusQueueName
        }
        {
          name: 'KeyVault__VaultUrl'
          value: keyVaultUri
        }
        {
          name: 'AzureDevOps__OrganizationUrl'
          value: adoOrganizationUrl
        }
        {
          name: 'AzureDevOps__PatSecretName'
          value: 'ado-pat-token'
        }
        {
          name: 'Llm__Endpoint'
          value: llmEndpoint
        }
        {
          name: 'Llm__DeploymentName'
          value: llmDeploymentName
        }
        {
          name: 'Llm__ApiKeySecretName'
          value: 'foundry-api-key'
        }
      ]
    }
  }
}

@description('Function App resource ID')
output functionAppId string = functionApp.id

@description('Function App name')
output functionAppName string = functionApp.name

@description('Function App default hostname')
output defaultHostname string = functionApp.properties.defaultHostName

@description('Function App principal ID')
output principalId string = functionApp.identity.principalId

@description('Webhook URL')
output webhookUrl string = 'https://${functionApp.properties.defaultHostName}/api/webhook'
