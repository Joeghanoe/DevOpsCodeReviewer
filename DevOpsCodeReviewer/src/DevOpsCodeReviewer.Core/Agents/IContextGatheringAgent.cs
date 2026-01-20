using DevOpsCodeReviewer.Core.Models;

namespace DevOpsCodeReviewer.Core.Agents;

/// <summary>
/// Input for the Context Gathering Agent.
/// </summary>
public class ContextGatheringInput
{
    /// <summary>
    /// The changed files from the PR.
    /// </summary>
    public List<FileContent> ChangedFiles { get; set; } = [];

    /// <summary>
    /// Execution context with PR metadata.
    /// </summary>
    public AgentExecutionContext Context { get; set; } = new();
}

/// <summary>
/// Agent responsible for gathering contextual information about the codebase.
/// </summary>
/// <remarks>
/// This agent:
/// - Parses imports/usings from changed files
/// - Fetches related files (imported modules, interfaces, base classes)
/// - Identifies patterns in the codebase (error handling, logging, etc.)
/// - Provides architectural insights
/// </remarks>
public interface IContextGatheringAgent : IAgent<ContextGatheringInput, ContextResult>
{
}
