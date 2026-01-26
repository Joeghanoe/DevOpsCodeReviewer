namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for CodeReviewAgent structured output.
/// Note: Overview generation is handled by a separate OverviewAgent.
/// </summary>
public class CodeReviewLlmResponse
{
    /// <summary>
    /// List of review comments. [REQUIRED]
    /// If validation fails, LLM will be asked to retry with feedback.
    /// </summary>
    public List<ReviewCommentInfo>? Comments { get; set; }
}

/// <summary>
/// Key change description for overview.
/// </summary>
public class KeyChangeInfo
{
    /// <summary>
    /// Description of the key change. [OPTIONAL - skipped if null]
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Rationale for why this is a key change. [OPTIONAL]
    /// </summary>
    public string? Rationale { get; set; }
}

/// <summary>
/// Important file with significance score for overview.
/// </summary>
public class ImportantFileInfo
{
    /// <summary>
    /// Path to the file. [OPTIONAL - entry skipped if null]
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Significance score 1-5. [OPTIONAL - clamped to range, defaults to 3]
    /// </summary>
    public int? Score { get; set; }

    /// <summary>
    /// Description of why this file is important. [OPTIONAL]
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Individual review comment from the LLM.
/// Comments missing required fields are skipped rather than using defaults.
/// </summary>
public class ReviewCommentInfo
{
    /// <summary>
    /// Path to the file being commented on. [REQUIRED - comment skipped if missing]
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Line number where the comment applies (1-based). [REQUIRED - must be >= 1, comment skipped if missing/invalid]
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Severity level: Info, Minor, Major, Critical, or Blocker. [OPTIONAL - defaults to Minor]
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// Category: Bug, Security, Performance, Style, BestPractice, etc. [OPTIONAL - defaults to Other]
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// The review comment message. [REQUIRED - comment skipped if missing]
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Concrete example of what could go wrong if the issue isn't fixed. [OPTIONAL]
    /// </summary>
    public string? ImpactExample { get; set; }

    /// <summary>
    /// Suggested fix or improvement. [OPTIONAL - defaults to empty string]
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// Reference to related code pattern or file. [OPTIONAL]
    /// </summary>
    public string? RelatedPattern { get; set; }
}
