using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;

namespace DevOpsCodeReviewer.Infrastructure.Output;

/// <summary>
/// Outputs review comments to local files for debugging.
/// </summary>
public class LocalFileOutputService : IReviewOutputService
{
    private readonly ReviewOutputOptions _options;
    private readonly ILogger<LocalFileOutputService> _logger;
    private readonly ConcurrentDictionary<string, List<string>> _buffers = new();
    private readonly ConcurrentDictionary<string, string> _overviews = new();

    public LocalFileOutputService(
        IOptions<ReviewOutputOptions> options,
        ILogger<LocalFileOutputService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> PublishOverviewAsync(CodeReviewRequest request, ReviewOverview overview, CancellationToken ct)
    {
        var key = GetBufferKey(request);
        var formatted = FormatOverview(overview);
        _overviews[key] = formatted;

        if (_options.LogToConsole)
        {
            _logger.LogInformation("Review Overview: {Summary}", overview.Summary);
        }

        return Task.FromResult(true);
    }

    public Task<bool> PublishCommentAsync(CodeReviewRequest request, ReviewComment comment, string formattedContent, CancellationToken ct)
    {
        var key = GetBufferKey(request);

        var buffer = _buffers.GetOrAdd(key, _ => new List<string>());

        lock (buffer)
        {
            buffer.Add(formattedContent);
        }

        if (_options.LogToConsole)
        {
            _logger.LogInformation("[{Severity}] {FilePath}:{Line} - {Message}",
                comment.Severity, comment.FilePath, comment.LineNumber, comment.Message);
        }

        return Task.FromResult(true);
    }

    public async Task FinalizeReviewAsync(CodeReviewRequest request, int totalComments, CancellationToken ct)
    {
        var key = GetBufferKey(request);

        _buffers.TryRemove(key, out var buffer);
        _overviews.TryRemove(key, out var overview);

        // Ensure output directory exists
        Directory.CreateDirectory(_options.OutputDirectory);

        var fileName = $"PR_{request.PullRequestId}_{request.ProjectName}_{request.RepositoryName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.md";
        var filePath = Path.Combine(_options.OutputDirectory, fileName);

        var sb = new StringBuilder();
        sb.AppendLine($"# Code Review: {request.Title}");
        sb.AppendLine();
        sb.AppendLine($"**PR**: #{request.PullRequestId}");
        sb.AppendLine($"**Repository**: {request.RepositoryName}");
        sb.AppendLine($"**Author**: {request.AuthorName}");
        sb.AppendLine($"**Generated**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Total Comments**: {totalComments}");
        sb.AppendLine();

        // Add overview if present
        if (!string.IsNullOrEmpty(overview))
        {
            sb.AppendLine(overview);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Detailed Comments");
        sb.AppendLine();

        if (buffer != null)
        {
            lock (buffer)
            {
                foreach (var comment in buffer)
                {
                    sb.AppendLine(comment);
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), ct);

        _logger.LogInformation("Review output written to {FilePath} ({CommentCount} comments)",
            filePath, totalComments);
    }

    private static string FormatOverview(ReviewOverview overview)
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

        sb.AppendLine("## 📋 Review Overview");
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

        return sb.ToString();
    }

    private static string GetBufferKey(CodeReviewRequest request)
    {
        return $"{request.ProjectId}_{request.RepositoryId}_{request.PullRequestId}";
    }
}
