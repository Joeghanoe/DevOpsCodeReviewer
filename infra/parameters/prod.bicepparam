using '../main.bicep'

param environment = 'prod'
param location = 'eastus'
param baseName = 'DevOpsCodeReviewer'

// Replace these with your actual values
param adoOrganizationUrl = 'https://dev.azure.com/your-organization'
param llmEndpoint = 'https://your-openai-prod.openai.azure.com/'
param llmDeploymentName = 'gpt-4o'

param tags = {
  project: 'DevOpsCodeReviewer'
  environment: 'prod'
  costCenter: 'production'
}
