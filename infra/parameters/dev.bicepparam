using '../main.bicep'

param environment = 'dev'
param location = 'westeurope'
param projectName = 'devops-agent'

// Replace these with your actual values
param adoOrganizationUrl = 'https://dev.azure.com/your-organization'
param llmEndpoint = 'https://devops-agent-we-dev-oai.openai.azure.com/'
param llmDeploymentName = 'gpt-4o'

param tags = {
  project: 'devops-agent'
  environment: 'dev'
  costCenter: 'development'
}
