using Azure.Messaging.ServiceBus;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Core.Workflows;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using DevOpsCodeReviewer.Infrastructure.Output;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DevOpsCodeReviewer.Functions.Functions;

/// <summary>
/// Service Bus trigger function for processing code reviews using the agent pipeline.
/// </summary>
public class CodeReviewProcessor
{
    private readonly IAzureDevOpsService _adoService;
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly ICodeReviewWorkflow _workflow;
    private readonly IReviewOutputService _reviewOutputService;
    private readonly AzureDevOpsOptions _adoOptions;
    private readonly ILogger<CodeReviewProcessor> _logger;

    public CodeReviewProcessor(
        IAzureDevOpsService adoService,
        ICodeAnalysisService codeAnalysisService,
        ICodeReviewWorkflow workflow,
        IReviewOutputService reviewOutputService,
        IOptions<AzureDevOpsOptions> adoOptions,
        ILogger<CodeReviewProcessor> logger)
    {
        _adoService = adoService;
        _codeAnalysisService = codeAnalysisService;
        _workflow = workflow;
        _reviewOutputService = reviewOutputService;
        _adoOptions = adoOptions.Value;
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

            _logger.LogInformation("Processing code review for PR #{PullRequestId} using agent pipeline",
                request.PullRequestId);

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
        // ═══════════════════════════════════════════════════════════════
        // Step 1: Get PR iterations and changed files
        // ═══════════════════════════════════════════════════════════════
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

        // Get changed files with diff content
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

        // Filter files
        var filteredFiles = _codeAnalysisService.FilterReviewableFiles(files);

        if (filteredFiles.Count == 0)
        {
            _logger.LogInformation("All files filtered out for PR #{PullRequestId}", request.PullRequestId);
            return;
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 2: Execute the Agent Pipeline
        // ═══════════════════════════════════════════════════════════════
        // The workflow orchestrates:
        //   1. ContextGatheringAgent - fetches related files, identifies patterns
        //   2. DiffAnalyzerAgent - categorizes changes, identifies concerns
        //   3. CodeReviewAgent - performs contextual review
        // ═══════════════════════════════════════════════════════════════
        var reviewResponse = await _workflow.ExecuteAsync(request, filteredFiles, cancellationToken);

        _logger.LogInformation("Agent pipeline completed. Generated {CommentCount} comments",
            reviewResponse.Comments.Count);

        if (reviewResponse.TokenUsage != null)
        {
            _logger.LogInformation("Token usage - Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                reviewResponse.TokenUsage.PromptTokens,
                reviewResponse.TokenUsage.CompletionTokens,
                reviewResponse.TokenUsage.TotalTokens);
        }

        if (reviewResponse.ContextSummary != null)
        {
            _logger.LogInformation("Context summary - Related files: {RelatedCount}, Patterns: {PatternCount}",
                reviewResponse.ContextSummary.RelatedFilesCount,
                reviewResponse.ContextSummary.IdentifiedPatterns.Count);
        }

        // ═══════════════════════════════════════════════════════════════
        // Step 3: Duplicate Detection
        // ═══════════════════════════════════════════════════════════════
        var existingThreads = await _adoService.GetPullRequestThreadsAsync(
            request.OrganizationUrl,
            request.ProjectId,
            request.RepositoryId,
            request.PullRequestId,
            cancellationToken);

        // Convert threads to ExistingComment format
        var existingComments = existingThreads
            .Where(t => t.ThreadContext?.FilePath != null)
            .Select(t => new ExistingComment
            {
                FilePath = t.ThreadContext!.FilePath!,
                LineNumber = t.ThreadContext.RightFileStart?.Line,
                Content = t.Comments.FirstOrDefault()?.Content ?? ""
            })
            .ToList();

        var uniqueComments = _codeAnalysisService.FilterDuplicateComments(
            reviewResponse.Comments,
            existingComments,
            _adoOptions.BotSignature);

        _logger.LogInformation("{UniqueCount} unique comments after duplicate filtering", uniqueComments.Count);

        // ═══════════════════════════════════════════════════════════════
        // Step 4: Post Comments
        // ═══════════════════════════════════════════════════════════════
        var postedCount = 0;
        foreach (var comment in uniqueComments)
        {
            var success = await PostCommentAsync(request, comment, cancellationToken);
            if (success)
            {
                postedCount++;
            }
        }

        // Finalize the review output
        await _reviewOutputService.FinalizeReviewAsync(request, postedCount, cancellationToken);

        _logger.LogInformation("Posted {PostedCount} comments to PR #{PullRequestId}",
            postedCount, request.PullRequestId);
    }

    private async Task<bool> PostCommentAsync(
        CodeReviewRequest request,
        ReviewComment comment,
        CancellationToken cancellationToken)
    {
        var content = FormatCommentContent(comment);

        return await _reviewOutputService.PublishCommentAsync(
            request,
            comment,
            content,
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

        // Add concrete impact example if available
        if (!string.IsNullOrEmpty(comment.ImpactExample))
        {
            content += $"\n\n**Example Impact:** {comment.ImpactExample}";
        }

        if (!string.IsNullOrEmpty(comment.Suggestion))
        {
            content += $"\n\n**Suggestion:** {comment.Suggestion}";
        }

        if (!string.IsNullOrEmpty(comment.SuggestedCode))
        {
            content += $"\n\n```\n{comment.SuggestedCode}\n```";
        }

        // Add reference to related code if available
        if (!string.IsNullOrEmpty(comment.RelatedCodeReference))
        {
            content += $"\n\n**Related:** {comment.RelatedCodeReference}";
        }

        // Add bot signature for duplicate detection
        content += $"\n\n{_adoOptions.BotSignature}";

        return content;
    }
}
