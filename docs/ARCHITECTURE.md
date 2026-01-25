# Architecture

This document describes the system architecture of the Azure DevOps AI Code Reviewer.

## System Overview

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Azure DevOps   │────►│  Azure Function  │────►│  Service Bus    │
│  Service Hook   │     │  (Webhook)       │     │  Queue          │
└─────────────────┘     └──────────────────┘     └────────┬────────┘
                                                          │
                                                          ▼
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Azure DevOps   │◄────│  Azure Function  │◄────│  Service Bus    │
│  PR Comments +  │     │  (Processor)     │     │  Trigger        │
│  Risk Summary   │     └────────┬─────────┘     └─────────────────┘
└─────────────────┘              │
                                 ▼
                        ┌──────────────────────────────────┐
                        │       4-Agent AI Pipeline        │
                        │  ──────────────────────────────  │
                        │  1. ContextGatheringAgent        │
                        │  2. DiffAnalyzerAgent            │
                        │  3. CodeReviewAgent              │
                        │  4. OverviewAgent                │
                        └────────────────┬─────────────────┘
                                         │
                                         ▼
                        ┌──────────────────┐
                        │  Azure OpenAI    │
                        │  (Foundry)       │
                        └──────────────────┘
```

## Components

### 1. Webhook Handler (HTTP Trigger)

**File**: `Functions/WebhookHandler.cs`

Receives webhook notifications from Azure DevOps when PRs are created or updated.

**Responsibilities**:
- Validate webhook secret
- Parse PR event payload
- Filter supported events (`git.pullrequest.created`, `git.pullrequest.updated`)
- Skip draft and non-active PRs
- Queue review request to Service Bus
- Return 202 Accepted immediately

**Design Decisions**:
- Uses query parameter for webhook secret (simpler than custom headers in ADO)
- Returns quickly to avoid webhook timeout
- Generates correlation ID for distributed tracing

### 2. Code Review Processor (Service Bus Trigger)

**File**: `Functions/CodeReviewProcessor.cs`

Processes queued review requests and orchestrates the multi-agent AI pipeline.

**Responsibilities**:
- Get PR iterations from Azure DevOps API
- Fetch changed files content
- Filter and prioritize files for review
- Execute 4-agent review pipeline (see below)
- Filter duplicate comments
- Post comments and overview to PR

**Design Decisions**:
- Uses Service Bus for reliable, at-least-once delivery
- 5-minute lock duration for long LLM calls
- Max 3 delivery attempts before dead-lettering
- Chunks files to stay within LLM context limits

### 3. Health Check

**File**: `Functions/HealthCheck.cs`

Provides endpoints for monitoring and load balancer probes.

**Endpoints**:
- `/api/health` - Full health check with dependencies
- `/api/health/live` - Liveness probe (is the function running?)
- `/api/health/ready` - Readiness probe (are dependencies available?)

## Multi-Agent Pipeline

The code review processor orchestrates a sequential 4-agent pipeline. Each agent builds on the output of the previous agent.

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Code Review Pipeline                         │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐ │
│  │ Changed Files   │───►│ 1. Context      │───►│ ContextResult   │ │
│  │ (from PR)       │    │    Gathering    │    │ - Related files │ │
│  └─────────────────┘    │    Agent        │    │ - Patterns      │ │
│                         └─────────────────┘    └────────┬────────┘ │
│                                                         │          │
│                                                         ▼          │
│                         ┌─────────────────┐    ┌─────────────────┐ │
│                         │ 2. Diff         │───►│ DiffAnalysis    │ │
│                         │    Analyzer     │    │ - Change type   │ │
│                         │    Agent        │    │ - Concerns      │ │
│                         └─────────────────┘    └────────┬────────┘ │
│                                                         │          │
│                                                         ▼          │
│                         ┌─────────────────┐    ┌─────────────────┐ │
│                         │ 3. Code         │───►│ ReviewComments  │ │
│                         │    Review       │    │ - Line comments │ │
│                         │    Agent        │    │ - Severity      │ │
│                         └─────────────────┘    └────────┬────────┘ │
│                                                         │          │
│                                                         ▼          │
│                         ┌─────────────────┐    ┌─────────────────┐ │
│                         │ 4. Overview     │───►│ ReviewOverview  │ │
│                         │    Agent        │    │ - Summary       │ │
│                         │                 │    │ - Risk score    │ │
│                         └─────────────────┘    └─────────────────┘ │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Agent 1: Context Gathering Agent

**File**: `Infrastructure/AI/Agents/ContextGatheringAgent.cs`

Analyzes the changed files to build understanding of the codebase context.

**Input**: List of changed files with content
**Output**: `ContextResult`
- Related files discovered
- Identified patterns (DI, repository pattern, etc.)
- Import/dependency analysis

**Purpose**: Provides context so subsequent agents understand how the changes fit into the broader codebase.

### Agent 2: Diff Analyzer Agent

**File**: `Infrastructure/AI/Agents/DiffAnalyzerAgent.cs`

Categorizes the type of changes and identifies areas of concern.

**Input**: `ContextResult` from Agent 1
**Output**: `DiffAnalysis`
- Change category: `feature`, `bugfix`, `refactor`, `config`, `test`, `docs`, etc.
- Change summary
- Areas of concern (potential issues to investigate)

**Purpose**: Helps the review agent focus on the most relevant aspects based on change type.

### Agent 3: Code Review Agent

**File**: `Infrastructure/AI/Agents/CodeReviewAgent.cs`

Performs detailed code review using language-specific prompts and principles.

**Input**: `DiffAnalysis` from Agent 2, plus principles prompts
**Output**: `CodeReviewResult`
- List of `ReviewComment` objects with:
  - File path and line number
  - Category (Bug, Security, Performance, Architecture, CloudCompliance, Style)
  - Severity (Info=1, Minor=2, Major=3, Critical=4, Blocker=5)
  - Message with actionable feedback

**Principles Applied**:
- Architectural principles (API versioning, service independence, etc.)
- Cloud principles (statelessness, no PII in logs, testability, etc.)
- Security principles (least privilege, secure defaults, fail securely, etc.)

### Agent 4: Overview Agent

**File**: `Infrastructure/AI/Agents/OverviewAgent.cs`

Synthesizes all findings into an executive summary with risk assessment.

**Input**: `DiffAnalysis` + `ReviewComments`
**Output**: `ReviewOverview`
- Executive summary (2-4 sentences)
- Key changes with rationale
- Important files ranked by impact (1-5 score)
- Confidence score (1-5)
- Risk assessment: `safe`, `low-risk`, `medium-risk`, `high-risk`, `critical-risk`

**Risk Assessment Logic**:
| Condition | Risk Level |
|-----------|------------|
| Any Blocker comments | `critical-risk` |
| 2+ Critical comments | `high-risk` |
| Any Critical or Major | `medium-risk` |
| Only Minor/Info | `low-risk` |
| No comments | `safe` |

## Principles System

The Code Review Agent applies configurable principles during review. Principles are defined in markdown files under `prompts/principles/`.

### Available Principles

| File | Category | Focus Areas |
|------|----------|-------------|
| `architectural-principles.md` | Architecture | Service independence, API versioning, async patterns, single responsibility |
| `cloud-principles.md` | CloudCompliance | No PII in logs, stateless services, DRY, testability, immutability |
| `security-principles.md` | Security | Least privilege, secure defaults, fail securely, defense in depth |

### How Principles Work

1. Principles are loaded and injected into the Code Review Agent's prompt
2. Each principle defines:
   - **What to detect**: Code patterns that violate the principle
   - **Severity**: How severe a violation should be rated
   - **Examples**: Bad and good code examples
3. Violations are flagged with the appropriate category (Architecture, CloudCompliance, Security)

### Adding Custom Principles

Create a new markdown file in `prompts/principles/` following this structure:

```markdown
# Your Principle Name

