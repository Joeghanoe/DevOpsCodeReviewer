namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for OverviewAgent structured output.
/// </summary>
public class OverviewLlmResponse
{
    /// <summary>
    /// Executive summary of what the PR accomplishes (2-4 sentences).
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
    /// Confidence score 1-5 (MUST be clamped to this range).
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

    /// <summary>
    /// Optional flags for comment quality issues detected.
    /// </summary>
    public List<string>? ValidationWarnings { get; set; }
}
