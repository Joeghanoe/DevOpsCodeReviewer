using './main.bicep'

param environment = 'dev'
param location = 'westeurope'
param projectName = 'devops-agent'
param adoOrganizationUrl = 'https://dev.azure.com/your-organization'
param llmEndpoint = 'https://devops-agent-we-dev-oai.openai.azure.com/'
param llmDeploymentName = 'gpt-4o'
