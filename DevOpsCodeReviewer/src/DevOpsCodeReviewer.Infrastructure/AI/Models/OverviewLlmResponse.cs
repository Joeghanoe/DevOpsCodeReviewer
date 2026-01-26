namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for OverviewAgent structured output.
/// Used for generating PR overview summaries with risk assessment.
/// If validation fails, LLM will be asked to retry with feedback.
/// </summary>
public class OverviewLlmResponse
{
    /// <summary>
    /// Executive summary of what the PR accomplishes (2-4 sentences).
    /// [REQUIRED - triggers retry if missing]
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Key changes identified in the PR.
    /// [OPTIONAL - empty list if none, entries with missing Description are skipped]
    /// </summary>
    public List<KeyChangeInfo>? KeyChanges { get; set; }

    /// <summary>
    /// Files with significant changes.
    /// [OPTIONAL - empty list if none, entries with missing FilePath are skipped]
    /// </summary>
    public List<ImportantFileInfo>? ImportantFiles { get; set; }

    /// <summary>
    /// Confidence score 1-5.
    /// [OPTIONAL - clamped to valid range, defaults to 3 if null]
    /// </summary>
    public int? ConfidenceScore { get; set; }

    /// <summary>
    /// Explanation of confidence score.
    /// [OPTIONAL]
    /// </summary>
    public string? ConfidenceRationale { get; set; }

    /// <summary>
    /// Risk assessment: safe, low-risk, medium-risk, high-risk, critical-risk.
    /// [REQUIRED - triggers retry if missing, normalized from common variations]
    /// Common variations handled: "low", "medium", "high", "critical" (without "-risk" suffix)
    /// </summary>
    public string? RiskAssessment { get; set; }

    /// <summary>
    /// Optional flags for comment quality issues detected by the LLM.
    /// [OPTIONAL - for LLM self-reporting of potential issues]
    /// </summary>
    public List<string>? ValidationWarnings { get; set; }
}
