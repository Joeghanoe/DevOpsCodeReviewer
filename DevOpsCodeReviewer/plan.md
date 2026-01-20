# Re-architect DevOpsCodeReviewer with Microsoft Agent Framework

## Goals
1. Replace the current single-LLM-call architecture with a multi-agent sequential pipeline that provides contextual code reviews
2. Restructure the project following SOLID principles for maintainability

## Current Problem
The v3 review shows generic comments like "add error handling" without knowing:
- If there's already a global error boundary in the app
- What error handling patterns exist elsewhere in the codebase
- How the changed files fit into the broader architecture

## Proposed Architecture: Sequential Agent Pipeline

```
[Service Bus Trigger]
        ↓
[1. Context Agent] - Fetches related files (imports, interfaces, parent classes)
        ↓
[2. Diff Analyzer Agent] - Parses diff, identifies concerns, structures input for review
        ↓
[3. Code Review Agent] - Reviews with full context (diff + related files + patterns)
        ↓
[Azure DevOps / Local File Output]
```

## Key Changes

### 1. Add Agent Framework NuGet Packages
**File: `DevOpsCodeReviewer.Functions/DevOpsCodeReviewer.Functions.csproj`**
```xml
<PackageReference Include="Microsoft.Agents.AI" Version="1.x.x" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.x.x" />
```
Note: Exact versions TBD - these are preview packages from Microsoft Agent Framework

### 2. Create Agent Definitions
**New file: `DevOpsCodeReviewer.Functions/Agents/ContextGatheringAgent.cs`**
- Receives: List of changed file paths
- Actions:
  - Parse imports/usings from each changed file
  - Fetch content of imported files from Azure DevOps
  - Identify interface implementations and fetch those
- Returns: `ContextResult` with related file contents and identified patterns

**New file: `DevOpsCodeReviewer.Functions/Agents/DiffAnalyzerAgent.cs`**
- Receives: Diff hunks + context from ContextGatheringAgent
- Actions:
  - Categorize changes (new feature, bug fix, refactor)
  - Identify areas of concern (error handling gaps, security-sensitive code)
  - Structure the analysis for the review agent
- Returns: `DiffAnalysis` with categorized concerns and structured context

**New file: `DevOpsCodeReviewer.Functions/Agents/CodeReviewAgent.cs`**
- Receives: `DiffAnalysis` + full context
- Actions:
  - Review code with awareness of existing patterns
  - Generate contextual suggestions (not generic "add error handling")
  - Reference related code when making suggestions
- Returns: `CodeReviewResponse` with comments

### 3. Create Workflow Orchestrator
**New file: `DevOpsCodeReviewer.Functions/Workflows/CodeReviewWorkflow.cs`**
```csharp
public class CodeReviewWorkflow
{
    public static Workflow Build(
        AIAgent contextAgent,
        AIAgent analyzerAgent,
        AIAgent reviewAgent)
    {
        return new WorkflowBuilder(contextAgent)
            .AddEdge(contextAgent, analyzerAgent)
            .AddEdge(analyzerAgent, reviewAgent)
            .Build();
    }
}
```

### 4. Refactor LlmService → AgentService
**Modify: `DevOpsCodeReviewer.Functions/Services/LlmService.cs`**
- Rename to `AgentService.cs` or create new service
- Replace single `ChatClient.CompleteChatAsync()` call with workflow execution
- Wire up the three agents in sequence

### 5. Update Program.cs for Agent Registration
**Modify: `DevOpsCodeReviewer.Functions/Program.cs`**
- Register agents with DI
- Configure workflow builder
- Keep existing Azure Functions infrastructure

### 6. Remove Markdown Prompts (Optional)
**Delete or archive: `prompts/*.md`**
- Agent instructions will be defined in code with the agent definitions
- More structured, type-safe approach

## SOLID Project Restructure

### Current Structure Issues
- All code in single Functions project (violation of SRP)
- Services tightly coupled to Azure Functions infrastructure
- No clear separation between domain logic and infrastructure

### New Multi-Project Structure
```
DevOpsCodeReviewer/
├── DevOpsCodeReviewer.sln
│
├── src/
│   ├── DevOpsCodeReviewer.Core/              # Domain logic (no external deps)
│   │   ├── Agents/
│   │   │   ├── IAgent.cs                     # Agent abstraction
│   │   │   ├── ContextGatheringAgent.cs
│   │   │   ├── DiffAnalyzerAgent.cs
│   │   │   └── CodeReviewAgent.cs
│   │   ├── Models/
│   │   │   ├── CodeReviewRequest.cs
│   │   │   ├── CodeReviewResponse.cs
│   │   │   ├── ReviewComment.cs
│   │   │   ├── ContextResult.cs
│   │   │   └── DiffAnalysis.cs
│   │   ├── Services/
│   │   │   ├── ICodeAnalysisService.cs
│   │   │   ├── CodeAnalysisService.cs
│   │   │   ├── IDiffService.cs
│   │   │   └── DiffService.cs
│   │   └── Workflows/
│   │       ├── ICodeReviewWorkflow.cs
│   │       └── CodeReviewWorkflow.cs
│   │
│   ├── DevOpsCodeReviewer.Infrastructure/    # External integrations
│   │   ├── AzureDevOps/
│   │   │   ├── IAzureDevOpsService.cs
│   │   │   ├── AzureDevOpsService.cs
│   │   │   └── AzureDevOpsOutputService.cs
│   │   ├── AI/
│   │   │   ├── IAgentFactory.cs
│   │   │   ├── AzureOpenAIAgentFactory.cs
│   │   │   └── AgentConfiguration.cs
│   │   └── Configuration/
│   │       ├── AzureDevOpsOptions.cs
│   │       ├── LlmOptions.cs
│   │       └── ServiceBusOptions.cs
│   │
│   └── DevOpsCodeReviewer.Functions/         # Azure Functions host (thin layer)
│       ├── Functions/
│       │   ├── WebhookHandler.cs
│       │   ├── CodeReviewProcessor.cs
│       │   └── HealthCheck.cs
│       ├── Program.cs                        # DI composition root
│       ├── host.json
│       └── local.settings.json
│
└── prompts/                                   # Shared prompt templates (optional)
```

