using './main.bicep'

param environment = 'dev'
param location = 'eastus'
param baseName = 'DevOpsCodeReviewer'
param adoOrganizationUrl = 'https://dev.azure.com/your-organization'
param llmEndpoint = 'https://your-openai.openai.azure.com/'
param llmDeploymentName = 'gpt-4o'
