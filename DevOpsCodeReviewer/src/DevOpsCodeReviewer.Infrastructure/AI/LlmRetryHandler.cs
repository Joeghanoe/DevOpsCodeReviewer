using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;

namespace DevOpsCodeReviewer.Infrastructure.AI;

/// <summary>
/// Handles LLM retries with feedback loop when response validation fails.
/// Provides feedback to the LLM about validation errors and retries up to MaxRetries times.
/// </summary>
public class LlmRetryHandler
{
    private const int MaxRetries = 3;

    /// <summary>
    /// Executes an LLM call with retry logic and validation feedback.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="agent">The AI agent to execute.</param>
    /// <param name="initialPrompt">The initial prompt to send.</param>
    /// <param name="validator">Function to validate the response. Returns (isValid, errors).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Parsed response if successful, null if all retries failed.</returns>
    public static async Task<TResponse?> ExecuteWithRetryAsync<TResponse>(
        AIAgent agent,
        string initialPrompt,
        Func<string, TResponse?, (bool isValid, List<string> errors)> validator,
        ILogger logger, 
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        var messages = new List<ChatMessage> { new UserChatMessage(initialPrompt) };

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await agent.RunAsync(messages, cancellationToken: cancellationToken);
                var responseText = response.AsChatResponse().Text;

                // Try to parse
                TResponse? parsed = null;
                try
                {
                    parsed = JsonSerializer.Deserialize<TResponse>(responseText, JsonSerializerOptions.Web);
                }
                catch (JsonException ex)
                {
                    if (attempt < MaxRetries)
                    {
                        var feedback = $"Your response was not valid JSON. Error: {ex.Message}. " +
                                       "Please respond with valid JSON matching the expected schema.";
                        messages.Add(new AssistantChatMessage(responseText));
                        messages.Add(new UserChatMessage(feedback));

                        logger.LogWarning("LLM returned invalid JSON on attempt {Attempt}. Retrying...", attempt);
                        continue;
                    }

                    logger.LogError(ex, "LLM returned invalid JSON on final attempt {Attempt}", attempt);
                    return null;
                }

                // Validate the parsed response
                var (isValid, errors) = validator(responseText, parsed);

                if (isValid && parsed != null)
                {
                    logger.LogDebug("LLM response valid on attempt {Attempt}", attempt);
                    return parsed;
                }

                if (attempt < MaxRetries)
                {
                    // Build feedback message for retry
                    var feedback = BuildFeedbackMessage(errors, attempt);
                    messages.Add(new AssistantChatMessage(responseText));
                    messages.Add(new UserChatMessage(feedback));

                    logger.LogWarning(
                        "LLM response invalid on attempt {Attempt}/{Max}. Errors: {Errors}. Retrying...",
                        attempt, MaxRetries, string.Join("; ", errors));
                }
                else
                {
                    logger.LogError(
                        "LLM response invalid on final attempt {Attempt}. Errors: {Errors}",
                        attempt, string.Join("; ", errors));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "LLM call failed on attempt {Attempt}", attempt);

                if (attempt >= MaxRetries)
                {
                    return null;
                }
            }
        }

        logger.LogError("LLM failed to produce valid response after {Max} attempts", MaxRetries);
        return null;
    }

    private static string BuildFeedbackMessage(List<string> errors, int attempt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Your response (attempt {attempt}) had the following issues:");
        foreach (var error in errors)
        {
            sb.AppendLine($"- {error}");
        }
        sb.AppendLine();
        sb.AppendLine("Please fix these issues and respond again with valid JSON matching the expected schema.");
        return sb.ToString();
    }
}
