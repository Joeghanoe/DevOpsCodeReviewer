namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for CodeReviewAgent structured output.
/// Named with "Llm" suffix to avoid collision with DevOpsCodeReviewer.Core.Models.CodeReviewResponse
/// </summary>
public class CodeReviewLlmResponse
{
    /// <summary>
    /// High-level overview of the PR changes.
    /// </summary>
    public ReviewOverviewInfo? Overview { get; set; }
    
    public List<ReviewCommentInfo>? Comments { get; set; }
}

/// <summary>
/// High-level summary of the PR review, similar to Greptile's overview.
/// </summary>
public class ReviewOverviewInfo
{
    /// <summary>
    /// Executive summary of what the PR accomplishes.
    /// </summary>
    public string? Summary { get; set; }
    
    /// <summary>
    /// Key changes identified in the PR.
    /// </summary>
    public List<KeyChangeInfo>? KeyChanges { get; set; }
    
    /// <summary>
    /// Files with significant changes.
    /// </summary>
    public List<ImportantFileInfo>? ImportantFiles { get; set; }
    
    /// <summary>
    /// Confidence score 1-5.
    /// </summary>
    public int? ConfidenceScore { get; set; }
    
    /// <summary>
    /// Explanation of confidence score.
    /// </summary>
    public string? ConfidenceRationale { get; set; }
    
    /// <summary>
    /// Risk assessment: safe, low-risk, medium-risk, high-risk, critical-risk.
    /// </summary>
    public string? RiskAssessment { get; set; }
}

public class KeyChangeInfo
{
    public string? Description { get; set; }
    public string? Rationale { get; set; }
}

public class ImportantFileInfo
{
    public string? FilePath { get; set; }
    public int? Score { get; set; }
    public string? Description { get; set; }
}

public class ReviewCommentInfo
{
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    public string? Severity { get; set; }
    public string? Category { get; set; }
    public string? Message { get; set; }
    
    /// <summary>
    /// Concrete example of what could go wrong if the issue isn't fixed.
    /// </summary>
    public string? ImpactExample { get; set; }
    
    public string? Suggestion { get; set; }
    public string? RelatedPattern { get; set; }
}
