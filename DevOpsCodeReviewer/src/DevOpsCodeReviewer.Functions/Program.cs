using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DevOpsCodeReviewer.Core.Configuration;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Core.Workflows;
using DevOpsCodeReviewer.Infrastructure.AI;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using DevOpsCodeReviewer.Infrastructure.Output;
using DevOpsCodeReviewer.Infrastructure.Workflows;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Polly;
using Polly.Extensions.Http;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // ═══════════════════════════════════════════════════════════════
        // Configuration Binding
        // ═══════════════════════════════════════════════════════════════
        services.Configure<AzureDevOpsOptions>(configuration.GetSection("AzureDevOps"));
        services.Configure<LlmOptions>(configuration.GetSection("Llm"));
        services.Configure<ServiceBusOptions>(configuration.GetSection("ServiceBus"));
        services.Configure<WebhookOptions>(configuration.GetSection("Webhook"));
        services.Configure<ReviewOutputOptions>(configuration.GetSection("ReviewOutput"));

        // Map AzureDevOpsOptions to CodeAnalysisOptions for Core services
        services.Configure<CodeAnalysisOptions>(opt =>
        {
            var adoOptions = configuration.GetSection("AzureDevOps").Get<AzureDevOpsOptions>();
            if (adoOptions != null)
            {
                opt.IncludedExtensions = adoOptions.IncludedExtensions;
                opt.ExcludedPatterns = adoOptions.ExcludedPatterns;
                opt.MaxFileSizeBytes = adoOptions.MaxFileSizeBytes;
                opt.BotSignature = adoOptions.BotSignature;
            }

            var llmOptions = configuration.GetSection("Llm").Get<LlmOptions>();
            if (llmOptions != null)
            {
                opt.MaxFilesPerRequest = llmOptions.MaxFilesPerRequest;
                opt.MaxLinesPerRequest = llmOptions.MaxLinesPerRequest;
            }
        });

        // ═══════════════════════════════════════════════════════════════
        // Key Vault
        // ═══════════════════════════════════════════════════════════════
        var keyVaultUrl = configuration["KeyVault:VaultUrl"];
        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            services.AddSingleton(sp =>
            {
                var credential = new DefaultAzureCredential();
                return new SecretClient(new Uri(keyVaultUrl), credential);
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // Core Services
        // ═══════════════════════════════════════════════════════════════
        services.AddSingleton<IDiffService, DiffService>();
        services.AddSingleton<ICodeAnalysisService, CodeAnalysisService>();

        // ═══════════════════════════════════════════════════════════════
        // Infrastructure - Azure DevOps
        // ═══════════════════════════════════════════════════════════════
        services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(GetRetryPolicy());

        // ═══════════════════════════════════════════════════════════════
        // Infrastructure - Microsoft Agent Framework & AI
        // ═══════════════════════════════════════════════════════════════
        services.AddSingleton<IChatClient>(sp =>
        {
            var llmOptions = configuration.GetSection("Llm").Get<LlmOptions>()
                ?? throw new InvalidOperationException("LLM configuration is required");

            // Get API key (direct or from KeyVault)
            var apiKey = llmOptions.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                var secretClient = sp.GetService<SecretClient>();
                if (secretClient != null && !string.IsNullOrEmpty(llmOptions.ApiKeySecretName))
                {
                    var secret = secretClient.GetSecret(llmOptions.ApiKeySecretName);
                    apiKey = secret.Value.Value;
                }
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("No API key configured. Set Llm:ApiKey or configure Key Vault.");
            }

            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(llmOptions.Endpoint),
                new System.ClientModel.ApiKeyCredential(apiKey));

            ChatClient chatClient = azureOpenAIClient.GetChatClient(llmOptions.DeploymentName);
            return chatClient.AsIChatClient();
        });

        services.AddSingleton<IPromptService, PromptService>();
        services.AddSingleton<IAgentFactory, AzureOpenAIAgentFactory>();

        // ═══════════════════════════════════════════════════════════════
        // Workflow
        // ═══════════════════════════════════════════════════════════════
        services.AddSingleton<ICodeReviewWorkflow, CodeReviewWorkflow>();

        // ═══════════════════════════════════════════════════════════════
        // Output Services
        // ═══════════════════════════════════════════════════════════════
#if DEBUG
        services.AddSingleton<IReviewOutputService, LocalFileOutputService>();
#else
        services.AddSingleton<IReviewOutputService, AzureDevOpsOutputService>();
#endif

        // ═══════════════════════════════════════════════════════════════
        // Telemetry
        // ═══════════════════════════════════════════════════════════════
        services.AddApplicationInsightsTelemetryWorkerService();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));

        // Set minimum log level
        logging.SetMinimumLevel(LogLevel.Information);

        // Filter out noisy Azure SDK logs
        logging.AddFilter("Azure.Core", LogLevel.Warning);
        logging.AddFilter("Azure.Identity", LogLevel.Warning);
        logging.AddFilter("Microsoft.Agents.AI", LogLevel.Information);
    })
    .Build();

await host.RunAsync();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                // Logging handled by Polly's built-in logging
            });
}
