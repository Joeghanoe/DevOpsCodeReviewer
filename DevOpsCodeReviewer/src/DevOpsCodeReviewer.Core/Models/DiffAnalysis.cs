namespace DevOpsCodeReviewer.Core.Models;

/// <summary>
/// Result from the Diff Analyzer Agent containing structured analysis of the changes.
/// </summary>
public class DiffAnalysis
{
    /// <summary>
    /// The context result from the previous agent.
    /// </summary>
    public ContextResult Context { get; set; } = new();

    /// <summary>
    /// Categorized areas of concern identified in the diff.
    /// </summary>
    public List<AreaOfConcern> AreasOfConcern { get; set; } = [];

    /// <summary>
    /// Overall change categorization.
    /// </summary>
    public ChangeCategory ChangeCategory { get; set; }

    /// <summary>
    /// Summary of the changes for the review agent.
    /// </summary>
    public string ChangeSummary { get; set; } = string.Empty;

    /// <summary>
    /// Files grouped by their review priority.
    /// </summary>
    public List<PrioritizedFile> PrioritizedFiles { get; set; } = [];

    /// <summary>
    /// Specific review focus areas based on the analysis.
    /// </summary>
    public List<ReviewFocus> ReviewFocusAreas { get; set; } = [];
}

/// <summary>
/// An area of concern identified in the code changes.
/// </summary>
public class AreaOfConcern
{
    /// <summary>
    /// The file containing the concern.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Starting line number.
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// Ending line number.
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// Type of concern.
    /// </summary>
    public ConcernType Type { get; set; }

    /// <summary>
    /// Description of the concern.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Severity of the concern.
    /// </summary>
    public ConcernSeverity Severity { get; set; }

    /// <summary>
    /// Related context files that might help understand this concern.
    /// </summary>
    public List<string> RelatedContextFiles { get; set; } = [];

    /// <summary>
    /// Existing patterns that might address this concern.
    /// </summary>
    public List<string> RelevantPatterns { get; set; } = [];
}

/// <summary>
/// Types of concerns in code changes.
/// </summary>
public enum ConcernType
{
    MissingErrorHandling,
    SecurityRisk,
    PerformanceImpact,
    InconsistentPattern,
    MissingValidation,
    PotentialNullReference,
    ResourceLeak,
    ThreadSafety,
    CodeDuplication,
    MissingTests,
    Other
}

/// <summary>
/// Severity levels for concerns.
/// </summary>
public enum ConcernSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Overall categorization of the change.
/// </summary>
public enum ChangeCategory
{
    NewFeature,
    BugFix,
    Refactoring,
    Performance,
    Security,
    Documentation,
    Testing,
    Configuration,
    Mixed
}

/// <summary>
/// A file with its review priority.
/// </summary>
public class PrioritizedFile
{
    /// <summary>
    /// The file content.
    /// </summary>
    public FileContent File { get; set; } = new();

    /// <summary>
    /// Priority level (1 = highest).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Reason for the priority assignment.
    /// </summary>
    public string PriorityReason { get; set; } = string.Empty;

    /// <summary>
    /// Areas of concern in this file.
    /// </summary>
    public List<AreaOfConcern> Concerns { get; set; } = [];
}

/// <summary>
/// A specific focus area for the review.
/// </summary>
public class ReviewFocus
{
    /// <summary>
    /// Title of the focus area.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description of what to look for.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Relevant existing patterns to consider.
    /// </summary>
    public List<CodePattern> RelevantPatterns { get; set; } = [];

    /// <summary>
    /// Files to focus on for this area.
    /// </summary>
    public List<string> RelevantFiles { get; set; } = [];
}
