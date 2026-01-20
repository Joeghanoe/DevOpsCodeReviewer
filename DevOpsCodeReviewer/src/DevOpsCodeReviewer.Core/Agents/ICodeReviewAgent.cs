using DevOpsCodeReviewer.Core.Models;

namespace DevOpsCodeReviewer.Core.Agents;

/// <summary>
/// Input for the Code Review Agent.
/// </summary>
public class CodeReviewInput
{
    /// <summary>
    /// The diff analysis from the Diff Analyzer Agent.
    /// </summary>
    public DiffAnalysis Analysis { get; set; } = new();

    /// <summary>
    /// The original code review request.
    /// </summary>
    public CodeReviewRequest Request { get; set; } = new();

    /// <summary>
    /// Execution context with PR metadata.
    /// </summary>
    public AgentExecutionContext ExecutionContext { get; set; } = new();
}

/// <summary>
/// Agent responsible for performing the actual code review with full context.
/// </summary>
/// <remarks>
/// This agent:
/// - Reviews code with awareness of existing patterns
/// - Generates contextual suggestions (not generic advice)
/// - References related code when making suggestions
/// - Prioritizes issues based on severity and impact
/// </remarks>
public interface ICodeReviewAgent : IAgent<CodeReviewInput, CodeReviewResponse>
{
}
