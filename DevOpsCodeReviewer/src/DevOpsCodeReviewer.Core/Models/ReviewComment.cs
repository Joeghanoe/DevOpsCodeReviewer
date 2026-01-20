using System.Text.Json.Serialization;

namespace DevOpsCodeReviewer.Core.Models;

/// <summary>
/// Individual code review comment.
/// </summary>
public class ReviewComment
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }

    [JsonPropertyName("endLineNumber")]
    public int? EndLineNumber { get; set; }

    [JsonPropertyName("category")]
    public ReviewCategory Category { get; set; }

    [JsonPropertyName("severity")]
    public ReviewSeverity Severity { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }

    [JsonPropertyName("suggestedCode")]
    public string? SuggestedCode { get; set; }

    /// <summary>
    /// SHA256 fingerprint for duplicate detection.
    /// </summary>
    [JsonIgnore]
    public string? Fingerprint { get; set; }

    /// <summary>
    /// Reference to related code that informs this suggestion.
    /// </summary>
    [JsonPropertyName("relatedCodeReference")]
    public string? RelatedCodeReference { get; set; }
}

/// <summary>
/// Categories for code review comments.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewCategory
{
    Bug,
    Security,
    Performance,
    Style,
    BestPractice,
    Maintainability,
    ErrorHandling,
    Documentation,
    Testing,
    Other
}

/// <summary>
/// Severity levels for code review comments.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewSeverity
{
    Info = 1,
    Minor = 2,
    Major = 3,
    Critical = 4,
    Blocker = 5
}
