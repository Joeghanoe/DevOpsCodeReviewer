# Azure DevOps AI Code Reviewer

An open-source, production-ready Azure DevOps Pull Request code review bot powered by Azure OpenAI.

## Architecture

```
Azure DevOps          Azure Functions              Azure OpenAI
Service Hook  ──────► HTTP Trigger  ──────►       (Foundry)
                      (Webhook)          │              │
                           │             │              │
                           ▼             │              ▼
                      Service Bus  ◄─────┴───── SB Trigger
                        Queue                  (Processor)
                           │                        │
                           ▼                        ▼
                      Dead Letter            PR Comments
                       Handler               (ADO REST API)
```

## Features

- **Automated PR Reviews**: Automatically reviews code changes when PRs are created or updated
- **Multi-Language Support**: C#, TypeScript, JavaScript, Python, Java, Go, Rust, and more
- **Smart Filtering**: Skips binary files, lock files, and generated code
- **Duplicate Detection**: Avoids posting redundant comments
- **Large PR Handling**: Intelligent chunking for PRs with many changes
- **Dead Letter Handling**: Graceful handling of processing failures
- **Infrastructure as Code**: Full Bicep templates for Azure deployment

## Quick Start

### Prerequisites

- Azure subscription
- Azure DevOps organization with admin access
- .NET 8 SDK
- Azure CLI
- Azure Functions Core Tools v4

### 1. Clone and Configure

```bash
git clone https://github.com/your-org/azdo-code-reviewer.git
cd azdo-code-reviewer

# Copy example settings
cp src/DevOpsCodeReviewer.Functions/local.settings.json.example \
   src/DevOpsCodeReviewer.Functions/local.settings.json
```

### 2. Deploy Infrastructure

```bash
# Login to Azure
az login

# Deploy infrastructure
az deployment sub create \
  --location westeurope \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam
```

### 3. Configure Azure DevOps Service Hook

1. Go to your Azure DevOps project settings
2. Navigate to **Service hooks** → **Create subscription**
3. Select **Web Hooks**
4. Choose trigger: **Pull request created** and **Pull request updated**
5. Set URL: `https://{your-function}.azurewebsites.net/api/webhook?secret={your-secret}`

### 4. Create a Test PR

Create a pull request in your Azure DevOps repository. Within 1-2 minutes, you should see AI-generated review comments.

## Configuration

| Setting | Description | Default |
|---------|-------------|---------|
| `AzureDevOps__OrganizationUrl` | Your ADO organization URL | Required |
| `AzureDevOps__PatSecretName` | Key Vault secret name for PAT | `ado-pat-token` |
| `Llm__Endpoint` | Azure OpenAI endpoint | Required |
| `Llm__DeploymentName` | Model deployment name | `gpt-4o` |
| `Llm__MaxTokens` | Max tokens per request | `4096` |
| `ServiceBus__QueueName` | Service Bus queue name | `codereview-requests` |

See [Configuration Guide](docs/CONFIGURATION.md) for full details.

## Project Structure

```
├── src/
│   └── DevOpsCodeReviewer.Functions/
│       ├── Configuration/     # Strongly-typed settings
│       ├── Functions/         # Azure Function triggers
│       ├── Models/            # Data transfer objects
│       └── Services/          # Business logic
├── tests/
│   └── DevOpsCodeReviewer.Tests/
├── infra/                     # Bicep IaC templates
├── prompts/                   # LLM review prompts
├── docs/                      # Documentation
└── scripts/                   # Setup automation
```

## Development

### Build

```bash
dotnet build src/DevOpsCodeReviewer.Functions
```

### Test

```bash
dotnet test tests/DevOpsCodeReviewer.Tests
```

### Run Locally

```bash
# Start Azurite for local storage emulation
azurite --silent --location .azurite

# Start the function app
cd src/DevOpsCodeReviewer.Functions
func start
```

## Contributing

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community guidelines.

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This project is licensed under the MIT License - see [LICENSE](LICENSE) for details.
