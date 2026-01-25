# Setup Guide

This guide walks you through deploying the Azure DevOps AI Code Reviewer to your Azure environment.

## Prerequisites

Before you begin, ensure you have:

- [ ] Azure subscription with Contributor access
- [ ] Azure DevOps organization with Project Administrator access
- [ ] Azure CLI installed (`az --version`)
- [ ] .NET 8 SDK installed (`dotnet --version`)
- [ ] Azure Functions Core Tools v4 (`func --version`)
- [ ] Azure OpenAI or OpenAI API access

## Step 1: Clone the Repository

```bash
git clone https://github.com/Joeghanoe/DevOpsCodeReviewer.git
cd DevOpsCodeReviewer
```

## Step 2: Create Azure OpenAI Resource

1. Go to the [Azure Portal](https://portal.azure.com)
2. Create an **Azure OpenAI** resource
3. Deploy a model (recommended: `gpt-4o`)
4. Note the following:
   - Endpoint URL: `https://your-resource.openai.azure.com/`
   - API Key (from Keys and Endpoint section)
   - Deployment name (e.g., `gpt-4o`)

## Step 3: Create Azure DevOps PAT

1. Go to Azure DevOps: `https://dev.azure.com/your-org`
2. Click your profile icon → **Personal access tokens**
3. Click **+ New Token**
4. Configure:
   - Name: `DevOpsCodeReviewer`
   - Expiration: 1 year (or as needed)
   - Scopes:
     - **Code**: Read & Write
     - **Pull Request Threads**: Read & Write
5. Copy the token (you won't see it again!)

## Step 4: Deploy Infrastructure

### Option A: Using Azure CLI

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "Your Subscription Name"

# Update parameters
# Edit infra/parameters/dev.bicepparam with your values:
# - adoOrganizationUrl: Your Azure DevOps org URL
# - llmEndpoint: Your Azure OpenAI endpoint

# Deploy
az deployment sub create \
  --location westeurope \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam
```

### Option B: Using the Setup Script

```bash
# PowerShell
./scripts/setup.ps1

# Bash
./scripts/setup.sh
```

### Deployment Outputs

After deployment, note these values:
- **Function App Name**: `func-DevOpsCodeReviewer-dev-xxxx`
- **Key Vault Name**: `kv-DevOpsCodeReviewer-dev`
- **Webhook URL**: `https://func-xxx.azurewebsites.net/api/webhook`

## Step 5: Configure Secrets in Key Vault

```bash
# Set your Key Vault name from deployment output
KV_NAME="kv-DevOpsCodeReviewer-dev"

# Add Azure DevOps PAT
az keyvault secret set \
  --vault-name $KV_NAME \
  --name "ado-pat-token" \
  --value "your-pat-token-here"

# Add Azure OpenAI API Key
az keyvault secret set \
  --vault-name $KV_NAME \
  --name "foundry-api-key" \
  --value "your-openai-api-key-here"
```

## Step 6: Deploy Function App

```bash
# Build the project
dotnet publish src/DevOpsCodeReviewer.Functions \
  --configuration Release \
  --output ./publish

# Deploy to Azure
func azure functionapp publish func-DevOpsCodeReviewer-dev-xxxx
```

Or use GitHub Actions by pushing to main branch.

## Step 7: Configure Azure DevOps Service Hook

1. Go to your Azure DevOps project
2. Navigate to **Project Settings** → **Service hooks**
3. Click **+ Create subscription**
4. Select **Web Hooks**
5. Configure trigger:
   - Event: **Pull request created**
   - Repository: (select your repo or "Any")
   - Target branch: (optional filter)
6. Configure action:
   - URL: `https://func-xxx.azurewebsites.net/api/webhook?secret=YOUR_WEBHOOK_SECRET`
   - (Get the webhook secret from the Function App configuration or generate a new one)
7. Click **Test** to verify the connection
8. Click **Finish**

Repeat for **Pull request updated** event.

## Step 8: Test the Integration

1. Create a test pull request in your Azure DevOps repository
2. Wait 1-2 minutes
3. Check the PR for AI-generated comments
4. If no comments appear, check Application Insights for errors

## Troubleshooting

### No Comments Appearing

1. Check the health endpoint:
   ```bash
   curl https://func-xxx.azurewebsites.net/api/health
   ```

2. Check Application Insights:
   - Go to Azure Portal → Your Function App → Application Insights
   - Check **Failures** and **Logs**

3. Verify Service Hook:
   - Go to Azure DevOps → Service hooks
   - Check the hook's "Recent events" for delivery status

### Authentication Errors

1. Verify PAT token is correct in Key Vault
2. Check PAT permissions include Code Read/Write and PR Threads
3. Ensure PAT hasn't expired

### LLM Errors

1. Verify Azure OpenAI endpoint and deployment name
2. Check API key is correct in Key Vault
3. Verify model deployment has sufficient quota

## Next Steps

- [Configuration Guide](CONFIGURATION.md) - All configuration options
- [Customization Guide](CUSTOMIZATION.md) - Customize review prompts
- [Architecture](ARCHITECTURE.md) - System design details
