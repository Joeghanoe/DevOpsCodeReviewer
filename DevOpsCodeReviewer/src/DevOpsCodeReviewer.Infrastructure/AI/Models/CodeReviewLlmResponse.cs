namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for CodeReviewAgent structured output.
/// Named with "Llm" suffix to avoid collision with DevOpsCodeReviewer.Core.Models.CodeReviewResponse
/// </summary>
public class CodeReviewLlmResponse
{
    public List<ReviewCommentInfo>? Comments { get; set; }
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
