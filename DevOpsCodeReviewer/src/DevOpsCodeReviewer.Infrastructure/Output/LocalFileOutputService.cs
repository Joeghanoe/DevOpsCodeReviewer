using System.Collections.Concurrent;
using System.Text;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsCodeReviewer.Infrastructure.Output;

/// <summary>
/// Outputs review comments to local files for debugging.
/// </summary>
public class LocalFileOutputService : IReviewOutputService
{
    private readonly ReviewOutputOptions _options;
    private readonly ILogger<LocalFileOutputService> _logger;
    private readonly ConcurrentDictionary<string, List<string>> _buffers = new();

    public LocalFileOutputService(
        IOptions<ReviewOutputOptions> options,
        ILogger<LocalFileOutputService> logger)
    {
        _options = options.Value;
        _logger = logger;
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

        if (!_buffers.TryRemove(key, out var buffer))
        {
            _logger.LogWarning("No buffer found for PR {PullRequestId}", request.PullRequestId);
            return;
        }

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
        sb.AppendLine("---");
        sb.AppendLine();

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

        await File.WriteAllTextAsync(filePath, sb.ToString(), ct);

        _logger.LogInformation("Review output written to {FilePath} ({CommentCount} comments)",
            filePath, totalComments);
    }

    private static string GetBufferKey(CodeReviewRequest request)
    {
        return $"{request.ProjectId}_{request.RepositoryId}_{request.PullRequestId}";
    }
}
