using System.Text.Json.Serialization;

namespace DevOpsCodeReviewer.Functions.Models;

/// <summary>
/// Individual code review comment from the LLM.
/// </summary>
public class ReviewComment
{
    /// <summary>
    /// Path to the file being commented on.
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number where the comment applies (1-based).
    /// </summary>
    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }

    /// <summary>
    /// End line number for multi-line comments (optional).
    /// </summary>
    [JsonPropertyName("endLineNumber")]
    public int? EndLineNumber { get; set; }

    /// <summary>
    /// Category of the comment.
    /// </summary>
    [JsonPropertyName("category")]
    public ReviewCategory Category { get; set; }

    /// <summary>
    /// Severity level of the issue (1-5).
    /// </summary>
    [JsonPropertyName("severity")]
    public ReviewSeverity Severity { get; set; }

    /// <summary>
    /// The review comment message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Suggested fix or improvement (optional).
    /// </summary>
    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }

    /// <summary>
    /// Code snippet for the suggestion (optional).
    /// </summary>
    [JsonPropertyName("suggestedCode")]
    public string? SuggestedCode { get; set; }

    /// <summary>
    /// SHA256 fingerprint for duplicate detection.
    /// </summary>
    [JsonIgnore]
    public string? Fingerprint { get; set; }
}

/// <summary>
/// Categories for code review comments.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewCategory
{
    /// <summary>
    /// Potential bugs or errors.
    /// </summary>
    Bug,

    /// <summary>
    /// Security vulnerabilities.
    /// </summary>
    Security,

    /// <summary>
    /// Performance issues.
    /// </summary>
    Performance,

    /// <summary>
    /// Code style and formatting.
    /// </summary>
    Style,

    /// <summary>
    /// Best practices and patterns.
    /// </summary>
    BestPractice,

    /// <summary>
    /// Code maintainability.
    /// </summary>
    Maintainability,

    /// <summary>
    /// Error handling issues.
    /// </summary>
    ErrorHandling,

    /// <summary>
    /// Documentation and comments.
    /// </summary>
    Documentation,

    /// <summary>
    /// Testing concerns.
    /// </summary>
    Testing,

    /// <summary>
    /// Other suggestions.
    /// </summary>
    Other
}

/// <summary>
/// Severity levels for code review comments.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewSeverity
{
    /// <summary>
    /// Informational, nice to know.
    /// </summary>
    Info = 1,

    /// <summary>
    /// Minor issue, should be fixed but not blocking.
    /// </summary>
    Minor = 2,

    /// <summary>
    /// Major issue, should be addressed before merge.
    /// </summary>
    Major = 3,

    /// <summary>
    /// Critical issue, must be fixed before merge.
    /// </summary>
    Critical = 4,

    /// <summary>
    /// Blocker, PR cannot be merged with this issue.
    /// </summary>
    Blocker = 5
}
