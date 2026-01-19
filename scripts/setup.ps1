#Requires -Version 7.0

<#
.SYNOPSIS
    Interactive setup script for Azure DevOps AI Code Reviewer.

.DESCRIPTION
    This script guides you through deploying the Azure DevOps AI Code Reviewer
    to your Azure environment.

.EXAMPLE
    ./setup.ps1

.NOTES
    Requires:
    - PowerShell 7+
    - Azure CLI
    - .NET 8 SDK
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment = 'dev',

    [Parameter()]
    [string]$Location = 'eastus',

    [Parameter()]
    [switch]$SkipInfrastructure,

    [Parameter()]
    [switch]$SkipFunctionDeploy
)

$ErrorActionPreference = 'Stop'

# Colors for output
function Write-Header {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host "[*] $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "[+] $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "[!] $Message" -ForegroundColor Red
}

function Test-Command {
    param([string]$Command)
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

# Banner
Write-Host @"

    _    ____        ____          _      ____            _
   / \  |_  / __ _ / ___|___   __| | ___|  _ \ _____   _(_) _____      _____ _ __
  / _ \  / / / _` | |   / _ \ / _` |/ _ \ |_) / _ \ \ / / |/ _ \ \ /\ / / _ \ '__|
 / ___ \/ /_| (_| | |__| (_) | (_| |  __/  _ <  __/\ V /| |  __/\ V  V /  __/ |
/_/   \_/____|\__,_|\____\___/ \__,_|\___|_| \_\___| \_/ |_|\___| \_/\_/ \___|_|

                        AI-Powered Code Review for Azure DevOps

"@ -ForegroundColor Magenta

Write-Header "Prerequisites Check"

# Check Azure CLI
Write-Step "Checking Azure CLI..."
if (-not (Test-Command "az")) {
    Write-Error "Azure CLI not found. Install from https://aka.ms/installazurecli"
    exit 1
}
$azVersion = az version --query '"azure-cli"' -o tsv
Write-Success "Azure CLI version $azVersion"

# Check .NET SDK
Write-Step "Checking .NET SDK..."
if (-not (Test-Command "dotnet")) {
    Write-Error ".NET SDK not found. Install from https://dot.net"
    exit 1
}
$dotnetVersion = dotnet --version
Write-Success ".NET SDK version $dotnetVersion"

# Check Azure Functions Core Tools (optional)
Write-Step "Checking Azure Functions Core Tools..."
if (Test-Command "func") {
    $funcVersion = func --version
    Write-Success "Azure Functions Core Tools version $funcVersion"
} else {
    Write-Host "[!] Azure Functions Core Tools not found (optional for local dev)" -ForegroundColor Yellow
}

# Check Azure login
Write-Step "Checking Azure login..."
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "You need to login to Azure. Opening browser..." -ForegroundColor Yellow
    az login
    $account = az account show | ConvertFrom-Json
}
Write-Success "Logged in as $($account.user.name)"
Write-Host "Subscription: $($account.name)" -ForegroundColor Gray

# Confirm subscription
$confirm = Read-Host "Use this subscription? (Y/n)"
if ($confirm -eq 'n' -or $confirm -eq 'N') {
    Write-Host "`nAvailable subscriptions:" -ForegroundColor Yellow
    az account list --output table
    $subId = Read-Host "Enter subscription ID"
    az account set --subscription $subId
}

Write-Header "Configuration"

# Get Azure DevOps organization URL
$adoUrl = Read-Host "Enter your Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)"
if (-not $adoUrl.StartsWith("https://")) {
    $adoUrl = "https://dev.azure.com/$adoUrl"
}

# Get Azure OpenAI endpoint
$llmEndpoint = Read-Host "Enter your Azure OpenAI endpoint URL"
if (-not $llmEndpoint.EndsWith("/")) {
    $llmEndpoint = "$llmEndpoint/"
}

# Get deployment name
$llmDeployment = Read-Host "Enter your Azure OpenAI deployment name [gpt-4o]"
if ([string]::IsNullOrEmpty($llmDeployment)) {
    $llmDeployment = "gpt-4o"
}

# Generate webhook secret
$webhookSecret = [System.Guid]::NewGuid().ToString("N").Substring(0, 16)
Write-Host "Generated webhook secret: $webhookSecret" -ForegroundColor Gray

if (-not $SkipInfrastructure) {
    Write-Header "Deploying Infrastructure"

    Write-Step "Deploying Bicep templates to Azure..."

    $deployParams = @{
        location           = $Location
        environment        = $Environment
        adoOrganizationUrl = $adoUrl
        llmEndpoint        = $llmEndpoint
        llmDeploymentName  = $llmDeployment
        webhookSecret      = $webhookSecret
    }

    $paramsJson = $deployParams | ConvertTo-Json -Compress
    $deployment = az deployment sub create `
        --location $Location `
        --template-file "infra/main.bicep" `
        --parameters "infra/parameters/$Environment.bicepparam" `
        --parameters "adoOrganizationUrl=$adoUrl" `
        --parameters "llmEndpoint=$llmEndpoint" `
        --parameters "llmDeploymentName=$llmDeployment" `
        --parameters "webhookSecret=$webhookSecret" `
        --query "properties.outputs" `
        -o json | ConvertFrom-Json

    if (-not $deployment) {
        Write-Error "Infrastructure deployment failed!"
        exit 1
    }

    $functionAppName = $deployment.functionAppName.value
    $keyVaultName = $deployment.keyVaultName.value
    $webhookUrl = $deployment.webhookUrl.value
    $resourceGroup = $deployment.resourceGroupName.value

    Write-Success "Infrastructure deployed successfully!"
    Write-Host "  Function App: $functionAppName" -ForegroundColor Gray
    Write-Host "  Key Vault: $keyVaultName" -ForegroundColor Gray
    Write-Host "  Webhook URL: $webhookUrl" -ForegroundColor Gray
}

Write-Header "Configure Secrets"

# Get PAT token
Write-Host "You need an Azure DevOps PAT with the following permissions:" -ForegroundColor Yellow
Write-Host "  - Code: Read & Write" -ForegroundColor Gray
Write-Host "  - Pull Request Threads: Read & Write" -ForegroundColor Gray
Write-Host ""
$pat = Read-Host "Enter your Azure DevOps PAT" -AsSecureString
$patPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($pat)
)

# Get API key
$apiKey = Read-Host "Enter your Azure OpenAI API key" -AsSecureString
$apiKeyPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKey)
)

Write-Step "Storing secrets in Key Vault..."
az keyvault secret set --vault-name $keyVaultName --name "ado-pat-token" --value $patPlain --output none
az keyvault secret set --vault-name $keyVaultName --name "foundry-api-key" --value $apiKeyPlain --output none
Write-Success "Secrets stored successfully!"

if (-not $SkipFunctionDeploy) {
    Write-Header "Deploying Function App"

    Write-Step "Building the project..."
    dotnet publish "src/DevOpsCodeReviewer.Functions" --configuration Release --output "./publish"

    Write-Step "Deploying to Azure..."
    Push-Location "./publish"
    func azure functionapp publish $functionAppName
    Pop-Location

    Write-Success "Function App deployed successfully!"

    # Health check
    Write-Step "Running health check..."
    Start-Sleep -Seconds 10
    $healthUrl = "https://$functionAppName.azurewebsites.net/api/health/live"
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -Method Get -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Success "Health check passed!"
        }
    }
    catch {
        Write-Host "[!] Health check failed - the function may still be starting up" -ForegroundColor Yellow
    }
}

Write-Header "Setup Complete!"

Write-Host @"
Next Steps:

1. Configure Azure DevOps Service Hook:
   - Go to: $adoUrl/_settings/serviceHooks
   - Create a new Web Hooks subscription
   - Event: Pull request created
   - URL: $webhookUrl`?secret=$webhookSecret

2. Repeat for 'Pull request updated' event

3. Create a test PR to verify the integration

Useful URLs:
  - Health Check: https://$functionAppName.azurewebsites.net/api/health
  - Webhook URL: $webhookUrl
  - Key Vault: https://portal.azure.com/#@/resource/subscriptions/.../resourceGroups/$resourceGroup/providers/Microsoft.KeyVault/vaults/$keyVaultName

"@ -ForegroundColor Green

Write-Host "Happy reviewing! " -ForegroundColor Cyan
