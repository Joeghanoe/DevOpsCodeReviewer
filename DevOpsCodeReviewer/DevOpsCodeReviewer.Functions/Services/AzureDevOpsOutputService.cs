using DevOpsCodeReviewer.Functions.Models;
using Microsoft.Extensions.Logging;

namespace DevOpsCodeReviewer.Functions.Services;

/// <summary>
/// Implementation of IReviewOutputService that posts comments to Azure DevOps.
/// Used in Release builds for production deployment.
/// </summary>
public class AzureDevOpsOutputService : IReviewOutputService
{
    private readonly IAzureDevOpsService _adoService;
    private readonly ILogger<AzureDevOpsOutputService> _logger;

    public AzureDevOpsOutputService(
        IAzureDevOpsService adoService,
        ILogger<AzureDevOpsOutputService> logger)
    {
        _adoService = adoService;
        _logger = logger;
    }

    public async Task<bool> PublishCommentAsync(
        CodeReviewRequest request,
        ReviewComment comment,
        string formattedContent,
        CancellationToken ct)
    {
        var threadRequest = new CreateThreadRequest
        {
            Comments =
            [
                new CreateComment
                {
                    Content = formattedContent,
                    CommentType = "text"
                }
            ],
            Status = "active",
            ThreadContext = new ThreadContext
            {
                FilePath = comment.FilePath,
                RightFileStart = new LinePosition
                {
                    Line = comment.LineNumber,
                    Offset = 1
                },
                RightFileEnd = new LinePosition
                {
                    Line = comment.EndLineNumber ?? comment.LineNumber,
                    Offset = 1
                }
            }
        };

        var thread = await _adoService.CreateCommentThreadAsync(
            request.OrganizationUrl,
            request.ProjectId,
            request.RepositoryId,
            request.PullRequestId,
            threadRequest,
            ct);

        if (thread != null)
        {
            _logger.LogDebug("Posted comment to {FilePath}:{LineNumber}",
                comment.FilePath, comment.LineNumber);
            return true;
        }

        _logger.LogWarning("Failed to post comment to {FilePath}:{LineNumber}",
            comment.FilePath, comment.LineNumber);
        return false;
    }

    public Task FinalizeReviewAsync(CodeReviewRequest request, int totalComments, CancellationToken ct)
    {
        // Azure DevOps posts comments immediately, no finalization needed
        _logger.LogInformation("Review finalized for PR #{PullRequestId} with {TotalComments} comments posted to Azure DevOps",
            request.PullRequestId, totalComments);
        return Task.CompletedTask;
    }
}
