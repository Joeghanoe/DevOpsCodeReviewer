using DevOpsCodeReviewer.Functions.Models;

namespace DevOpsCodeReviewer.Functions.Services;

/// <summary>
/// Interface for Azure DevOps API operations.
/// </summary>
public interface IAzureDevOpsService
{
    /// <summary>
    /// Gets pull request details.
    /// </summary>
    Task<PullRequestDetails?> GetPullRequestAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all iterations for a pull request.
    /// </summary>
    Task<List<PullRequestIteration>> GetPullRequestIterationsAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file changes for a specific iteration.
    /// </summary>
    Task<List<IterationChange>> GetIterationChangesAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        int iterationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file content at a specific commit.
    /// </summary>
    Task<string?> GetFileContentAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        string path,
        string? commitId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all comment threads for a pull request.
    /// </summary>
    Task<List<CommentThread>> GetPullRequestThreadsAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new comment thread on a pull request.
    /// </summary>
    Task<CommentThread?> CreateCommentThreadAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CreateThreadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file contents for all changed files in an iteration.
    /// </summary>
    Task<List<FileContent>> GetChangedFilesContentAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        int iterationId,
        string? sourceCommitId,
        CancellationToken cancellationToken = default);
}
