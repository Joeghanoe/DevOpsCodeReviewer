using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DevOpsCodeReviewer.Functions.Configuration;
using DevOpsCodeReviewer.Functions.Models;
using DevOpsCodeReviewer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsCodeReviewer.Functions.Functions;

/// <summary>
/// Service Bus trigger function for processing code reviews.
/// </summary>
public class CodeReviewProcessor
{
    private readonly IAzureDevOpsService _adoService;
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly ILlmService _llmService;
    private readonly AzureDevOpsOptions _adoOptions;
    private readonly LlmOptions _llmOptions;
    private readonly ILogger<CodeReviewProcessor> _logger;

    public CodeReviewProcessor(
        IAzureDevOpsService adoService,
        ICodeAnalysisService codeAnalysisService,
        ILlmService llmService,
        IOptions<AzureDevOpsOptions> adoOptions,
        IOptions<LlmOptions> llmOptions,
        ILogger<CodeReviewProcessor> logger)
    {
        _adoService = adoService;
        _codeAnalysisService = codeAnalysisService;
        _llmService = llmService;
        _adoOptions = adoOptions.Value;
        _llmOptions = llmOptions.Value;
        _logger = logger;
    }

    [Function("CodeReviewProcessor")]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBus:QueueName%", Connection = "ServiceBus:ConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        CodeReviewRequest? request = null;

        try
        {
            // Deserialize the request
            request = JsonSerializer.Deserialize<CodeReviewRequest>(
                message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                _logger.LogError("Failed to deserialize message: {MessageId}", message.MessageId);
                await messageActions.DeadLetterMessageAsync(message,
                    deadLetterReason: "DeserializationFailed",
                    deadLetterErrorDescription: "Could not deserialize the message body",
                    cancellationToken: cancellationToken);
                return;
            }

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = request.CorrelationId,
                ["PullRequestId"] = request.PullRequestId,
                ["Project"] = request.ProjectName,
                ["Repository"] = request.RepositoryName
            });

            _logger.LogInformation("Processing code review for PR #{PullRequestId}", request.PullRequestId);

            await ProcessReviewAsync(request, cancellationToken);

            // Complete the message
            await messageActions.CompleteMessageAsync(message, cancellationToken);

            _logger.LogInformation("Completed code review for PR #{PullRequestId}", request.PullRequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing code review for PR #{PullRequestId}",
                request?.PullRequestId ?? 0);

            // Let Service Bus handle retries
            throw;
        }
    }

    private async Task ProcessReviewAsync(CodeReviewRequest request, CancellationToken cancellationToken)
    {
        // Get PR iterations
        var iterations = await _adoService.GetPullRequestIterationsAsync(
            request.OrganizationUrl,
            request.ProjectId,
            request.RepositoryId,
            request.PullRequestId,
            cancellationToken);

        if (iterations.Count == 0)
        {
            _logger.LogWarning("No iterations found for PR #{PullRequestId}", request.PullRequestId);
            return;
        }

        // Use the latest iteration
        var latestIteration = iterations.MaxBy(i => i.Id)!;
        _logger.LogInformation("Using iteration {IterationId} for PR #{PullRequestId}",
            latestIteration.Id, request.PullRequestId);

        // Get changed files
        var files = await _adoService.GetChangedFilesContentAsync(
            request.OrganizationUrl,
            request.ProjectId,
            request.RepositoryId,
            request.PullRequestId,
            latestIteration.Id,
            latestIteration.SourceRefCommit?.CommitId,
            cancellationToken);

        if (files.Count == 0)
        {
            _logger.LogInformation("No reviewable files found for PR #{PullRequestId}", request.PullRequestId);
            return;
        }

        _logger.LogInformation("Found {FileCount} reviewable files", files.Count);

        // Filter and prioritize files
        var filteredFiles = _codeAnalysisService.FilterReviewableFiles(files);
        var prioritizedFiles = _codeAnalysisService.PrioritizeFiles(filteredFiles);

        // Chunk files for LLM processing
        var chunks = _codeAnalysisService.ChunkFilesForReview(
            prioritizedFiles,
            _llmOptions.MaxFilesPerRequest,
            _llmOptions.MaxLinesPerRequest);

        // Call LLM for code review
        var reviewResponse = await _llmService.ReviewCodeInChunksAsync(request, chunks, cancellationToken);

        _logger.LogInformation("LLM generated {CommentCount} comments", reviewResponse.Comments.Count);

        if (reviewResponse.TokenUsage != null)
        {
            _logger.LogInformation("Token usage - Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                reviewResponse.TokenUsage.PromptTokens,
                reviewResponse.TokenUsage.CompletionTokens,
                reviewResponse.TokenUsage.TotalTokens);
        }

        // Filter by minimum severity
        var filteredBySeverity = reviewResponse.Comments
            .Where(c => (int)c.Severity >= _llmOptions.MinSeverityLevel)
            .ToList();

        _logger.LogInformation("{FilteredCount} comments after severity filtering (min: {MinSeverity})",
            filteredBySeverity.Count, _llmOptions.MinSeverityLevel);

        // Get existing threads for duplicate detection
        var existingThreads = await _adoService.GetPullRequestThreadsAsync(
            request.OrganizationUrl,
            request.ProjectId,
            request.RepositoryId,
            request.PullRequestId,
            cancellationToken);

        // Filter duplicates
        var uniqueComments = _codeAnalysisService.FilterDuplicateComments(
            filteredBySeverity,
            existingThreads,
            _adoOptions.BotSignature);

        _logger.LogInformation("{UniqueCount} unique comments after duplicate filtering", uniqueComments.Count);

        // Post comments to PR
        var postedCount = 0;
        foreach (var comment in uniqueComments)
        {
            var thread = await PostCommentAsync(request, comment, cancellationToken);
            if (thread != null)
            {
                postedCount++;
            }
        }

        _logger.LogInformation("Posted {PostedCount} comments to PR #{PullRequestId}",
            postedCount, request.PullRequestId);
    }

    private async Task<CommentThread?> PostCommentAsync(
        CodeReviewRequest request,
        ReviewComment comment,
        CancellationToken cancellationToken)
    {
        var content = FormatCommentContent(comment);

        var threadRequest = new CreateThreadRequest
        {
            Comments =
            [
                new CreateComment
                {
                    Content = content,
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

        return await _adoService.CreateCommentThreadAsync(
            request.OrganizationUrl,
            request.ProjectId,
            request.RepositoryId,
            request.PullRequestId,
            threadRequest,
            cancellationToken);
    }

    private string FormatCommentContent(ReviewComment comment)
    {
        var severityEmoji = comment.Severity switch
        {
            ReviewSeverity.Info => "ℹ️",
            ReviewSeverity.Minor => "💡",
            ReviewSeverity.Major => "⚠️",
            ReviewSeverity.Critical => "🚨",
            ReviewSeverity.Blocker => "🛑",
            _ => "📝"
        };

        var content = $"{severityEmoji} **[{comment.Category}]** {comment.Message}";

        if (!string.IsNullOrEmpty(comment.Suggestion))
        {
            content += $"\n\n**Suggestion:** {comment.Suggestion}";
        }

        if (!string.IsNullOrEmpty(comment.SuggestedCode))
        {
            content += $"\n\n```\n{comment.SuggestedCode}\n```";
        }

        // Add bot signature for duplicate detection
        content += $"\n\n{_adoOptions.BotSignature}";

        return content;
    }
}
