namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for ContextGatheringAgent structured output.
/// </summary>
public class PatternAnalysisResponse
{
    public List<PatternInfo>? Patterns { get; set; }
    public List<InsightInfo>? Insights { get; set; }
}

public class PatternInfo
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public List<string>? ImplementingFiles { get; set; }
    public string? ExampleCode { get; set; }
}

public class InsightInfo
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Impact { get; set; }
}
