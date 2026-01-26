using Azure.Messaging.ServiceBus;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps.Models;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using DevOpsCodeReviewer.Infrastructure.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DevOpsCodeReviewer.Functions.Functions;

/// <summary>
/// HTTP trigger function for Azure DevOps webhooks.
/// Authentication is handled by Azure Functions host using function keys.
/// </summary>
public class WebhookHandler
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusOptions _serviceBusOptions;
    private readonly AzureDevOpsOptions _adoOptions;
    private readonly ILogger<WebhookHandler> _logger;

    private static readonly HashSet<string> SupportedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "git.pullrequest.created",
        "git.pullrequest.updated"
    };

    public WebhookHandler(
        IOptions<ServiceBusOptions> serviceBusOptions,
        IOptions<AzureDevOpsOptions> adoOptions,
        ILogger<WebhookHandler> logger)
    {
        _serviceBusOptions = serviceBusOptions.Value;
        _adoOptions = adoOptions.Value;
        _logger = logger;

        _serviceBusClient = new ServiceBusClient(_serviceBusOptions.ConnectionString);
    }

    [Function("WebhookHandler")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "webhook")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        _logger.LogInformation("Received webhook request");

        // Parse the payload
        PullRequestPayload? payload;
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
            payload = JsonSerializer.Deserialize<PullRequestPayload>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse webhook payload");
            return new BadRequestObjectResult("Invalid JSON payload");
        }

        // Validate the payload structure
        var validationResult = WebhookPayloadValidator.Validate(payload);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Invalid webhook payload: {Errors}", validationResult.ErrorMessage);
            return new BadRequestObjectResult(new
            {
                message = "Invalid payload",
                errors = validationResult.Errors
            });
        }

        // After validation passes, payload and its required fields are guaranteed non-null
        // This assertion helps the compiler understand the null-safety
        if (payload == null)
        {
            throw new InvalidOperationException("Validation passed but payload is null - this should never happen");
        }

        // Validate event type
        if (!SupportedEventTypes.Contains(payload.EventType))
        {
            _logger.LogInformation("Ignoring unsupported event type: {EventType}", payload.EventType);
            return new OkObjectResult(new { message = "Event type not supported", eventType = payload.EventType });
        }

        // Validate PR status (only process active PRs)
        if (!payload.Resource.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Ignoring non-active PR: {Status}", payload.Resource.Status);
            return new OkObjectResult(new { message = "PR is not active", status = payload.Resource.Status });
        }

        // Skip draft PRs
        if (payload.Resource.IsDraft)
        {
            _logger.LogInformation("Ignoring draft PR: {PullRequestId}", payload.Resource.PullRequestId);
            return new OkObjectResult(new { message = "Draft PRs are not reviewed" });
        }

        // Build the code review request
        var organizationUrl = ExtractOrganizationUrl(payload);
        var reviewRequest = new CodeReviewRequest
        {
            OrganizationUrl = organizationUrl,
            ProjectId = payload.Resource.Repository.Project.Id,
            ProjectName = payload.Resource.Repository.Project.Name,
            RepositoryId = payload.Resource.Repository.Id,
            RepositoryName = payload.Resource.Repository.Name,
            PullRequestId = payload.Resource.PullRequestId,
            Title = payload.Resource.Title,
            Description = payload.Resource.Description,
            SourceBranch = payload.Resource.SourceRefName,
            TargetBranch = payload.Resource.TargetRefName,
            AuthorName = payload.Resource.CreatedBy?.DisplayName ?? "Unknown",
            CorrelationId = correlationId,
            ReceivedAt = DateTime.UtcNow,
            EventType = payload.EventType
        };

        // Send to Service Bus
        try
        {
            await SendToServiceBusAsync(reviewRequest, cancellationToken);

            _logger.LogInformation(
                "Queued review request for PR #{PullRequestId} in {Project}/{Repository}",
                reviewRequest.PullRequestId,
                reviewRequest.ProjectName,
                reviewRequest.RepositoryName);

            return new AcceptedResult(
                string.Empty,
                new
                {
                    message = "Review request queued",
                    correlationId,
                    pullRequestId = reviewRequest.PullRequestId
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue review request");
            return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private string ExtractOrganizationUrl(PullRequestPayload payload)
    {
        // Try to get from resource containers
        var baseUrl = payload.ResourceContainers?.Account?.BaseUrl
            ?? payload.ResourceContainers?.Collection?.BaseUrl;

        if (!string.IsNullOrEmpty(baseUrl))
        {
            return baseUrl.TrimEnd('/');
        }

        // Fall back to configured organization URL
        return _adoOptions.OrganizationUrl;
    }

    private async Task SendToServiceBusAsync(CodeReviewRequest request, CancellationToken cancellationToken)
    {
        await using var sender = _serviceBusClient.CreateSender(_serviceBusOptions.QueueName);

        var messageBody = JsonSerializer.Serialize(request);
        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = request.CorrelationId,
            CorrelationId = request.CorrelationId,
            ContentType = "application/json",
            Subject = $"PR-{request.PullRequestId}"
        };

        // Add custom properties for filtering/routing
        message.ApplicationProperties["projectId"] = request.ProjectId;
        message.ApplicationProperties["repositoryId"] = request.RepositoryId;
        message.ApplicationProperties["pullRequestId"] = request.PullRequestId;
        message.ApplicationProperties["eventType"] = request.EventType;

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
