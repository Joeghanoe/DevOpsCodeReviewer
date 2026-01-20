using DevOpsCodeReviewer.Core.Models;

namespace DevOpsCodeReviewer.Core.Agents;

/// <summary>
/// Input for the Diff Analyzer Agent.
/// </summary>
public class DiffAnalyzerInput
{
    /// <summary>
    /// The context result from the Context Gathering Agent.
    /// </summary>
    public ContextResult Context { get; set; } = new();

    /// <summary>
    /// Execution context with PR metadata.
    /// </summary>
    public AgentExecutionContext ExecutionContext { get; set; } = new();
}

/// <summary>
/// Agent responsible for analyzing diff changes and identifying areas of concern.
/// </summary>
/// <remarks>
/// This agent:
/// - Categorizes changes (new feature, bug fix, refactor)
/// - Identifies potential issues (missing error handling, security risks)
/// - Prioritizes files for review
/// - Structures the analysis for the Code Review Agent
/// </remarks>
public interface IDiffAnalyzerAgent : IAgent<DiffAnalyzerInput, DiffAnalysis>
{
}
