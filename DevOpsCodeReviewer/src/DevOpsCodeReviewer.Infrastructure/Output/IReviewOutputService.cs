using DevOpsCodeReviewer.Core.Models;

namespace DevOpsCodeReviewer.Infrastructure.Output;

/// <summary>
/// Abstraction for publishing code review comments.
/// Implementations can post to Azure DevOps or write to local files.
/// </summary>
public interface IReviewOutputService
{
    /// <summary>
    /// Publishes a single code review comment.
    /// </summary>
    /// <param name="request">The code review request containing PR context.</param>
    /// <param name="comment">The review comment to publish.</param>
    /// <param name="formattedContent">The formatted comment content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the comment was successfully published.</returns>
    Task<bool> PublishCommentAsync(CodeReviewRequest request, ReviewComment comment, string formattedContent, CancellationToken ct);

    /// <summary>
    /// Finalizes the review output after all comments have been published.
    /// For local file output, this writes the buffered comments to disk.
    /// </summary>
    /// <param name="request">The code review request containing PR context.</param>
    /// <param name="totalComments">The total number of comments published.</param>
    /// <param name="ct">Cancellation token.</param>
    Task FinalizeReviewAsync(CodeReviewRequest request, int totalComments, CancellationToken ct);
}
