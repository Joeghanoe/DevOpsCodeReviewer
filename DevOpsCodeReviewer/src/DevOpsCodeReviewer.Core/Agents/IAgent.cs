namespace DevOpsCodeReviewer.Core.Agents;

/// <summary>
/// Base interface for all agents in the code review pipeline.
/// </summary>
/// <typeparam name="TInput">The input type for the agent.</typeparam>
/// <typeparam name="TOutput">The output type for the agent.</typeparam>
public interface IAgent<TInput, TOutput>
{
    /// <summary>
    /// The name of the agent for logging and tracing.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the agent's task.
    /// </summary>
    /// <param name="input">The input data for the agent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output from the agent.</returns>
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Agent execution context with metadata.
/// </summary>
public class AgentExecutionContext
{
    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The pull request ID being processed.
    /// </summary>
    public int PullRequestId { get; set; }

    /// <summary>
    /// Organization URL for Azure DevOps API calls.
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// Project ID for Azure DevOps API calls.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Repository ID for Azure DevOps API calls.
    /// </summary>
    public string RepositoryId { get; set; } = string.Empty;

    /// <summary>
    /// The source commit ID of the PR.
    /// </summary>
    public string? SourceCommitId { get; set; }

    /// <summary>
    /// The target commit ID of the PR.
    /// </summary>
    public string? TargetCommitId { get; set; }
}

/// <summary>
/// Result wrapper for agent execution.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public class AgentResult<T>
{
    /// <summary>
    /// Whether the agent execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The result data.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Execution duration in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Token usage for this agent execution.
    /// </summary>
    public Models.TokenUsage? TokenUsage { get; set; }

    public static AgentResult<T> Ok(T data, long executionTimeMs = 0) => new()
    {
        Success = true,
        Data = data,
        ExecutionTimeMs = executionTimeMs
    };

    public static AgentResult<T> Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
