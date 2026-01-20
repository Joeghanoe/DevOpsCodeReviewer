using DevOpsCodeReviewer.Core.Models;

namespace DevOpsCodeReviewer.Core.Workflows;

/// <summary>
/// Orchestrates the sequential agent pipeline for code review.
/// </summary>
public interface ICodeReviewWorkflow
{
    /// <summary>
    /// Executes the complete code review workflow.
    /// </summary>
    /// <param name="request">The code review request.</param>
    /// <param name="changedFiles">The changed files to review.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated code review response.</returns>
    Task<CodeReviewResponse> ExecuteAsync(
        CodeReviewRequest request,
        List<FileContent> changedFiles,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a workflow execution with detailed metrics.
/// </summary>
public class WorkflowResult
{
    /// <summary>
    /// The code review response.
    /// </summary>
    public CodeReviewResponse Response { get; set; } = new();

    /// <summary>
    /// Execution metrics for each agent.
    /// </summary>
    public List<AgentMetrics> AgentMetrics { get; set; } = [];

    /// <summary>
    /// Total workflow execution time in milliseconds.
    /// </summary>
    public long TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// Whether the workflow completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error details if the workflow failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Execution metrics for a single agent.
/// </summary>
public class AgentMetrics
{
    /// <summary>
    /// Name of the agent.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Token usage for this agent.
    /// </summary>
    public TokenUsage? TokenUsage { get; set; }

    /// <summary>
    /// Whether the agent executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
