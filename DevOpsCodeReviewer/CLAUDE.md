# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build solution
dotnet build DevOpsPipelineAgent.sln

# Run Azure Functions locally
cd src/DevOpsCodeReviewer.Functions && func start
```

## Architecture

This is an Azure Functions application that provides automated AI-powered code review for Azure DevOps pull requests using a multi-agent pipeline.

### Project Structure

```
src/
├── DevOpsCodeReviewer.Core/           # Domain logic (no external dependencies)
├── DevOpsCodeReviewer.Infrastructure/ # External integrations (Azure, OpenAI, DevOps)
└── DevOpsCodeReviewer.Functions/      # Azure Functions host (thin orchestration layer)
```

Dependencies flow outward: Functions → Infrastructure → Core. Core has zero infrastructure dependencies.

### Request Flow

1. **WebhookHandler** (`Functions/WebhookHandler.cs`) - HTTP trigger receives Azure DevOps PR webhook events (`git.pullrequest.created`, `git.pullrequest.updated`), validates the request, and queues a `CodeReviewRequest` to Service Bus

2. **CodeReviewProcessor** (`Functions/CodeReviewProcessor.cs`) - Service Bus trigger processes queued review requests:
   - Fetches PR iterations and changed files via `AzureDevOpsService`
   - Filters and prioritizes files via `CodeAnalysisService`
   - Executes three-agent pipeline: ContextGatheringAgent → DiffAnalyzerAgent → CodeReviewAgent
   - Posts review comments back to the PR via `IReviewOutputService`

### Key Services

**Core Layer:**
- **CodeAnalysisService** - File filtering, chunking, duplicate detection, and prioritization logic
- **DiffService** - Computes unified diff hunks between original and modified files

**Infrastructure Layer:**
- **AzureDevOpsService** - REST API client for Azure DevOps (PR details, iterations, file content, comment threads)
- **AzureOpenAIAgentFactory** - Creates configured agents for the review pipeline
- **PromptService** - Loads language-specific prompt templates from `prompts/` directory
- **CodeReviewWorkflow** - Orchestrates the sequential agent pipeline

**Agents (in `Infrastructure/AI/Agents/`):**
- **ContextGatheringAgent** - Parses imports, fetches related files, identifies patterns
- **DiffAnalyzerAgent** - Categorizes changes and identifies concerns
- **CodeReviewAgent** - Performs contextual review with full context

### Output Services

In DEBUG builds, `LocalFileOutputService` writes reviews to local files in `Reviews/`. In RELEASE builds, `AzureDevOpsOutputService` posts comments directly to the PR.

### Configuration Sections

Configuration is bound via `IOptions<T>` pattern:
- `AzureDevOps` - Organization URL, PAT, included extensions, excluded patterns
- `Llm` - Azure OpenAI endpoint, deployment name, token limits, temperature
- `ServiceBus` - Connection string and queue name
- `Webhook` - Secret for webhook validation
- `ReviewOutput` - Output configuration
- `KeyVault` - Vault URL for secret resolution

Secrets can be provided directly in config (local dev) or via Key Vault secret names (production).

## Prompt Templates

Located in `prompts/` directory:
- `generic-code-review.md` - Default for all languages
- `csharp-code-review.md` - C#/.NET specific patterns
- `typescript-code-review.md` - TypeScript/JavaScript/React patterns

Prompt selection: `.cs` → csharp, `.ts`/`.tsx`/`.js`/`.jsx` → typescript, others → generic.

The LLM response follows strict JSON schemas defined in `Infrastructure/AI/Models/` with categories (Bug, Security, Performance, etc.) and severity levels (Info, Minor, Major, Critical, Blocker).

## Adding New Language Prompts

1. Create `prompts/{language}-code-review.md` following existing structure
2. Update `PromptService.cs` to map file extensions to the new prompt
