namespace DevOpsCodeReviewer.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Azure Service Bus.
/// </summary>
public class ServiceBusOptions
{
    /// <summary>
    /// Service Bus connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Queue name for code review requests.
    /// </summary>
    public string QueueName { get; set; } = "codereview-requests";
}

/// <summary>
/// Configuration options for webhook handling.
/// </summary>
public class WebhookOptions
{
    /// <summary>
    /// Secret for webhook validation.
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}

/// <summary>
/// Configuration options for review output.
/// </summary>
public class ReviewOutputOptions
{
    /// <summary>
    /// Output directory for local file output.
    /// </summary>
    public string OutputDirectory { get; set; } = "Reviews";

    /// <summary>
    /// Whether to log output to console.
    /// </summary>
    public bool LogToConsole { get; set; } = true;
}
