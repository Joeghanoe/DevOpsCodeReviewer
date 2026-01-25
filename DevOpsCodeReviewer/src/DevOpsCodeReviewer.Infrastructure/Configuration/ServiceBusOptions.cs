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