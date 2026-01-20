using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps.Models;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

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

    public async Task<bool> PublishOverviewAsync(CodeReviewRequest request, ReviewOverview overview, CancellationToken ct)
    {
        try
        {
            var formattedOverview = FormatOverview(overview);

            // Post overview as a general PR comment (no file context)
            var threadRequest = new CreateThreadRequest
            {
                Comments =
                [
                    new CreateComment
                    {
                        Content = formattedOverview,
                        CommentType = "text"
                    }
                ],
                Status = "closed" // Overview is informational, mark as closed
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
                _logger.LogInformation("Posted review overview to PR #{PullRequestId}", request.PullRequestId);
                return true;
            }

            _logger.LogWarning("Failed to post review overview to PR #{PullRequestId}", request.PullRequestId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting review overview to PR #{PullRequestId}", request.PullRequestId);
            return false;
        }
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

    private string FormatOverview(ReviewOverview overview)
    {
        var sb = new StringBuilder();

        // Risk badge
        var riskEmoji = overview.RiskAssessment switch
        {
            "safe" => "✅",
            "low-risk" => "🟢",
            "medium-risk" => "🟡",
            "high-risk" => "🟠",
            "critical-risk" => "🔴",
            _ => "⚪"
        };

        sb.AppendLine("## 📋 Code Review Overview");
        sb.AppendLine();
        sb.AppendLine("### Summary");
        sb.AppendLine(overview.Summary);
        sb.AppendLine();

        // Key Changes
        if (overview.KeyChanges.Count > 0)
        {
            sb.AppendLine("### Key Changes");
            foreach (var change in overview.KeyChanges)
            {
                sb.AppendLine($"- **{change.Description}**");
                if (!string.IsNullOrEmpty(change.Rationale))
                {
                    sb.AppendLine($"  - {change.Rationale}");
                }
            }
            sb.AppendLine();
        }

        // Important Files
        if (overview.ImportantFiles.Count > 0)
        {
            sb.AppendLine("### Important Files Changed");
            sb.AppendLine();
            sb.AppendLine("| File | Score | Overview |");
            sb.AppendLine("|------|-------|----------|");
            foreach (var file in overview.ImportantFiles.OrderByDescending(f => f.Score))
            {
                var scoreDisplay = $"{file.Score}/5";
                sb.AppendLine($"| `{file.FilePath}` | {scoreDisplay} | {file.Description ?? "-"} |");
            }
            sb.AppendLine();
        }

        // Confidence and Risk Assessment
        sb.AppendLine($"### Assessment");
        sb.AppendLine();
        sb.AppendLine($"**Confidence Score**: {overview.ConfidenceScore}/5");
        if (!string.IsNullOrEmpty(overview.ConfidenceRationale))
        {
            sb.AppendLine($"> {overview.ConfidenceRationale}");
        }
        sb.AppendLine();
        sb.AppendLine($"**Risk Assessment**: {riskEmoji} {overview.RiskAssessment}");
        sb.AppendLine();

        // Bot signature
        sb.AppendLine($"{_options.BotSignature}");

        return sb.ToString();
    }
}
