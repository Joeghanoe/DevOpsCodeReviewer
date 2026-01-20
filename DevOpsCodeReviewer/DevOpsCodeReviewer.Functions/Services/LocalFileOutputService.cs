using System.Collections.Concurrent;
using System.Text;
using DevOpsCodeReviewer.Functions.Configuration;
using DevOpsCodeReviewer.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsCodeReviewer.Functions.Services;

/// <summary>
/// Implementation of IReviewOutputService that writes comments to a local TXT file.
/// Used in Debug builds for local development and testing.
/// </summary>
public class LocalFileOutputService : IReviewOutputService
{
    private readonly ReviewOutputOptions _options;
    private readonly ILogger<LocalFileOutputService> _logger;
    private readonly ConcurrentDictionary<int, List<BufferedComment>> _commentBuffers = new();

    public LocalFileOutputService(
        IOptions<ReviewOutputOptions> options,
        ILogger<LocalFileOutputService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<bool> PublishCommentAsync(
        CodeReviewRequest request,
        ReviewComment comment,
        string formattedContent,
        CancellationToken ct)
    {
        var buffer = _commentBuffers.GetOrAdd(request.PullRequestId, _ => new List<BufferedComment>());

        lock (buffer)
        {
            buffer.Add(new BufferedComment
            {
                Comment = comment,
                FormattedContent = formattedContent,
                Request = request
            });
        }

        if (_options.LogToConsole)
        {
            _logger.LogInformation("[DEBUG OUTPUT] Comment buffered for {FilePath}:{LineNumber} - {Category}/{Severity}",
                comment.FilePath, comment.LineNumber, comment.Category, comment.Severity);
        }

        return Task.FromResult(true);
    }

    public async Task FinalizeReviewAsync(CodeReviewRequest request, int totalComments, CancellationToken ct)
    {
        if (!_commentBuffers.TryRemove(request.PullRequestId, out var buffer) || buffer.Count == 0)
        {
            _logger.LogInformation("[DEBUG OUTPUT] No comments to write for PR #{PullRequestId}", request.PullRequestId);
            return;
        }

        // Ensure output directory exists
        Directory.CreateDirectory(_options.OutputDirectory);

        // Generate filename
        var sanitizedProject = SanitizeFileName(request.ProjectName);
        var sanitizedRepo = SanitizeFileName(request.RepositoryName);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
        var fileName = $"PR_{request.PullRequestId}_{sanitizedProject}_{sanitizedRepo}_{timestamp}.txt";
        var filePath = Path.Combine(_options.OutputDirectory, fileName);

        // Build file content
        var content = BuildFileContent(request, buffer);

        // Write to file
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, ct);

        _logger.LogInformation("[DEBUG OUTPUT] Code review report written to: {FilePath}", filePath);

        if (_options.LogToConsole)
        {
            _logger.LogInformation("[DEBUG OUTPUT] Report contains {CommentCount} comments for PR #{PullRequestId}",
                buffer.Count, request.PullRequestId);
        }
    }

    private static string BuildFileContent(CodeReviewRequest request, List<BufferedComment> comments)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("================================================================================");
        sb.AppendLine("CODE REVIEW REPORT");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Pull Request ID: {request.PullRequestId}");
        sb.AppendLine($"Project: {request.ProjectName}");
        sb.AppendLine($"Repository: {request.RepositoryName}");
        sb.AppendLine($"Author: {request.AuthorName}");
        sb.AppendLine($"Title: {request.Title}");
        sb.AppendLine($"Source Branch: {request.SourceBranch}");
        sb.AppendLine($"Target Branch: {request.TargetBranch}");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"COMMENTS ({comments.Count} total)");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        // Comments
        for (int i = 0; i < comments.Count; i++)
        {
            var buffered = comments[i];
            var comment = buffered.Comment;

            sb.AppendLine($"--- Comment {i + 1} ---");
            sb.AppendLine($"File: {comment.FilePath}");
            sb.AppendLine($"Line: {comment.LineNumber}{(comment.EndLineNumber.HasValue ? $"-{comment.EndLineNumber}" : "")}");
            sb.AppendLine($"Category: {comment.Category}");
            sb.AppendLine($"Severity: {comment.Severity}");
            sb.AppendLine();
            sb.AppendLine("Message:");
            sb.AppendLine(comment.Message);

            if (!string.IsNullOrEmpty(comment.Suggestion))
            {
                sb.AppendLine();
                sb.AppendLine("Suggestion:");
                sb.AppendLine(comment.Suggestion);
            }

            if (!string.IsNullOrEmpty(comment.SuggestedCode))
            {
                sb.AppendLine();
                sb.AppendLine("Suggested Code:");
                sb.AppendLine("```");
                sb.AppendLine(comment.SuggestedCode);
                sb.AppendLine("```");
            }

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(name.Length);

        foreach (var c in name)
        {
            sanitized.Append(invalidChars.Contains(c) ? '_' : c);
        }

        return sanitized.ToString();
    }

    private class BufferedComment
    {
        public ReviewComment Comment { get; init; } = null!;
        public string FormattedContent { get; init; } = string.Empty;
        public CodeReviewRequest Request { get; init; } = null!;
    }
}
