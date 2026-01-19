using System.Text.Json.Serialization;

namespace DevOpsCodeReviewer.Functions.Models;

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

/// <summary>
/// Pull request iteration (a set of changes).
/// </summary>
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

/// <summary>
/// Response wrapper for iterations list.
/// </summary>
public class IterationsResponse
{
    [JsonPropertyName("value")]
    public List<PullRequestIteration> Value { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// File change in a pull request iteration.
/// </summary>
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

/// <summary>
/// Item details for a change.
/// </summary>
public class ChangeItem
{
    [JsonPropertyName("objectId")]
    public string? ObjectId { get; set; }

    [JsonPropertyName("originalObjectId")]
    public string? OriginalObjectId { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// Response wrapper for iteration changes.
/// </summary>
public class IterationChangesResponse
{
    [JsonPropertyName("changeEntries")]
    public List<IterationChange> ChangeEntries { get; set; } = [];
}

/// <summary>
/// Pull request comment thread.
/// </summary>
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

/// <summary>
/// Thread context with file and line information.
/// </summary>
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

/// <summary>
/// Line position in a file.
/// </summary>
public class LinePosition
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

/// <summary>
/// Individual comment in a thread.
/// </summary>
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

/// <summary>
/// Property value for thread properties.
/// </summary>
public class PropertyValue
{
    [JsonPropertyName("$type")]
    public string? Type { get; set; }

    [JsonPropertyName("$value")]
    public object? Value { get; set; }
}

/// <summary>
/// Response wrapper for threads list.
/// </summary>
public class ThreadsResponse
{
    [JsonPropertyName("value")]
    public List<CommentThread> Value { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// Request to create a new comment thread.
/// </summary>
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

/// <summary>
/// Comment to create in a new thread.
/// </summary>
public class CreateComment
{
    [JsonPropertyName("parentCommentId")]
    public int ParentCommentId { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("commentType")]
    public string CommentType { get; set; } = "text";
}

/// <summary>
/// File content with metadata.
/// </summary>
public class FileContent
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public string? ObjectId { get; set; }
    public string? OriginalObjectId { get; set; }
}
