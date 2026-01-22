# Customization Guide

This guide explains how to customize the AI code reviewer for your team's needs.

## Overview

The review behavior is controlled through:
1. **Language-specific prompts** - Define review focus for each programming language
2. **Principles** - Configurable rules for architecture, cloud, and security compliance
3. **Configuration settings** - Tune severity thresholds, file filtering, and more

## Customizing Review Prompts

The AI's behavior is primarily controlled through prompts in the `prompts/` directory.

### Prompt Structure

Each prompt file contains:

1. **Role definition** - What expertise the AI should apply
2. **Focus areas** - Categories of issues to look for
3. **Severity definitions** - What each level means
4. **Anti-patterns** - Examples of bad code to flag
5. **Good patterns** - Examples of correct code
6. **Output format** - JSON structure for responses

### Adding Language-Specific Prompts

1. Create a new file: `prompts/{language}-code-review.md`

2. Follow the structure of existing prompts:
   ```markdown
   # {Language} Code Review Prompt

   You are an expert {language} code reviewer...

   ## {Language}-Specific Focus Areas

   ### 1. {Category}
   - Specific things to look for
   - Anti-patterns

   ## Common Anti-Patterns

   ```{language}
   // BAD: Example of bad code
   ```

   ## Good Patterns

   ```{language}
   // GOOD: Example of good code
   ```
   ```

3. Update `LlmService.cs` to use the new prompt (optional - currently uses a built-in prompt)

### Customizing Severity Levels

In your prompt, redefine what each level means for your team:

```markdown
## Severity Definitions

| Level | Name | Description |
|-------|------|-------------|
| 1 | Info | Nice to know, optional fix |
| 2 | Minor | Code smell, fix when convenient |
| 3 | Major | Bug risk, should fix before merge |
| 4 | Critical | High bug risk, must fix |
| 5 | Blocker | Security issue or guaranteed bug |
```

### Adding Company-Specific Rules

Add a section for internal standards:

```markdown
## Company Standards

### Logging
- All public service methods must log entry and exit
- Error logs must include correlation ID
- Never log PII

### Error Handling
- Use Result<T> pattern for expected failures
- Only catch specific exception types
- Always include error context

### Naming Conventions
- Services: `{Domain}Service`
- Repositories: `{Entity}Repository`
- DTOs: `{Name}Request`, `{Name}Response`
```

## Customizing File Filtering

### Include Additional Extensions

In `appsettings` or environment:

```json
{
  "AzureDevOps": {
    "IncludedExtensions": [
      ".cs", ".ts", ".tsx", ".js", ".jsx", ".py",
      ".java", ".go", ".rs", ".bicep", ".yaml", ".yml",
      ".json", ".vue", ".svelte", ".kt"
    ]
  }
}
```

### Exclude Patterns

```json
{
  "AzureDevOps": {
    "ExcludedPatterns": [
      "package-lock.json",
      "yarn.lock",
      "*.min.js",
      "*.designer.cs",
      "*.generated.cs",
      "*.g.cs",
      "wwwroot/lib/*",
      "vendor/*",
      "*.snapshot"
    ]
  }
}
```

### Adjust File Size Limit

```json
{
  "AzureDevOps": {
    "MaxFileSizeBytes": 204800
  }
}
```

## Customizing Review Principles

The reviewer applies configurable principles during code review. These are defined in `prompts/principles/` and enforce architectural, cloud, and security standards.

### Built-in Principles

| Principle File | Category | What It Checks |
|----------------|----------|----------------|
| `architectural-principles.md` | Architecture | Service independence, API versioning, async/await patterns, single responsibility |
| `cloud-principles.md` | CloudCompliance | No PII in logs, stateless services, DRY, testability, immutability |
| `security-principles.md` | Security | Least privilege, secure defaults, fail securely, defense in depth |

### Modifying Existing Principles

Edit the principle files in `prompts/principles/` to adjust:

1. **Detection patterns** - What code patterns to flag
2. **Severity levels** - How severe a violation is (Info, Minor, Major, Critical, Blocker)
3. **Examples** - Bad/good code examples that guide the AI

Example: Make unversioned APIs a blocker instead of critical:

```markdown
## A.9: Interface Versioning

**Detect:**
- API routes without version prefix
- Controllers without `[ApiVersion]` attribute

**Severity:** Blocker  <!-- Changed from Critical -->
```

### Creating Custom Principles

Create a new file in `prompts/principles/` following this template:

```markdown
# Your Company Principles

## Principle: Logging Standard

All service methods must include structured logging.

**Detect:**
- Public service methods without logging statements
- Logging without correlation ID
- Using string interpolation instead of structured logging

**Severity:** Major
**Impact Example:** "Without structured logging, debugging production issues becomes nearly impossible."

```csharp
// BAD: No logging or unstructured
public async Task<Order> ProcessOrderAsync(int orderId)
{
    Console.WriteLine($"Processing order {orderId}");  // Bad
    return await _repository.GetOrderAsync(orderId);
}

// GOOD: Structured logging with correlation
public async Task<Order> ProcessOrderAsync(int orderId)
{
    _logger.LogInformation("Processing order {OrderId}", orderId);
    var order = await _repository.GetOrderAsync(orderId);
    _logger.LogInformation("Order {OrderId} processed successfully", orderId);
    return order;
}
```
```

### Disabling Principles

To disable a principle category entirely, remove or rename the corresponding file in `prompts/principles/`.

To disable specific rules within a principle, comment them out or remove them from the markdown file.

### Principle Categories in Comments

