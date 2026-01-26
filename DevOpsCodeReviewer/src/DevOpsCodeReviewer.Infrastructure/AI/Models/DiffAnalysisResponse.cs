namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for DiffAnalyzerAgent structured output.
/// Used for analyzing code diffs and identifying areas of concern.
/// </summary>
public class DiffAnalysisResponse
{
    /// <summary>
    /// List of identified concerns in the diff. [OPTIONAL - empty list if none]
    /// Entries with missing required fields are skipped.
    /// </summary>
    public List<DiffConcern>? Concerns { get; set; }

    /// <summary>
    /// List of areas that need review focus. [OPTIONAL - empty list if none]
    /// Entries with missing required fields are skipped.
    /// </summary>
    public List<DiffFocusArea>? FocusAreas { get; set; }

    /// <summary>
    /// Brief summary of what the changes accomplish. [OPTIONAL - defaults to empty string]
    /// </summary>
    public string? Summary { get; set; }
}

/// <summary>
/// A concern identified in the diff that needs attention.
/// </summary>
public class DiffConcern
{
    /// <summary>
    /// File path or area where the concern is located. [REQUIRED - entry skipped if missing]
    /// </summary>
    public string? Area { get; set; }

    /// <summary>
    /// Severity level: Low, Medium, or High. [OPTIONAL - defaults to Medium]
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// Explanation of why this is a concern. [REQUIRED - entry skipped if missing]
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// An area that needs focused review attention.
/// </summary>
public class DiffFocusArea
{
    /// <summary>
    /// File path or area that needs focus. [REQUIRED - entry skipped if missing]
    /// </summary>
    public string? Area { get; set; }

    /// <summary>
    /// Reason why this area needs focused review. [OPTIONAL]
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Suggested review depth: Surface, Standard, or Deep. [OPTIONAL - defaults to Standard]
    /// </summary>
    public string? SuggestedReviewDepth { get; set; }
}
