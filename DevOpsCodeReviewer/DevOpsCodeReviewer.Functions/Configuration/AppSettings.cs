namespace DevOpsCodeReviewer.Functions.Configuration;

/// <summary>
/// Root configuration binding for all application settings.
/// </summary>
public class AppSettings
{
    public AzureDevOpsOptions AzureDevOps { get; set; } = new();
    public LlmOptions Llm { get; set; } = new();
    public ServiceBusOptions ServiceBus { get; set; } = new();
    public KeyVaultOptions KeyVault { get; set; } = new();
    public WebhookOptions Webhook { get; set; } = new();
}

/// <summary>
/// Service Bus configuration options.
/// </summary>
public class ServiceBusOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "codereview-requests";
}

/// <summary>
/// Key Vault configuration options.
/// </summary>
public class KeyVaultOptions
{
    public string VaultUrl { get; set; } = string.Empty;
}

/// <summary>
/// Webhook configuration options.
/// </summary>
public class WebhookOptions
{
    public string Secret { get; set; } = string.Empty;
}
