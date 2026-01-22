using System.Text.Json.Serialization;

namespace DevOpsCodeReviewer.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Detailed pull request information from Azure DevOps API.
/// </summary>
public class PullRequestDetails
{
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("sourceRefName")]
    public string SourceRefName { get; set; } = string.Empty;

    [JsonPropertyName("targetRefName")]
    public string TargetRefName { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public IdentityRef? CreatedBy { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; }

    [JsonPropertyName("lastMergeSourceCommit")]
    public CommitRef? LastMergeSourceCommit { get; set; }

    [JsonPropertyName("lastMergeTargetCommit")]
    public CommitRef? LastMergeTargetCommit { get; set; }
}

public class PullRequestIteration
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("author")]
    public IdentityRef? Author { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }

    [JsonPropertyName("updatedDate")]
    public DateTime UpdatedDate { get; set; }

    [JsonPropertyName("sourceRefCommit")]
    public CommitRef? SourceRefCommit { get; set; }

    [JsonPropertyName("targetRefCommit")]
    public CommitRef? TargetRefCommit { get; set; }

    [JsonPropertyName("commonRefCommit")]
    public CommitRef? CommonRefCommit { get; set; }

    [JsonPropertyName("hasMoreCommits")]
    public bool HasMoreCommits { get; set; }
}

public class IterationsResponse
{
    [JsonPropertyName("value")]
    public List<PullRequestIteration> Value { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class IterationChange
{
    [JsonPropertyName("changeId")]
    public int ChangeId { get; set; }

    [JsonPropertyName("changeTrackingId")]
    public int ChangeTrackingId { get; set; }

    [JsonPropertyName("item")]
    public ChangeItem Item { get; set; } = new();

    [JsonPropertyName("changeType")]
    public string ChangeType { get; set; } = string.Empty;
}

public class ChangeItem
{
    [JsonPropertyName("objectId")]
    public string? ObjectId { get; set; }

    [JsonPropertyName("originalObjectId")]
    public string? OriginalObjectId { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

public class IterationChangesResponse
{
    [JsonPropertyName("changeEntries")]
    public List<IterationChange> ChangeEntries { get; set; } = [];
}

public class CommentThread
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("publishedDate")]
    public DateTime PublishedDate { get; set; }

    [JsonPropertyName("lastUpdatedDate")]
    public DateTime LastUpdatedDate { get; set; }

    [JsonPropertyName("comments")]
    public List<Comment> Comments { get; set; } = [];

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("threadContext")]
    public ThreadContext? ThreadContext { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertyValue>? Properties { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }
}

public class ThreadContext
{
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("rightFileStart")]
    public LinePosition? RightFileStart { get; set; }

    [JsonPropertyName("rightFileEnd")]
    public LinePosition? RightFileEnd { get; set; }

    [JsonPropertyName("leftFileStart")]
    public LinePosition? LeftFileStart { get; set; }

    [JsonPropertyName("leftFileEnd")]
    public LinePosition? LeftFileEnd { get; set; }
}

public class LinePosition
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

public class Comment
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("parentCommentId")]
    public int ParentCommentId { get; set; }

    [JsonPropertyName("author")]
    public IdentityRef? Author { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("publishedDate")]
    public DateTime PublishedDate { get; set; }

    [JsonPropertyName("lastUpdatedDate")]
    public DateTime LastUpdatedDate { get; set; }

    [JsonPropertyName("commentType")]
    public string? CommentType { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }
}

public class PropertyValue
{
    [JsonPropertyName("$type")]
    public string? Type { get; set; }

    [JsonPropertyName("$value")]
    public object? Value { get; set; }
}

public class ThreadsResponse
{
    [JsonPropertyName("value")]
    public List<CommentThread> Value { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class CreateThreadRequest
{
    [JsonPropertyName("comments")]
    public List<CreateComment> Comments { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("threadContext")]
    public ThreadContext? ThreadContext { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertyValue>? Properties { get; set; }
}

public class CreateComment
{
    [JsonPropertyName("parentCommentId")]
    public int ParentCommentId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("commentType")]
    public string CommentType { get; set; } = "text";
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

/// <summary>
/// Request to create or update a pull request status.
/// </summary>
public class CreatePullRequestStatusRequest
{
    [JsonPropertyName("state")]
    public string State { get; set; } = "pending";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("targetUrl")]
    public string? TargetUrl { get; set; }

    [JsonPropertyName("context")]
    public StatusContext Context { get; set; } = new();
}

/// <summary>
/// Context identifier for the status (genre + name).
/// </summary>
public class StatusContext
{
    [JsonPropertyName("genre")]
    public string Genre { get; set; } = "code-review";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "AI Code Review";
}

/// <summary>
/// Pull request status response from Azure DevOps.
/// </summary>
public class PullRequestStatus
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("context")]
    public StatusContext? Context { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime CreationDate { get; set; }

    [JsonPropertyName("updatedDate")]
    public DateTime UpdatedDate { get; set; }
}
