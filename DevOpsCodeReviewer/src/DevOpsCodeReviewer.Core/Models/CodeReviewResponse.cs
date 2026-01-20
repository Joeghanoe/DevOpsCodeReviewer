using System.Text.Json.Serialization;

namespace DevOpsCodeReviewer.Core.Models;

/// <summary>
/// Response containing code review comments from the agent pipeline.
/// </summary>
public class CodeReviewResponse
{
    [JsonPropertyName("comments")]
    public List<ReviewComment> Comments { get; set; } = [];

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("filesReviewed")]
    public int FilesReviewed { get; set; }

    [JsonPropertyName("linesReviewed")]
    public int LinesReviewed { get; set; }

    [JsonPropertyName("tokenUsage")]
    public TokenUsage? TokenUsage { get; set; }

    /// <summary>
    /// Context information gathered about the codebase.
    /// </summary>
    [JsonPropertyName("contextSummary")]
    public ContextSummary? ContextSummary { get; set; }
}

/// <summary>
/// Token usage statistics from LLM calls.
/// </summary>
public class TokenUsage
{
    [JsonPropertyName("promptTokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completionTokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// Summary of context gathered from the codebase.
/// </summary>
public class ContextSummary
{
    /// <summary>
    /// Number of related files discovered and analyzed.
    /// </summary>
    public int RelatedFilesCount { get; set; }

    /// <summary>
    /// Patterns identified in the codebase.
    /// </summary>
    public List<string> IdentifiedPatterns { get; set; } = [];

    /// <summary>
    /// Key architectural insights.
    /// </summary>
    public List<string> ArchitecturalInsights { get; set; } = [];
}
