#!/bin/bash

# Azure DevOps AI Code Reviewer - Interactive Setup Script
# Usage: ./setup.sh [--environment dev|staging|prod] [--location eastus]

set -e

# Default values
ENVIRONMENT="dev"
LOCATION="eastus"
SKIP_INFRA=false
SKIP_DEPLOY=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --environment|-e)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --location|-l)
            LOCATION="$2"
            shift 2
            ;;
        --skip-infra)
            SKIP_INFRA=true
            shift
            ;;
        --skip-deploy)
            SKIP_DEPLOY=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

header() {
    echo -e "\n${CYAN}========================================${NC}"
    echo -e "${CYAN}$1${NC}"
    echo -e "${CYAN}========================================${NC}\n"
}

step() {
    echo -e "${YELLOW}[*] $1${NC}"
}

success() {
    echo -e "${GREEN}[+] $1${NC}"
}

error() {
    echo -e "${RED}[!] $1${NC}"
}

# Banner
echo -e "${MAGENTA}"
cat << 'EOF'

    _    ____        ____          _      ____            _
   / \  |_  / __ _ / ___|___   __| | ___|  _ \ _____   _(_) _____      _____ _ __
  / _ \  / / / _` | |   / _ \ / _` |/ _ \ |_) / _ \ \ / / |/ _ \ \ /\ / / _ \ '__|
 / ___ \/ /_| (_| | |__| (_) | (_| |  __/  _ <  __/\ V /| |  __/\ V  V /  __/ |
/_/   \_/____|\__,_|\____\___/ \__,_|\___|_| \_\___| \_/ |_|\___| \_/\_/ \___|_|

                        AI-Powered Code Review for Azure DevOps

EOF
echo -e "${NC}"

header "Prerequisites Check"

# Check Azure CLI
step "Checking Azure CLI..."
if ! command -v az &> /dev/null; then
    error "Azure CLI not found. Install from https://aka.ms/installazurecli"
    exit 1
fi
AZ_VERSION=$(az version --query '"azure-cli"' -o tsv)
success "Azure CLI version $AZ_VERSION"

# Check .NET SDK
step "Checking .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    error ".NET SDK not found. Install from https://dot.net"
    exit 1
fi
DOTNET_VERSION=$(dotnet --version)
success ".NET SDK version $DOTNET_VERSION"

# Check Azure Functions Core Tools (optional)
step "Checking Azure Functions Core Tools..."
if command -v func &> /dev/null; then
    FUNC_VERSION=$(func --version)
    success "Azure Functions Core Tools version $FUNC_VERSION"
else
    echo -e "${YELLOW}[!] Azure Functions Core Tools not found (optional for local dev)${NC}"
fi

# Check jq
step "Checking jq..."
if ! command -v jq &> /dev/null; then
    error "jq not found. Install with: brew install jq (macOS) or apt install jq (Linux)"
    exit 1
fi
success "jq is installed"

# Check Azure login
step "Checking Azure login..."
if ! az account show &> /dev/null; then
    echo "You need to login to Azure. Opening browser..."
    az login
fi
ACCOUNT_NAME=$(az account show --query 'name' -o tsv)
USER_NAME=$(az account show --query 'user.name' -o tsv)
success "Logged in as $USER_NAME"
echo -e "Subscription: $ACCOUNT_NAME"

# Confirm subscription
read -p "Use this subscription? (Y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Nn]$ ]]; then
    echo -e "\nAvailable subscriptions:"
    az account list --output table
    read -p "Enter subscription ID: " SUB_ID
    az account set --subscription "$SUB_ID"
fi

header "Configuration"

# Get Azure DevOps organization URL
read -p "Enter your Azure DevOps organization URL (e.g., https://dev.azure.com/myorg): " ADO_URL
if [[ ! $ADO_URL == https://* ]]; then
    ADO_URL="https://dev.azure.com/$ADO_URL"
fi

# Get Azure OpenAI endpoint
read -p "Enter your Azure OpenAI endpoint URL: " LLM_ENDPOINT
if [[ ! $LLM_ENDPOINT == */ ]]; then
    LLM_ENDPOINT="$LLM_ENDPOINT/"
fi

# Get deployment name
read -p "Enter your Azure OpenAI deployment name [gpt-4o]: " LLM_DEPLOYMENT
LLM_DEPLOYMENT=${LLM_DEPLOYMENT:-gpt-4o}

# Generate webhook secret
WEBHOOK_SECRET=$(openssl rand -hex 8)
echo -e "Generated webhook secret: $WEBHOOK_SECRET"

if [ "$SKIP_INFRA" = false ]; then
    header "Deploying Infrastructure"

    step "Deploying Bicep templates to Azure..."

    DEPLOYMENT_OUTPUT=$(az deployment sub create \
        --location "$LOCATION" \
        --template-file "infra/main.bicep" \
        --parameters "infra/parameters/$ENVIRONMENT.bicepparam" \
        --parameters "adoOrganizationUrl=$ADO_URL" \
        --parameters "llmEndpoint=$LLM_ENDPOINT" \
        --parameters "llmDeploymentName=$LLM_DEPLOYMENT" \
        --parameters "webhookSecret=$WEBHOOK_SECRET" \
        --query "properties.outputs" \
        -o json)

    if [ $? -ne 0 ]; then
        error "Infrastructure deployment failed!"
        exit 1
    fi

    FUNCTION_APP_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.functionAppName.value')
    KEY_VAULT_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.keyVaultName.value')
    WEBHOOK_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.webhookUrl.value')
    RESOURCE_GROUP=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.resourceGroupName.value')

    success "Infrastructure deployed successfully!"
    echo -e "  Function App: $FUNCTION_APP_NAME"
    echo -e "  Key Vault: $KEY_VAULT_NAME"
    echo -e "  Webhook URL: $WEBHOOK_URL"
fi

header "Configure Secrets"

echo -e "${YELLOW}You need an Azure DevOps PAT with the following permissions:${NC}"
echo "  - Code: Read & Write"
echo "  - Pull Request Threads: Read & Write"
echo ""
read -sp "Enter your Azure DevOps PAT: " PAT
echo ""

read -sp "Enter your Azure OpenAI API key: " API_KEY
echo ""

step "Storing secrets in Key Vault..."
az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name "ado-pat-token" --value "$PAT" --output none
az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name "foundry-api-key" --value "$API_KEY" --output none
success "Secrets stored successfully!"

if [ "$SKIP_DEPLOY" = false ]; then
    header "Deploying Function App"

    step "Building the project..."
    dotnet publish "src/DevOpsCodeReviewer.Functions" --configuration Release --output "./publish"

    step "Deploying to Azure..."
    cd ./publish
    func azure functionapp publish "$FUNCTION_APP_NAME"
    cd ..

    success "Function App deployed successfully!"

    # Health check
    step "Running health check..."
    sleep 10
    HEALTH_URL="https://$FUNCTION_APP_NAME.azurewebsites.net/api/health/live"
    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$HEALTH_URL")

    if [ "$HTTP_STATUS" = "200" ]; then
        success "Health check passed!"
    else
        echo -e "${YELLOW}[!] Health check returned $HTTP_STATUS - the function may still be starting up${NC}"
    fi
fi

header "Setup Complete!"

echo -e "${GREEN}Next Steps:

1. Configure Azure DevOps Service Hook:
   - Go to: ${ADO_URL}/_settings/serviceHooks
   - Create a new Web Hooks subscription
   - Event: Pull request created
   - URL: ${WEBHOOK_URL}?secret=${WEBHOOK_SECRET}

2. Repeat for 'Pull request updated' event

3. Create a test PR to verify the integration

Useful URLs:
  - Health Check: https://${FUNCTION_APP_NAME}.azurewebsites.net/api/health
  - Webhook URL: ${WEBHOOK_URL}

${NC}"

echo -e "${CYAN}Happy reviewing! ${NC}"
