# Customization Guide

This guide explains how to customize the AI code reviewer for your team's needs.

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
