using System.Text.Json.Serialization;

namespace DevOpsCodeReviewer.Functions.Models;

/// <summary>
/// Azure DevOps webhook payload for pull request events.
/// </summary>
public class PullRequestPayload
{
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("notificationId")]
    public int NotificationId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("publisherId")]
    public string? PublisherId { get; set; }

    [JsonPropertyName("message")]
    public WebhookMessage? Message { get; set; }

    [JsonPropertyName("resource")]
    public PullRequestResource Resource { get; set; } = new();

    [JsonPropertyName("resourceVersion")]
    public string? ResourceVersion { get; set; }

    [JsonPropertyName("resourceContainers")]
    public ResourceContainers? ResourceContainers { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }
}

public class WebhookMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("markdown")]
    public string? Markdown { get; set; }
}

public class PullRequestResource
{
    [JsonPropertyName("repository")]
    public Repository Repository { get; set; } = new();

    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("codeReviewId")]
    public int CodeReviewId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public IdentityRef? CreatedBy { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("sourceRefName")]
    public string SourceRefName { get; set; } = string.Empty;

    [JsonPropertyName("targetRefName")]
    public string TargetRefName { get; set; } = string.Empty;

    [JsonPropertyName("mergeStatus")]
    public string? MergeStatus { get; set; }

    [JsonPropertyName("isDraft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("mergeId")]
    public string? MergeId { get; set; }

    [JsonPropertyName("lastMergeSourceCommit")]
    public CommitRef? LastMergeSourceCommit { get; set; }

    [JsonPropertyName("lastMergeTargetCommit")]
    public CommitRef? LastMergeTargetCommit { get; set; }

    [JsonPropertyName("lastMergeCommit")]
    public CommitRef? LastMergeCommit { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class Repository
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("project")]
    public Project Project { get; set; } = new();

    [JsonPropertyName("defaultBranch")]
    public string? DefaultBranch { get; set; }

    [JsonPropertyName("remoteUrl")]
    public string? RemoteUrl { get; set; }
}

public class Project
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

public class IdentityRef
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}

public class CommitRef
{
    [JsonPropertyName("commitId")]
    public string CommitId { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class ResourceContainers
{
    [JsonPropertyName("collection")]
    public ResourceContainer? Collection { get; set; }

    [JsonPropertyName("account")]
    public ResourceContainer? Account { get; set; }

    [JsonPropertyName("project")]
    public ResourceContainer? Project { get; set; }
}

public class ResourceContainer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }
}
