using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DevOpsCodeReviewer.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DevOpsCodeReviewer.Functions.Functions;

/// <summary>
/// Service Bus trigger function for handling dead-lettered messages.
/// </summary>
public class DeadLetterHandler
{
    private readonly ILogger<DeadLetterHandler> _logger;

    public DeadLetterHandler(ILogger<DeadLetterHandler> logger)
    {
        _logger = logger;
    }

    [Function("DeadLetterHandler")]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBus:QueueName%/$deadletterqueue", Connection = "ServiceBus:ConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Processing dead-lettered message: {MessageId}", message.MessageId);

        // Try to extract useful information from the message
        CodeReviewRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize<CodeReviewRequest>(
                message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            // Ignore deserialization errors for dead letters
        }

        // Log details about the failure
        _logger.LogError(
            "Dead-lettered code review request - " +
            "MessageId: {MessageId}, " +
            "CorrelationId: {CorrelationId}, " +
            "PullRequestId: {PullRequestId}, " +
            "DeadLetterReason: {DeadLetterReason}, " +
            "DeadLetterErrorDescription: {DeadLetterErrorDescription}, " +
            "DeliveryCount: {DeliveryCount}, " +
            "EnqueuedTime: {EnqueuedTime}",
            message.MessageId,
            request?.CorrelationId ?? message.CorrelationId,
            request?.PullRequestId ?? 0,
            message.DeadLetterReason,
            message.DeadLetterErrorDescription,
            message.DeliveryCount,
            message.EnqueuedTime);

        // Log application properties
        if (message.ApplicationProperties.Count > 0)
        {
            _logger.LogInformation("Message properties: {Properties}",
                JsonSerializer.Serialize(message.ApplicationProperties));
        }

        // In a production system, you might want to:
        // 1. Send an alert to a monitoring system
        // 2. Store the failed message in a blob for manual review
        // 3. Attempt to notify the PR author about the failure

        // For now, we'll just complete the message to remove it from the DLQ
        // This prevents the DLQ from growing indefinitely
        await messageActions.CompleteMessageAsync(message, cancellationToken);

        _logger.LogInformation("Dead-lettered message processed and removed: {MessageId}", message.MessageId);
    }
}
