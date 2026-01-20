using Azure.Messaging.ServiceBus;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace DevOpsCodeReviewer.Functions.Functions;

/// <summary>
/// Health check endpoint for monitoring and load balancer probes.
/// </summary>
public class HealthCheck
{
    private readonly ServiceBusOptions _serviceBusOptions;
    private readonly LlmOptions _llmOptions;
    private readonly AzureDevOpsOptions _adoOptions;
    private readonly ILogger<HealthCheck> _logger;

    public HealthCheck(
        IOptions<ServiceBusOptions> serviceBusOptions,
        IOptions<LlmOptions> llmOptions,
        IOptions<AzureDevOpsOptions> adoOptions,
        ILogger<HealthCheck> logger)
    {
        _serviceBusOptions = serviceBusOptions.Value;
        _llmOptions = llmOptions.Value;
        _adoOptions = adoOptions.Value;
        _logger = logger;
    }

    [Function("HealthCheck")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, HealthCheckResult>();
        var overallHealthy = true;

        // Check Service Bus connectivity
        var serviceBusHealth = await CheckServiceBusAsync(cancellationToken);
        checks["serviceBus"] = serviceBusHealth;
        if (!serviceBusHealth.Healthy) overallHealthy = false;

        // Check configuration
        var configHealth = CheckConfiguration();
        checks["configuration"] = configHealth;
        if (!configHealth.Healthy) overallHealthy = false;

        var response = new HealthCheckResponse
        {
            Status = overallHealthy ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Version = GetVersion(),
            Checks = checks
        };

        _logger.LogInformation("Health check completed: {Status}", response.Status);

        return new OkObjectResult(response);
    }

    [Function("HealthCheckLive")]
    public IActionResult RunLive(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/live")] HttpRequest req)
    {
        // Simple liveness probe - just return 200 if the function is running
        return new OkObjectResult(new { status = "Alive", timestamp = DateTime.UtcNow });
    }

    [Function("HealthCheckReady")]
    public async Task<IActionResult> RunReady(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ready")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        // Readiness probe - check if dependencies are available
        var serviceBusHealth = await CheckServiceBusAsync(cancellationToken);

        if (serviceBusHealth.Healthy)
        {
            return new OkObjectResult(new { status = "Ready", timestamp = DateTime.UtcNow });
        }

        return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
    }

    private async Task<HealthCheckResult> CheckServiceBusAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(_serviceBusOptions.ConnectionString))
            {
                return new HealthCheckResult
                {
                    Healthy = false,
                    Message = "Connection string not configured"
                };
            }

            // Try to create a client and check queue properties
            await using var client = new ServiceBusClient(_serviceBusOptions.ConnectionString);
            await using var sender = client.CreateSender(_serviceBusOptions.QueueName);

            // Just creating the sender validates the connection
            return new HealthCheckResult
            {
                Healthy = true,
                Message = "Connected",
                Details = new { queue = _serviceBusOptions.QueueName }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Service Bus health check failed");
            return new HealthCheckResult
            {
                Healthy = false,
                Message = ex.Message
            };
        }
    }

    private HealthCheckResult CheckConfiguration()
    {
        var issues = new List<string>();

        if (string.IsNullOrEmpty(_adoOptions.OrganizationUrl))
        {
            issues.Add("AzureDevOps:OrganizationUrl not configured");
        }

        if (string.IsNullOrEmpty(_llmOptions.Endpoint))
        {
            issues.Add("Llm:Endpoint not configured");
        }

        if (string.IsNullOrEmpty(_llmOptions.DeploymentName))
        {
            issues.Add("Llm:DeploymentName not configured");
        }

        if (issues.Count > 0)
        {
            return new HealthCheckResult
            {
                Healthy = false,
                Message = "Configuration issues found",
                Details = new { issues }
            };
        }

        return new HealthCheckResult
        {
            Healthy = true,
            Message = "All required settings configured"
        };
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, HealthCheckResult> Checks { get; set; } = [];
}

public class HealthCheckResult
{
    public bool Healthy { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
}
