# Azure DevOps AI Code Reviewer

An open-source, production-ready Azure DevOps Pull Request code review bot powered by Azure OpenAI. Uses a **multi-agent AI pipeline** to provide intelligent, context-aware code reviews with risk assessment and executive summaries.

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
                      Dead Letter           4-Agent Pipeline
                       Handler              ────────────────
                                            │ 1. Context    │
                                            │ 2. Diff       │
                                            │ 3. Review     │
                                            │ 4. Overview   │
                                            └───────┬───────┘
                                                    ▼
                                             PR Comments +
                                             Risk Summary
                                            (ADO REST API)
```

## Features

- **Multi-Agent AI Pipeline**: Four specialized agents work together for comprehensive analysis
  - **Context Agent**: Gathers codebase context, identifies patterns and related files
  - **Diff Analyzer**: Categorizes changes and identifies areas of concern
  - **Code Review Agent**: Performs detailed review with severity-scored comments
  - **Overview Agent**: Synthesizes findings into executive summary with risk assessment
- **PR Overview & Risk Assessment**: Executive summary with confidence scoring (1-5) and risk levels (safe → critical-risk)
- **Principles-Based Review**: Configurable architectural, cloud, and security principles enforcement
- **Multi-Language Support**: C#, TypeScript, JavaScript, Python, Java, Go, Rust, and more
- **Smart Filtering**: Skips binary files, lock files, and generated code
- **Duplicate Detection**: Avoids posting redundant comments
- **Large PR Handling**: Intelligent chunking for PRs with many changes
- **Dead Letter Handling**: Graceful handling of processing failures
- **Infrastructure as Code**: Full Bicep templates for Azure deployment

## Review Output

Each PR review generates:

### Line-Level Comments
Individual code review comments posted directly on the relevant lines with:
- **Severity**: Info (1) → Blocker (5)
- **Category**: Bug, Security, Performance, Architecture, CloudCompliance, Style, etc.
- **Actionable feedback** with suggested fixes

### PR Overview (Summary Comment)
An executive summary posted as a PR comment containing:
- **Summary**: 2-4 sentence overview of the PR and findings
- **Key Changes**: What changed and why it matters
- **Important Files**: Files ranked by impact score (1-5)
- **Risk Assessment**: `safe` | `low-risk` | `medium-risk` | `high-risk` | `critical-risk`
- **Confidence Score**: 1-5 rating of review completeness

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
├── DevOpsCodeReviewer/
│   ├── src/
│   │   ├── DevOpsCodeReviewer.Core/           # Domain logic (no external dependencies)
│   │   │   ├── Agents/                        # Agent interfaces
│   │   │   ├── Models/                        # Domain models
│   │   │   ├── Services/                      # Core business logic
│   │   │   └── Workflows/                     # Workflow interfaces
│   │   ├── DevOpsCodeReviewer.Infrastructure/ # External integrations
│   │   │   ├── AI/                            # Azure OpenAI integration
│   │   │   │   ├── Agents/                    # Agent implementations
│   │   │   │   └── Models/                    # LLM response models
│   │   │   ├── AzureDevOps/                   # Azure DevOps API client
│   │   │   ├── Output/                        # PR comment posting
│   │   │   └── Workflows/                     # Pipeline orchestration
│   │   └── DevOpsCodeReviewer.Functions/      # Azure Functions host
│   │       ├── Configuration/                 # Strongly-typed settings
│   │       └── Functions/                     # Function triggers
│   ├── prompts/                               # LLM review prompts
│   │   └── principles/                        # Architectural/security principles
│   └── tests/
├── infra/                                     # Bicep IaC templates
├── docs/                                      # Documentation
└── scripts/                                   # Setup automation
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