Violations appear in PR comments with these categories:
- **Architecture** - From `architectural-principles.md`
- **CloudCompliance** - From `cloud-principles.md`
- **Security** - From `security-principles.md`
- **Bug, Performance, Style** - From language-specific prompts

## Customizing Review Behavior

### Adjust Severity Threshold

Only report major issues and above:

```json
{
  "Llm": {
    "MinSeverityLevel": 3
  }
}
```

### Adjust Review Thoroughness

For more thorough reviews (slower, more expensive):

```json
{
  "Llm": {
    "Temperature": 0.5,
    "MaxFilesPerRequest": 5,
    "MaxLinesPerRequest": 1500
  }
}
```

For faster reviews (less thorough):

```json
{
  "Llm": {
    "Temperature": 0.2,
    "MaxFilesPerRequest": 15,
    "MaxLinesPerRequest": 3000
  }
}
```

## Extending the Bot

### Adding a Summary Comment

Modify `CodeReviewProcessor.cs` to post a summary:

```csharp
// After posting individual comments
if (uniqueComments.Count > 0)
{
    var summary = $"AI Review Complete: Found {uniqueComments.Count} issues\n" +
                  $"- Critical: {uniqueComments.Count(c => c.Severity >= ReviewSeverity.Critical)}\n" +
                  $"- Major: {uniqueComments.Count(c => c.Severity == ReviewSeverity.Major)}";

    await PostSummaryComment(request, summary, cancellationToken);
}
```

### Adding Vote/Status

Automatically set PR vote based on findings:

```csharp
var hasBlockers = uniqueComments.Any(c => c.Severity == ReviewSeverity.Blocker);
var hasCritical = uniqueComments.Any(c => c.Severity == ReviewSeverity.Critical);

var vote = hasBlockers ? -10 :  // Reject
           hasCritical ? -5 :   // Wait
           uniqueComments.Count == 0 ? 10 : // Approve
           0;  // No vote

await _adoService.SetReviewerVoteAsync(request, vote, cancellationToken);
```

### Customizing Overview Output

The Overview Agent generates an executive summary. You can customize what's included by modifying `prompts/overview-generation.md`.

**Available Fields:**
- `summary` - 2-4 sentence executive summary
- `keyChanges` - List of significant changes with rationale
- `importantFiles` - Files ranked by impact score (1-5)
- `confidenceScore` - Review confidence (1-5)
- `riskAssessment` - Risk level: `safe`, `low-risk`, `medium-risk`, `high-risk`, `critical-risk`

**Customizing Risk Assessment Logic:**

Modify the risk derivation in `OverviewAgent.cs`:

```csharp
private static string DeriveRiskFromComments(List<ReviewComment> comments)
{
    // Add your custom logic here
    if (comments.Any(c => c.Category == ReviewCategory.Security))
        return "high-risk";  // Security issues always high risk

    // Default logic
    if (comments.Count == 0) return "safe";
    var maxSeverity = comments.Max(c => (int)c.Severity);
    // ...
}
```

**Customizing Important Files Selection:**

The Overview Agent scores files by impact. Modify the prompt to change scoring criteria:

```markdown
## Score Guidance (in prompts/overview-generation.md)

| Score | Meaning |
|-------|---------|
| 1 | Trivial change (whitespace, comments) |
| 2 | Minor change (small refactor) |
| 3 | Moderate change (new functionality) |
| 4 | Significant change (core logic) |
| 5 | Critical change (security, data model) |
```

### Filtering by File Path

Only review certain directories:

```csharp
var filteredFiles = files
    .Where(f => f.Path.StartsWith("src/") || f.Path.StartsWith("lib/"))
    .Where(f => !f.Path.Contains("/test/"))
    .ToList();
```

### Adding Custom Metrics

Track review effectiveness:

```csharp
_telemetryClient.TrackEvent("CodeReviewCompleted", new Dictionary<string, string>
{
    ["pullRequestId"] = request.PullRequestId.ToString(),
    ["repository"] = request.RepositoryName
}, new Dictionary<string, double>
{
    ["filesReviewed"] = files.Count,
    ["commentsGenerated"] = reviewResponse.Comments.Count,
    ["commentsPosted"] = uniqueComments.Count,
    ["tokensUsed"] = reviewResponse.TokenUsage?.TotalTokens ?? 0
});
```

## Integrating with Other Systems

### Slack Notifications

Add a Slack notification after review:

```csharp
var slackMessage = new
{
    text = $"AI Review Complete for PR #{request.PullRequestId}",
    attachments = new[]
    {
        new
        {
            color = hasCritical ? "danger" : "good",
            fields = new[]
            {
                new { title = "Repository", value = request.RepositoryName },
                new { title = "Issues Found", value = uniqueComments.Count.ToString() }
            }
        }
    }
};

await _httpClient.PostAsJsonAsync(slackWebhookUrl, slackMessage);
```

### GitHub Issues

Create issues for critical findings:

```csharp
var criticalIssues = uniqueComments
    .Where(c => c.Severity >= ReviewSeverity.Critical)
    .ToList();

foreach (var issue in criticalIssues)
{
    await CreateGitHubIssue(new
    {
        title = $"[AI Review] {issue.Category}: {issue.Message.Substring(0, 50)}...",
        body = $"Found in PR #{request.PullRequestId}\n\n{issue.Message}",
        labels = new[] { "ai-review", issue.Category.ToString().ToLower() }
    });
}
```

## Best Practices

1. **Test changes locally** before deploying
2. **Start conservative** with severity thresholds
3. **Iterate on prompts** based on feedback
4. **Monitor token usage** to control costs
5. **Review AI comments** periodically for quality
6. **Adjust based on team feedback** - the goal is helpful, not annoying