## Principle Title
Description of what to check for.

**Detect:**
- Pattern 1 to look for
- Pattern 2 to look for

**Severity:** Major
**Impact Example:** "Why this matters..."

```code
// BAD: Example of violation
// GOOD: Example of correct code
```
```

## Services

### Azure DevOps Service

**File**: `Services/AzureDevOpsService.cs`

Handles all communication with Azure DevOps REST API.

**Key Methods**:
- `GetPullRequestAsync` - Get PR metadata
- `GetPullRequestIterationsAsync` - Get PR iterations
- `GetIterationChangesAsync` - Get changed files list
- `GetFileContentAsync` - Get file content at commit
- `GetPullRequestThreadsAsync` - Get existing comments
- `CreateCommentThreadAsync` - Post new comment

### Code Analysis Service

**File**: `Services/CodeAnalysisService.cs`

Handles file filtering, chunking, and duplicate detection.

**Key Methods**:
- `FilterReviewableFiles` - Filter by extension and size
- `ChunkFilesForReview` - Split into LLM-sized batches
- `FilterDuplicateComments` - Prevent duplicate comments
- `PrioritizeFiles` - Order files for review priority

### Code Review Workflow

**File**: `Infrastructure/Workflows/CodeReviewWorkflow.cs`

Orchestrates the sequential 4-agent pipeline execution.

**Key Method**:
- `ExecuteAsync` - Runs all four agents in sequence, passing context between them

**Features**:
- Execution timing for each agent (logged for performance analysis)
- Correlation ID propagation for distributed tracing
- Graceful error handling with detailed logging

### Agent Factory

**File**: `Infrastructure/AI/AzureOpenAIAgentFactory.cs`

Creates configured agent instances using Microsoft Agents Framework.

**Key Methods**:
- `CreateContextGatheringAgent` - Creates context analysis agent
- `CreateDiffAnalyzerAgent` - Creates change categorization agent
- `CreateCodeReviewAgent` - Creates review agent with principles
- `CreateOverviewAgent` - Creates summary/risk assessment agent

### Prompt Service

**File**: `Infrastructure/AI/PromptService.cs`

Loads language-specific prompts and principles from the `prompts/` directory.

**Key Features**:
- Language-specific prompt selection (`.cs` → csharp, `.ts`/`.tsx` → typescript)
- Principles loading and injection
- Template caching for performance

## Data Flow

### 1. PR Created/Updated

```
ADO → Webhook → Validate → Parse → Queue Message → Return 202
```

### 2. Review Processing

```
Dequeue → Get Iterations → Get Files → Filter Files
    ↓
