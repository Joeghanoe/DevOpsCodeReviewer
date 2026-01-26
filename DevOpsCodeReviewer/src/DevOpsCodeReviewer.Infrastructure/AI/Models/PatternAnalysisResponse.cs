namespace DevOpsCodeReviewer.Infrastructure.AI.Models;

/// <summary>
/// Response schema for ContextGatheringAgent structured output.
/// Used for identifying patterns and architectural insights in the codebase.
/// </summary>
public class PatternAnalysisResponse
{
    /// <summary>
    /// List of identified code patterns. [OPTIONAL - empty list if none]
    /// Entries with missing required fields are skipped.
    /// </summary>
    public List<PatternInfo>? Patterns { get; set; }

    /// <summary>
    /// List of architectural insights. [OPTIONAL - empty list if none]
    /// Entries with missing required fields are skipped.
    /// </summary>
    public List<InsightInfo>? Insights { get; set; }
}

/// <summary>
/// A code pattern identified in the codebase.
/// </summary>
public class PatternInfo
{
    /// <summary>
    /// Name of the pattern (e.g., "Repository Pattern", "ILogger Usage"). [REQUIRED - entry skipped if missing]
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of the pattern and how it's used. [REQUIRED - entry skipped if missing]
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category: ErrorHandling, Logging, Validation, DataAccess, DependencyInjection, etc. [OPTIONAL - defaults to Other]
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// List of files that implement this pattern. [OPTIONAL - defaults to empty list]
    /// </summary>
    public List<string>? ImplementingFiles { get; set; }

    /// <summary>
    /// Example code snippet demonstrating the pattern. [OPTIONAL - defaults to empty string]
    /// </summary>
    public string? ExampleCode { get; set; }
}

/// <summary>
/// An architectural insight about the codebase structure.
/// </summary>
public class InsightInfo
{
    /// <summary>
    /// Title of the insight. [REQUIRED - entry skipped if missing]
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Detailed description of the insight. [OPTIONAL]
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Impact or significance of this insight. [OPTIONAL - defaults to "Unknown"]
    /// </summary>
    public string? Impact { get; set; }
}
