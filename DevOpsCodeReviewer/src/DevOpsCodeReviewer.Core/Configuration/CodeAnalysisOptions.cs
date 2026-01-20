namespace DevOpsCodeReviewer.Core.Configuration;

/// <summary>
/// Configuration options for code analysis operations.
/// </summary>
public class CodeAnalysisOptions
{
    /// <summary>
    /// File extensions to include in code review.
    /// </summary>
    public string[] IncludedExtensions { get; set; } =
    [
        ".cs", ".ts", ".tsx", ".js", ".jsx",
        ".py", ".java", ".go", ".rs",
        ".bicep", ".yaml", ".yml", ".json"
    ];

    /// <summary>
    /// File patterns to exclude from code review.
    /// </summary>
    public string[] ExcludedPatterns { get; set; } =
    [
        "node_modules/",
        "bun.lock",
        "package-lock.json",
        "yarn.lock",
        "pnpm-lock.yaml",
        "*.min.js",
        "*.min.css",
        "*.designer.cs",
        "*.generated.cs",
        "*.g.cs",
        "*.Designer.cs"
    ];

    /// <summary>
    /// Maximum file size in bytes to include in review.
    /// </summary>
    public int MaxFileSizeBytes { get; set; } = 100 * 1024; // 100KB

    /// <summary>
    /// Signature to identify bot comments for duplicate detection.
    /// </summary>
    public string BotSignature { get; set; } = "<!-- DevOpsCodeReviewer -->";

    /// <summary>
    /// Maximum files to include per LLM request.
    /// </summary>
    public int MaxFilesPerRequest { get; set; } = 10;

    /// <summary>
    /// Maximum lines of code to include per LLM request.
    /// </summary>
    public int MaxLinesPerRequest { get; set; } = 2000;

    /// <summary>
    /// Maximum number of files to include in a single review.
    /// </summary>
    public int MaxFilesPerReview { get; set; } = 50;
}
