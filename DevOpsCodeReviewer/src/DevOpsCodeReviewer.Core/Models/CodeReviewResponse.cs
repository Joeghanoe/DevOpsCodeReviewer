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

    /// <summary>
    /// High-level overview of the PR review (Greptile-style).
    /// </summary>
    [JsonPropertyName("overview")]
    public ReviewOverview? Overview { get; set; }

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
/// High-level summary of the PR review, similar to Greptile's overview.
/// </summary>
public class ReviewOverview
{
    /// <summary>
    /// Executive summary of what the PR accomplishes.
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Key changes identified in the PR.
    /// </summary>
    [JsonPropertyName("keyChanges")]
    public List<KeyChange> KeyChanges { get; set; } = [];

    /// <summary>
    /// Files with significant changes.
    /// </summary>
    [JsonPropertyName("importantFiles")]
    public List<ImportantFile> ImportantFiles { get; set; } = [];

    /// <summary>
    /// Confidence score 1-5.
    /// </summary>
    [JsonPropertyName("confidenceScore")]
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Explanation of confidence score.
    /// </summary>
    [JsonPropertyName("confidenceRationale")]
    public string? ConfidenceRationale { get; set; }

    /// <summary>
    /// Risk assessment: safe, low-risk, medium-risk, high-risk, critical-risk.
    /// </summary>
    [JsonPropertyName("riskAssessment")]
    public string RiskAssessment { get; set; } = "low-risk";
}

public class KeyChange
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("rationale")]
    public string? Rationale { get; set; }
}

public class ImportantFile
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
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
