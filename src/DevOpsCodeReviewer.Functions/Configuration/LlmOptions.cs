namespace DevOpsCodeReviewer.Functions.Configuration;

/// <summary>
/// Configuration options for Azure OpenAI / Foundry LLM integration.
/// </summary>
public class LlmOptions
{
    /// <summary>
    /// Azure OpenAI endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Model deployment name (e.g., gpt-4o, gpt-4-turbo).
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// API key for local development.
    /// In production, use ApiKeySecretName to retrieve from Key Vault.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Key Vault secret name containing the API key.
    /// </summary>
    public string ApiKeySecretName { get; set; } = "foundry-api-key";

    /// <summary>
    /// Maximum tokens for LLM response.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature for LLM responses (0.0 - 1.0).
    /// Lower values produce more deterministic outputs.
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    /// Maximum files to include per LLM request.
    /// </summary>
    public int MaxFilesPerRequest { get; set; } = 10;

    /// <summary>
    /// Maximum lines of code to include per LLM request.
    /// </summary>
    public int MaxLinesPerRequest { get; set; } = 2000;

    /// <summary>
    /// Maximum retries for LLM API calls.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds between retries.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Directory containing prompt templates.
    /// </summary>
    public string PromptsDirectory { get; set; } = "prompts";

    /// <summary>
    /// Enable structured JSON output mode.
    /// </summary>
    public bool UseStructuredOutput { get; set; } = true;

    /// <summary>
    /// Minimum severity level to report (1-5).
    /// 1 = Info, 2 = Minor, 3 = Major, 4 = Critical, 5 = Blocker
    /// </summary>
    public int MinSeverityLevel { get; set; } = 2;
}
