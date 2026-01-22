namespace DevOpsCodeReviewer.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Azure DevOps API integration.
/// </summary>
public class AzureDevOpsOptions
{
    /// <summary>
    /// The Azure DevOps organization URL (e.g., https://dev.azure.com/myorg).
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// Key Vault secret name containing the PAT token.
    /// </summary>
    public string PatSecretName { get; set; } = "ado-pat-token";

    /// <summary>
    /// API version to use for Azure DevOps REST API calls.
    /// </summary>
    public string ApiVersion { get; set; } = "7.1";

    /// <summary>
    /// Maximum number of files to include in a single review.
    /// </summary>
    public int MaxFilesPerReview { get; set; } = 50;

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
}
