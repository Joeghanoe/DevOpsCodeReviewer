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
│  PR Comments    │     │  (Processor)     │     │  Trigger        │
└─────────────────┘     └────────┬─────────┘     └─────────────────┘
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

Processes queued review requests and generates AI-powered code review comments.

**Responsibilities**:
- Get PR iterations from Azure DevOps API
- Fetch changed files content
- Filter and prioritize files for review
- Chunk large PRs for LLM processing
- Call Azure OpenAI for code review
- Filter duplicate comments
- Post comments to PR

**Design Decisions**:
- Uses Service Bus for reliable, at-least-once delivery
- 5-minute lock duration for long LLM calls
- Max 3 delivery attempts before dead-lettering
- Chunks files to stay within LLM context limits

### 3. Dead Letter Handler

**File**: `Functions/DeadLetterHandler.cs`

Handles messages that failed processing after max retries.

**Responsibilities**:
- Log failure details
- Clean up dead letter queue
- (Future) Send alerts or store for manual review

### 4. Health Check

**File**: `Functions/HealthCheck.cs`

Provides endpoints for monitoring and load balancer probes.

**Endpoints**:
- `/api/health` - Full health check with dependencies
- `/api/health/live` - Liveness probe (is the function running?)
- `/api/health/ready` - Readiness probe (are dependencies available?)

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

### LLM Service

**File**: `Services/LlmService.cs`

Handles communication with Azure OpenAI.

**Key Methods**:
- `ReviewCodeAsync` - Review a batch of files
- `ReviewCodeInChunksAsync` - Review in multiple batches

**Features**:
- Structured JSON output with schema validation
- Retry with exponential backoff
- Token usage tracking

## Data Flow

### 1. PR Created/Updated

```
ADO → Webhook → Validate → Parse → Queue Message → Return 202
```

### 2. Review Processing

```
Dequeue → Get Iterations → Get Files → Filter Files → Chunk Files
    → LLM Review → Parse Response → Filter Duplicates → Post Comments
```

### 3. Comment Posting

```
For each comment:
    Create thread context → Format message → POST to ADO API
```

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
