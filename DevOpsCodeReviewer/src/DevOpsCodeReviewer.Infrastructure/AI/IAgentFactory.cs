using DevOpsCodeReviewer.Core.Agents;

namespace DevOpsCodeReviewer.Infrastructure.AI;

/// <summary>
/// Factory for creating configured agents.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Creates the Context Gathering Agent.
    /// </summary>
    IContextGatheringAgent CreateContextGatheringAgent();

    /// <summary>
    /// Creates the Diff Analyzer Agent.
    /// </summary>
    IDiffAnalyzerAgent CreateDiffAnalyzerAgent();

    /// <summary>
    /// Creates the Code Review Agent.
    /// </summary>
    ICodeReviewAgent CreateCodeReviewAgent();
}
