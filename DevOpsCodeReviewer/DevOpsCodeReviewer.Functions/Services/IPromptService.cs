namespace DevOpsCodeReviewer.Functions.Services;

/// <summary>
/// Interface for loading and combining code review prompts.
/// </summary>
public interface IPromptService
{
    /// <summary>
    /// Gets the combined system prompt based on the files being reviewed.
    /// Combines the generic prompt with a language-specific prompt if available.
    /// </summary>
    /// <param name="filePaths">The paths of files being reviewed.</param>
    /// <returns>The combined system prompt.</returns>
    string GetSystemPrompt(IEnumerable<string> filePaths);
}
