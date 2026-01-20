using System.Text.Json.Serialization;

namespace DevOpsCodeReviewer.Functions.Models;

/// <summary>
/// Service Bus message model for code review requests.
/// </summary>
public record CodeReviewRequest
{
    /// <summary>
    /// The Azure DevOps organization URL.
    /// </summary>
    [JsonPropertyName("organizationUrl")]
    public string OrganizationUrl { get; init; } = string.Empty;

    /// <summary>
    /// The project ID or name.
    /// </summary>
    [JsonPropertyName("projectId")]
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>
    /// The project name for display purposes.
    /// </summary>
    [JsonPropertyName("projectName")]
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>
    /// The repository ID.
    /// </summary>
    [JsonPropertyName("repositoryId")]
    public string RepositoryId { get; init; } = string.Empty;

    /// <summary>
    /// The repository name for display purposes.
    /// </summary>
    [JsonPropertyName("repositoryName")]
    public string RepositoryName { get; init; } = string.Empty;

    /// <summary>
    /// The pull request ID.
    /// </summary>
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; init; }

    /// <summary>
    /// The PR title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The PR description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// The source branch reference name.
    /// </summary>
    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; init; } = string.Empty;

    /// <summary>
    /// The target branch reference name.
    /// </summary>
    [JsonPropertyName("targetBranch")]
    public string TargetBranch { get; init; } = string.Empty;

    /// <summary>
    /// The author's display name.
    /// </summary>
    [JsonPropertyName("authorName")]
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the webhook was received.
    /// </summary>
    [JsonPropertyName("receivedAt")]
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The event type that triggered this review.
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// The latest iteration ID to review (for update events).
    /// </summary>
    [JsonPropertyName("iterationId")]
    public int? IterationId { get; init; }
}
