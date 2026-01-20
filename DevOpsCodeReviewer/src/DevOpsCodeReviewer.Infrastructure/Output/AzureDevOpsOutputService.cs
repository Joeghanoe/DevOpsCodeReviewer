using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps.Models;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsCodeReviewer.Infrastructure.Output;

/// <summary>
/// Outputs review comments to Azure DevOps pull request threads.
/// </summary>
public class AzureDevOpsOutputService : IReviewOutputService
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsOutputService> _logger;

    public AzureDevOpsOutputService(
        IAzureDevOpsService azureDevOpsService,
        IOptions<AzureDevOpsOptions> options,
        ILogger<AzureDevOpsOutputService> logger)
    {
        _azureDevOpsService = azureDevOpsService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> PublishCommentAsync(CodeReviewRequest request, ReviewComment comment, string formattedContent, CancellationToken ct)
    {
        try
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

            var result = await _azureDevOpsService.CreateCommentThreadAsync(
                request.OrganizationUrl,
                request.ProjectId,
                request.RepositoryId,
                request.PullRequestId,
                threadRequest,
                ct);

            if (result != null)
            {
                _logger.LogDebug("Created comment thread {ThreadId} on {FilePath}:{Line}",
                    result.Id, comment.FilePath, comment.LineNumber);
                return true;
            }

            _logger.LogWarning("Failed to create comment thread on {FilePath}:{Line}",
                comment.FilePath, comment.LineNumber);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment thread on {FilePath}:{Line}",
                comment.FilePath, comment.LineNumber);
            return false;
        }
    }

    public Task FinalizeReviewAsync(CodeReviewRequest request, int totalComments, CancellationToken ct)
    {
        _logger.LogInformation("Published {CommentCount} comments to PR #{PullRequestId}",
            totalComments, request.PullRequestId);

        return Task.CompletedTask;
    }
}
