using System.Text.Json.Serialization;

namespace DevOpsCodeReviewer.Core.Models;

/// <summary>
/// Service Bus message model for code review requests.
/// </summary>
public record CodeReviewRequest
{
    [JsonPropertyName("organizationUrl")]
    public string OrganizationUrl { get; init; } = string.Empty;

    [JsonPropertyName("projectId")]
    public string ProjectId { get; init; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string ProjectName { get; init; } = string.Empty;

    [JsonPropertyName("repositoryId")]
    public string RepositoryId { get; init; } = string.Empty;

    [JsonPropertyName("repositoryName")]
    public string RepositoryName { get; init; } = string.Empty;

    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; init; } = string.Empty;

    [JsonPropertyName("targetBranch")]
    public string TargetBranch { get; init; } = string.Empty;

    [JsonPropertyName("authorName")]
    public string AuthorName { get; init; } = string.Empty;

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("receivedAt")]
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("iterationId")]
    public int? IterationId { get; init; }
}
