using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Core.Workflows;
using DevOpsCodeReviewer.Infrastructure.AI;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DevOpsCodeReviewer.Infrastructure.Workflows;

/// <summary>
/// Orchestrates the sequential agent pipeline for code review.
/// </summary>
public class CodeReviewWorkflow : ICodeReviewWorkflow
{
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<CodeReviewWorkflow> _logger;

    public CodeReviewWorkflow(
        IAgentFactory agentFactory,
        ILogger<CodeReviewWorkflow> logger)
    {
        _agentFactory = agentFactory;
        _logger = logger;
    }

    public async Task<CodeReviewResponse> ExecuteAsync(
        CodeReviewRequest request,
        List<FileContent> changedFiles,
        CancellationToken cancellationToken = default)
    {
        var workflowStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("[Workflow] Starting code review workflow for PR #{PullRequestId}", request.PullRequestId);

        var executionContext = new AgentExecutionContext
        {
            CorrelationId = request.CorrelationId,
            PullRequestId = request.PullRequestId,
            OrganizationUrl = request.OrganizationUrl,
            ProjectId = request.ProjectId,
            RepositoryId = request.RepositoryId
        };

        try
        {
            // Create agents
            var contextAgent = _agentFactory.CreateContextGatheringAgent();
            var analyzerAgent = _agentFactory.CreateDiffAnalyzerAgent();
            var reviewAgent = _agentFactory.CreateCodeReviewAgent();
            var overviewAgent = _agentFactory.CreateOverviewAgent();

            // ═══════════════════════════════════════════════════════════════
            // STEP 1: Context Gathering
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("[Workflow] Step 1: Context Gathering");
            var contextStopwatch = Stopwatch.StartNew();

            var contextInput = new ContextGatheringInput
            {
                ChangedFiles = changedFiles,
                Context = executionContext
            };

            var contextResult = await contextAgent.ExecuteAsync(contextInput, cancellationToken);
            contextStopwatch.Stop();

            _logger.LogInformation("[Workflow] Context gathering completed in {ElapsedMs}ms. Found {RelatedCount} related files, {PatternCount} patterns",
                contextStopwatch.ElapsedMilliseconds,
                contextResult.RelatedFiles.Count,
                contextResult.IdentifiedPatterns.Count);

            // ═══════════════════════════════════════════════════════════════
            // STEP 2: Diff Analysis
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("[Workflow] Step 2: Diff Analysis");
            var analysisStopwatch = Stopwatch.StartNew();

            var analysisInput = new DiffAnalyzerInput
            {
                Context = contextResult,
                ExecutionContext = executionContext
            };

            var analysisResult = await analyzerAgent.ExecuteAsync(analysisInput, cancellationToken);
            analysisStopwatch.Stop();

            _logger.LogInformation("[Workflow] Diff analysis completed in {ElapsedMs}ms. Category: {Category}, Concerns: {ConcernCount}",
                analysisStopwatch.ElapsedMilliseconds,
                analysisResult.ChangeCategory,
                analysisResult.AreasOfConcern.Count);

            // ═══════════════════════════════════════════════════════════════
            // STEP 3: Code Review
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("[Workflow] Step 3: Code Review");
            var reviewStopwatch = Stopwatch.StartNew();

            var reviewInput = new CodeReviewInput
            {
                Analysis = analysisResult,
                Request = request,
                ExecutionContext = executionContext
            };

            var reviewResult = await reviewAgent.ExecuteAsync(reviewInput, cancellationToken);
            reviewStopwatch.Stop();

            _logger.LogInformation("[Workflow] Code review completed in {ElapsedMs}ms. Generated {CommentCount} comments",
                reviewStopwatch.ElapsedMilliseconds,
                reviewResult.Comments.Count);

            // ═══════════════════════════════════════════════════════════════
            // STEP 4: Overview Generation
            // ═══════════════════════════════════════════════════════════════
            _logger.LogInformation("[Workflow] Step 4: Overview Generation");
            var overviewStopwatch = Stopwatch.StartNew();

            var overviewInput = new OverviewInput
            {
                Analysis = analysisResult,
                Comments = reviewResult.Comments,
                Request = request,
                ExecutionContext = executionContext
            };

            var overviewResult = await overviewAgent.ExecuteAsync(overviewInput, cancellationToken);
            overviewStopwatch.Stop();

            _logger.LogInformation("[Workflow] Overview generation completed in {ElapsedMs}ms. Risk: {Risk}, Confidence: {Confidence}/5",
                overviewStopwatch.ElapsedMilliseconds,
                overviewResult.RiskAssessment,
                overviewResult.ConfidenceScore);

            // Attach overview to review result
            reviewResult.Overview = overviewResult;

            // ═══════════════════════════════════════════════════════════════
            // COMPLETE
            // ═══════════════════════════════════════════════════════════════
            workflowStopwatch.Stop();

            _logger.LogInformation(
                "[Workflow] Pipeline completed in {TotalMs}ms (Context: {ContextMs}ms, Analysis: {AnalysisMs}ms, Review: {ReviewMs}ms, Overview: {OverviewMs}ms). Total comments: {CommentCount}",
                workflowStopwatch.ElapsedMilliseconds,
                contextStopwatch.ElapsedMilliseconds,
                analysisStopwatch.ElapsedMilliseconds,
                reviewStopwatch.ElapsedMilliseconds,
                overviewStopwatch.ElapsedMilliseconds,
                reviewResult.Comments.Count);

            return reviewResult;
        }
        catch (Exception ex)
        {
            workflowStopwatch.Stop();
            _logger.LogError(ex, "[Workflow] Pipeline failed after {ElapsedMs}ms", workflowStopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
