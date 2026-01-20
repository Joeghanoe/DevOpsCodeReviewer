using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DevOpsCodeReviewer.Functions.Configuration;
using DevOpsCodeReviewer.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        // Bind configuration sections
        services.Configure<AppSettings>(configuration);
        services.Configure<AzureDevOpsOptions>(configuration.GetSection("AzureDevOps"));
        services.Configure<LlmOptions>(configuration.GetSection("Llm"));
        services.Configure<ServiceBusOptions>(configuration.GetSection("ServiceBus"));
        services.Configure<WebhookOptions>(configuration.GetSection("Webhook"));
        services.Configure<ReviewOutputOptions>(configuration.GetSection("ReviewOutput"));

        // Register Key Vault client
        var keyVaultUrl = configuration["KeyVault:VaultUrl"];
        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            services.AddSingleton(sp =>
            {
                var credential = new DefaultAzureCredential();
                return new SecretClient(new Uri(keyVaultUrl), credential);
            });
        }

        // Configure HttpClient with retry policy for Azure DevOps
        services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(GetRetryPolicy());

        // Register services
        services.AddSingleton<ICodeAnalysisService, CodeAnalysisService>();
        services.AddSingleton<IPromptService, PromptService>();
        services.AddSingleton<ILlmService, LlmService>();

        // Register review output service based on build configuration
#if DEBUG
        services.AddSingleton<IReviewOutputService, LocalFileOutputService>();
#else
        services.AddSingleton<IReviewOutputService, AzureDevOpsOutputService>();
#endif

        // Application Insights
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
