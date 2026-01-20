# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build solution
dotnet build DevOpsPipelineAgent.sln

# Run Azure Functions locally
cd DevOpsCodeReviewer.Functions && func start
```

## Architecture

This is an Azure Functions application that provides automated AI-powered code review for Azure DevOps pull requests.

### Request Flow

1. **WebhookHandler** (`Functions/WebhookHandler.cs`) - HTTP trigger receives Azure DevOps PR webhook events (`git.pullrequest.created`, `git.pullrequest.updated`), validates the request, and queues a `CodeReviewRequest` to Service Bus

2. **CodeReviewProcessor** (`Functions/CodeReviewProcessor.cs`) - Service Bus trigger processes queued review requests:
   - Fetches PR iterations and changed files via `AzureDevOpsService`
   - Filters and prioritizes files via `CodeAnalysisService`
   - Sends code to LLM for review via `LlmService`
   - Posts review comments back to the PR via `IReviewOutputService`

### Key Services

- **AzureDevOpsService** - REST API client for Azure DevOps (PR details, iterations, file content, comment threads)
- **LlmService** - Azure OpenAI integration using structured JSON output for review responses
- **CodeAnalysisService** - File filtering, chunking, duplicate detection, and prioritization logic
- **PromptService** - Loads language-specific prompt templates from `prompts/` directory
- **DiffService** - Computes unified diff hunks between original and modified files

### Output Services

In DEBUG builds, `LocalFileOutputService` writes reviews to local files in `Reviews/`. In RELEASE builds, `AzureDevOpsOutputService` posts comments directly to the PR.

### Configuration Sections

Configuration is bound via `IOptions<T>` pattern:
- `AzureDevOps` - Organization URL, PAT, included extensions, excluded patterns
- `Llm` - Azure OpenAI endpoint, deployment name, token limits, temperature
- `ServiceBus` - Connection string and queue name
- `Webhook` - Secret for webhook validation
- `ReviewOutput` - Output configuration

Secrets can be provided directly in config (local dev) or via Key Vault secret names (production).

## Prompt Templates

Located in `prompts/` directory:
- `generic-code-review.md` - Default for all languages
- `csharp-code-review.md` - C#/.NET specific patterns
- `typescript-code-review.md` - TypeScript/JavaScript/React patterns

The LLM response follows a strict JSON schema defined in `LlmService.GetResponseSchema()` with categories (Bug, Security, Performance, etc.) and severity levels (Info, Minor, Major, Critical, Blocker).

## Testing

- Unit tests use xUnit, FluentAssertions, and Moq
- Test fixtures in `DevOpsCodeReviewer.Tests/Fixtures/` are excluded from compilation (content files only)
- Global usings for test namespaces are configured in the test project