┌─────────────────────────────────────────────────┐
│              4-Agent Pipeline                    │
├─────────────────────────────────────────────────┤
│ 1. Context Agent    → Gather patterns & context │
│ 2. Diff Agent       → Categorize changes        │
│ 3. Review Agent     → Generate comments         │
│ 4. Overview Agent   → Synthesize & score risk   │
└─────────────────────────────────────────────────┘
    ↓
Filter Duplicates → Post Comments + Overview
```

### 3. Comment Posting

```
For line comments:
    Create thread context → Format message → POST to ADO API

For overview:
    Format summary → Post as PR-level comment
```

### 4. Output Structure

The final output includes:
- **Line-level comments**: Posted on specific lines in the PR diff
- **Overview comment**: Executive summary posted as a general PR comment
- **PR status**: Updated to reflect review completion

## Security

### Authentication

- **Azure DevOps**: PAT token stored in Key Vault
- **Azure OpenAI**: API key stored in Key Vault
- **Webhook**: Secret in query parameter or Basic Auth header
- **Function App**: System-assigned managed identity

### Authorization

- Function App identity has Key Vault Secrets User role
- Function App identity has Service Bus Data Sender/Receiver roles
- PAT requires Code Read/Write and PR Threads permissions

### Data Protection

- All traffic over HTTPS
- No code stored persistently
- Secrets in Key Vault with RBAC

## Scalability

### Consumption Plan (Default)

- Auto-scales from 0 to 200 instances
- Pay per execution
- Cold start possible

### Premium Plan (Production)

- Pre-warmed instances
- VNET integration
- Higher limits

### Queue Configuration

- Lock duration: 5 minutes (for LLM latency)
- Max delivery count: 3
- Duplicate detection: 10 minutes
- Dead letter on expiration

## Error Handling

### Transient Errors

- HTTP retries with Polly
- LLM retries with exponential backoff
- Service Bus automatic retries

### Permanent Errors

- Dead letter queue for failed messages
- Application Insights logging
- Correlation ID for tracing

## Monitoring

### Application Insights

- Request logging
- Dependency tracking
- Exception logging
- Custom metrics (tokens used, comments posted)

### Health Checks

- Kubernetes-compatible endpoints
- Dependency verification
- Configuration validation

## Cost Optimization

### Minimize LLM Costs

- Filter files before sending to LLM
- Chunk efficiently
- Cache prompt templates
- Use appropriate model (gpt-4o-mini for simple reviews)

### Minimize Function Costs

- Quick webhook response
- Efficient file filtering
- Batch operations where possible
