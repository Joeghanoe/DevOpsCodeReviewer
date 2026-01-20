using '../main.bicep'

param environment = 'prod'
param location = 'westeurope'
param projectName = 'devops-agent'

// Replace these with your actual values
param adoOrganizationUrl = 'https://dev.azure.com/your-organization'
param llmEndpoint = 'https://devops-agent-we-prd-oai.openai.azure.com/'
param llmDeploymentName = 'gpt-4o'

param tags = {
  project: 'devops-agent'
  environment: 'prod'
  costCenter: 'production'
}
