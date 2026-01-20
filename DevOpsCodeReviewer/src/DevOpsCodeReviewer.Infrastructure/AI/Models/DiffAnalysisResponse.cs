namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for DiffAnalyzerAgent structured output.
/// </summary>
public class DiffAnalysisResponse
{
    public List<DiffConcern>? Concerns { get; set; }
    public List<DiffFocusArea>? FocusAreas { get; set; }
    public string? Summary { get; set; }
}

public class DiffConcern
{
    public string? Area { get; set; }
    public string? Severity { get; set; }
    public string? Reason { get; set; }
}

public class DiffFocusArea
{
    public string? Area { get; set; }
    public string? Reason { get; set; }
    public string? SuggestedReviewDepth { get; set; }
}
