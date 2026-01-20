namespace DevOpsCodeReviewer.Core.Models;

/// <summary>
/// Result from the Context Gathering Agent containing related files and patterns.
/// </summary>
public class ContextResult
{
    /// <summary>
    /// The original changed files from the PR.
    /// </summary>
    public List<FileContent> ChangedFiles { get; set; } = [];

    /// <summary>
    /// Related files discovered through import/reference analysis.
    /// </summary>
    public List<RelatedFile> RelatedFiles { get; set; } = [];

    /// <summary>
    /// Patterns identified in the codebase.
    /// </summary>
    public List<CodePattern> IdentifiedPatterns { get; set; } = [];

    /// <summary>
    /// Architecture insights discovered.
    /// </summary>
    public List<ArchitectureInsight> ArchitectureInsights { get; set; } = [];
}

/// <summary>
/// A file related to the changed files in the PR.
/// </summary>
public class RelatedFile
{
    /// <summary>
    /// Path to the related file.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Content of the related file.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Why this file is relevant to the review.
    /// </summary>
    public RelationshipType Relationship { get; set; }

    /// <summary>
    /// The changed file that references this file.
    /// </summary>
    public string ReferencedBy { get; set; } = string.Empty;
}

/// <summary>
/// Types of relationships between files.
/// </summary>
public enum RelationshipType
{
    /// <summary>
    /// File is imported/referenced by a changed file.
    /// </summary>
    Import,

    /// <summary>
    /// File defines an interface implemented by a changed file.
    /// </summary>
    Interface,

    /// <summary>
    /// File is a base class of a changed file.
    /// </summary>
    BaseClass,

    /// <summary>
    /// File contains similar patterns to the changed file.
    /// </summary>
    SimilarPattern,

    /// <summary>
    /// File is a configuration that affects the changed file.
    /// </summary>
    Configuration,

    /// <summary>
    /// File contains error handling patterns.
    /// </summary>
    ErrorHandling,

    /// <summary>
    /// File contains shared utilities.
    /// </summary>
    SharedUtility
}

/// <summary>
/// A pattern identified in the codebase.
/// </summary>
public class CodePattern
{
    /// <summary>
    /// Name of the pattern (e.g., "GlobalErrorHandler", "RepositoryPattern").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of how this pattern is used.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Files that implement this pattern.
    /// </summary>
    public List<string> ImplementingFiles { get; set; } = [];

    /// <summary>
    /// Example code snippet showing the pattern.
    /// </summary>
    public string? ExampleCode { get; set; }

    /// <summary>
    /// Category of the pattern.
    /// </summary>
    public PatternCategory Category { get; set; }
}

/// <summary>
/// Categories of code patterns.
/// </summary>
public enum PatternCategory
{
    ErrorHandling,
    Logging,
    Validation,
    DataAccess,
    DependencyInjection,
    Authentication,
    Configuration,
    Testing,
    Other
}

/// <summary>
/// An architectural insight about the codebase.
/// </summary>
public class ArchitectureInsight
{
    /// <summary>
    /// The insight title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Relevant files for this insight.
    /// </summary>
    public List<string> RelevantFiles { get; set; } = [];
}