### SOLID Principles Applied

**S - Single Responsibility**
- `Core`: Pure domain logic, no infrastructure concerns
- `Infrastructure`: External service integrations only
- `Functions`: HTTP/ServiceBus triggers only, delegates to Core

**O - Open/Closed**
- `IAgent` interface allows new agents without modifying existing code
- `IAgentFactory` allows swapping AI providers (Azure OpenAI, Anthropic, etc.)

**L - Liskov Substitution**
- All agents implement `IAgent` and are interchangeable in workflows
- Output services implement `IReviewOutputService`

**I - Interface Segregation**
- `IAzureDevOpsService` split into focused interfaces if needed:
  - `IPullRequestService` - PR operations
  - `IFileContentService` - File retrieval
  - `ICommentService` - Thread/comment operations

**D - Dependency Inversion**
- Core depends only on abstractions (interfaces)
- Infrastructure implements interfaces defined in Core
- Functions composes everything via DI

## Files to Create/Modify

### New Projects
| Project | Purpose |
|---------|---------|
| `DevOpsCodeReviewer.Core` | Domain logic, agents, workflows |
| `DevOpsCodeReviewer.Infrastructure` | Azure DevOps, Azure OpenAI integrations |

### Move Existing Files
| From | To |
|------|-----|
| `Functions/Models/*` | `Core/Models/*` |
| `Functions/Services/CodeAnalysisService.cs` | `Core/Services/` |
| `Functions/Services/DiffService.cs` | `Core/Services/` |
| `Functions/Services/AzureDevOpsService.cs` | `Infrastructure/AzureDevOps/` |
| `Functions/Services/LlmService.cs` | `Infrastructure/AI/` (refactor) |
| `Functions/Configuration/*` | `Infrastructure/Configuration/` |

### Create New Files
| File | Purpose |
|------|---------|
| `Core/Agents/IAgent.cs` | Agent interface |
| `Core/Agents/ContextGatheringAgent.cs` | Gathers related file context |
| `Core/Agents/DiffAnalyzerAgent.cs` | Analyzes diff structure |
| `Core/Agents/CodeReviewAgent.cs` | Performs contextual review |
| `Core/Workflows/ICodeReviewWorkflow.cs` | Workflow abstraction |
| `Core/Workflows/CodeReviewWorkflow.cs` | Sequential pipeline |
| `Infrastructure/AI/IAgentFactory.cs` | Creates configured agents |
| `Infrastructure/AI/AzureOpenAIAgentFactory.cs` | Azure OpenAI implementation |

## Implementation Phases

### Phase 1: Project Restructure
1. Create new solution structure with `src/` and `tests/` folders
2. Create `DevOpsCodeReviewer.Core` project
3. Create `DevOpsCodeReviewer.Infrastructure` project
4. Move existing files to appropriate projects
5. Update project references and namespaces
6. Verify build passes

### Phase 2: Add Agent Framework Infrastructure
1. Add Microsoft.Agents.AI NuGet packages to Infrastructure
2. Create `IAgent` interface in Core
3. Create `IAgentFactory` in Core, implement in Infrastructure
4. Update Program.cs for agent DI registration

### Phase 3: Implement Context Gathering Agent
1. Create ContextGatheringAgent with import parsing logic
2. Add `IFileContentService` interface for file retrieval
3. Implement related file discovery (imports, interfaces)

### Phase 4: Implement Diff Analyzer Agent
1. Create DiffAnalyzerAgent
2. Define DiffAnalysis model with categorized concerns
3. Wire up Context → Analyzer edge in workflow

### Phase 5: Implement Code Review Agent
1. Migrate review logic from current LlmService
2. Update prompts to be context-aware
3. Wire up full sequential pipeline

### Phase 6: Integration & Testing
3. Compare output quality vs v3 reviews - manual by user
4. Tune agent instructions

## Verification

1. **Build**: `dotnet build DevOpsCodeReviewer.sln`
4. **Local Run (manual)**:
   - `cd src/DevOpsCodeReviewer.Functions && func start`
   - Trigger webhook with test PR
   - Check `Reviews/` folder for output with contextual comments
5. **Quality Check (manual)**:
   - Review output should reference related files and patterns
   - Comments should be contextual, not generic "add error handling"
   - Example good output: "Consider using the existing `ErrorHandlerMiddleware` pattern from `/src/middleware/ErrorHandler.cs` instead of inline try-catch"
