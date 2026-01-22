# Configuration Reference

This document describes all configuration options for the Azure DevOps AI Code Reviewer.

## Configuration Sources

Configuration is loaded from (in order of precedence):

1. Environment variables
2. `local.settings.json` (local development only)
3. Azure Function App Settings (production)

## Quick Reference

| Section | Key Settings |
|---------|--------------|
| Azure DevOps | Organization URL, PAT, file filtering |
| LLM | Azure OpenAI endpoint, model, token limits |
| Service Bus | Connection string, queue name |
| Key Vault | Vault URL for secrets |
| Webhook | Authentication secret |

## Azure DevOps Settings

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `AzureDevOps__OrganizationUrl` | Your Azure DevOps organization URL | - | Yes |
| `AzureDevOps__Pat` | PAT token (local dev only) | - | No |
| `AzureDevOps__PatSecretName` | Key Vault secret name for PAT | `ado-pat-token` | No |
| `AzureDevOps__ApiVersion` | Azure DevOps REST API version | `7.1` | No |
| `AzureDevOps__TimeoutSeconds` | API request timeout | `30` | No |
| `AzureDevOps__MaxFilesPerReview` | Maximum files to include | `50` | No |
| `AzureDevOps__MaxFileSizeBytes` | Maximum file size (bytes) | `102400` | No |
| `AzureDevOps__BotSignature` | Signature for duplicate detection | `<!-- DevOpsCodeReviewer -->` | No |

### Included Extensions

Default extensions reviewed:
```
.cs, .ts, .tsx, .js, .jsx, .py, .java, .go, .rs, .bicep, .yaml, .yml, .json
```

### Excluded Patterns

Default excluded patterns:
```
package-lock.json, yarn.lock, pnpm-lock.yaml,
*.min.js, *.min.css, *.designer.cs, *.generated.cs, *.g.cs
```

## LLM Settings

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `Llm__Endpoint` | Azure OpenAI endpoint URL | - | Yes |
| `Llm__DeploymentName` | Model deployment name | `gpt-4o` | No |
| `Llm__ApiKeySecretName` | Key Vault secret name | `foundry-api-key` | No |
| `Llm__MaxTokens` | Max response tokens | `4096` | No |
| `Llm__Temperature` | Response temperature (0-1) | `0.3` | No |
| `Llm__MaxFilesPerRequest` | Files per LLM request | `10` | No |
| `Llm__MaxLinesPerRequest` | Lines per LLM request | `2000` | No |
| `Llm__MaxRetries` | API retry attempts | `3` | No |
| `Llm__RetryDelayMs` | Initial retry delay | `1000` | No |
| `Llm__TimeoutSeconds` | Request timeout | `120` | No |
| `Llm__MinSeverityLevel` | Minimum severity to report | `2` | No |
| `Llm__UseStructuredOutput` | Use JSON mode | `true` | No |

### Severity Levels

| Level | Name | Description |
|-------|------|-------------|
| 1 | Info | Informational only |
| 2 | Minor | Should fix, not blocking |
| 3 | Major | Fix before merge |
| 4 | Critical | Must fix before merge |
| 5 | Blocker | Cannot merge |

### Review Categories

Comments are tagged with categories based on what triggered them:

| Category | Source | Description |
|----------|--------|-------------|
| Bug | Language prompts | Potential bugs and logic errors |
| Security | Security principles | Security vulnerabilities |
| Performance | Language prompts | Performance issues |
| Architecture | Architectural principles | Design and structure issues |
| CloudCompliance | Cloud principles | Cloud-readiness issues |
| Style | Language prompts | Code style and conventions |

## Service Bus Settings

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `ServiceBus__ConnectionString` | Service Bus connection string | - | Yes |
| `ServiceBus__QueueName` | Queue name | `codereview-requests` | No |

## Key Vault Settings

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `KeyVault__VaultUrl` | Key Vault URL | - | Yes (prod) |

## Webhook Settings

| Setting | Description | Default | Required |
|---------|-------------|---------|----------|
| `Webhook__Secret` | Webhook authentication secret | - | Recommended |

## Example Configuration

### Local Development (local.settings.json)

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",

    "ServiceBus__ConnectionString": "Endpoint=sb://...",
    "ServiceBus__QueueName": "codereview-requests",

    "AzureDevOps__OrganizationUrl": "https://dev.azure.com/myorg",
    "AzureDevOps__Pat": "your-pat-here",

    "Llm__Endpoint": "https://myopenai.openai.azure.com/",
    "Llm__DeploymentName": "gpt-4o",

    "Webhook__Secret": "my-secret-123"
  }
}
```

### Production (Azure Function App Settings)

```bash
# Set via Azure CLI
az functionapp config appsettings set \
  --name func-myapp \
  --resource-group rg-myapp \
  --settings \
    "AzureDevOps__OrganizationUrl=https://dev.azure.com/myorg" \
    "AzureDevOps__PatSecretName=ado-pat-token" \
    "Llm__Endpoint=https://myopenai.openai.azure.com/" \
    "Llm__DeploymentName=gpt-4o" \
    "Llm__ApiKeySecretName=foundry-api-key" \
    "KeyVault__VaultUrl=https://kv-myapp.vault.azure.net/"
```

## Tuning Recommendations

### For Large PRs

If your PRs often have many files:

```
Llm__MaxFilesPerRequest=15
Llm__MaxLinesPerRequest=3000
Llm__MaxTokens=8192
```

### For Faster Reviews

If speed is more important than thoroughness:

```
Llm__Temperature=0.1
Llm__MinSeverityLevel=3
AzureDevOps__MaxFilesPerReview=30
```

### For Comprehensive Reviews

If you want more detailed feedback:

```
Llm__Temperature=0.5
Llm__MinSeverityLevel=1
Llm__MaxTokens=8192
```

## Environment Variables

All settings can be configured via environment variables using the double-underscore format:

```bash
export AzureDevOps__OrganizationUrl="https://dev.azure.com/myorg"
export Llm__DeploymentName="gpt-4o"
```

## Risk Assessment Output

The Overview Agent produces a risk assessment for each PR. The risk level is derived from review comments:

| Risk Level | Condition |
|------------|-----------|
| `safe` | No comments generated |
| `low-risk` | Only Info or Minor comments |
| `medium-risk` | Any Critical or Major comments |
| `high-risk` | 2+ Critical comments |
| `critical-risk` | Any Blocker comments |

The Overview also includes:
- **Confidence Score** (1-5): How confident the AI is in its review
- **Important Files**: Files ranked by impact score (1-5)
- **Key Changes**: Summary of what changed and why it matters

## Prompt Configuration

Prompts are loaded from the `prompts/` directory:

| File | Purpose |
|------|---------|
| `generic-code-review.md` | Default prompt for all languages |
| `csharp-code-review.md` | C#/.NET specific patterns |
| `typescript-code-review.md` | TypeScript/JavaScript/React patterns |
| `overview-generation.md` | Overview Agent instructions |
| `principles/architectural-principles.md` | Architecture rules |
| `principles/cloud-principles.md` | Cloud compliance rules |
| `principles/security-principles.md` | Security rules |

### Language Prompt Selection

The system automatically selects prompts based on file extension:

| Extensions | Prompt Used |
|------------|-------------|
| `.cs` | `csharp-code-review.md` |
| `.ts`, `.tsx`, `.js`, `.jsx` | `typescript-code-review.md` |
| All others | `generic-code-review.md` |
