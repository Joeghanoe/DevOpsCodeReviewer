namespace DevOpsCodeReviewer.Functions.Configuration;

/// <summary>
/// Configuration options for local file output in Debug mode.
/// </summary>
public class ReviewOutputOptions
{
    /// <summary>
    /// Directory where review output files will be written.
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine("D:\\Prototypes\\DevOpsPipelineAgent\\DevOpsCodeReviewer\\Reviews");

    /// <summary>
    /// Whether to also log comments to the console.
    /// </summary>
    public bool LogToConsole { get; set; } = true;
}
