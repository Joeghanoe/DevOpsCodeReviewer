using System.ClientModel;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Security.KeyVault.Secrets;
using DevOpsCodeReviewer.Functions.Configuration;
using DevOpsCodeReviewer.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DevOpsCodeReviewer.Functions.Services;

/// <summary>
/// Service for LLM-based code review.
/// </summary>
public class LlmService : ILlmService
{
    private readonly LlmOptions _options;
    private readonly SecretClient? _secretClient;
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly IPromptService _promptService;
    private readonly ILogger<LlmService> _logger;
    private ChatClient? _chatClient;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public LlmService(
        IOptions<LlmOptions> options,
        ICodeAnalysisService codeAnalysisService,
        IPromptService promptService,
        ILogger<LlmService> logger,
        SecretClient? secretClient = null)
    {
        _options = options.Value;
        _secretClient = secretClient;
        _codeAnalysisService = codeAnalysisService;
        _promptService = promptService;
        _logger = logger;
    }

    private async Task<ChatClient> GetChatClientAsync(CancellationToken cancellationToken)
    {
        if (_chatClient != null)
            return _chatClient;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_chatClient != null)
                return _chatClient;

            var apiKey = await GetApiKeyAsync(cancellationToken);
            var client = new AzureOpenAIClient(
                new Uri(_options.Endpoint),
                new ApiKeyCredential(apiKey));

            _chatClient = client.GetChatClient(_options.DeploymentName);
            return _chatClient;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<string> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        // Try direct configuration first (local development)
        if (!string.IsNullOrEmpty(_options.ApiKey))
            return _options.ApiKey;

        // Try Key Vault
        if (_secretClient != null && !string.IsNullOrEmpty(_options.ApiKeySecretName))
        {
            var secret = await _secretClient.GetSecretAsync(_options.ApiKeySecretName, cancellationToken: cancellationToken);
            return secret.Value.Value;
        }

        throw new InvalidOperationException("No API key configured. Set Llm:ApiKey or configure Key Vault.");
    }

    public async Task<CodeReviewResponse> ReviewCodeAsync(
        CodeReviewRequest request,
        List<FileContent> files,
        CancellationToken cancellationToken = default)
    {
        var chatClient = await GetChatClientAsync(cancellationToken);

        var systemPrompt = _promptService.GetSystemPrompt(files.Select(f => f.Path));
        var userPrompt = BuildUserPrompt(request, files);

        _logger.LogInformation("Reviewing {FileCount} files for PR #{PullRequestId}",
            files.Count, request.PullRequestId);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _options.MaxTokens,
            Temperature = _options.Temperature
        };

        // Enable JSON mode
        options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            "code_review_response",
            BinaryData.FromString(GetResponseSchema()),
            jsonSchemaIsStrict: true);

        var response = await ExecuteWithRetryAsync(async () =>
        {
            return await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        }, cancellationToken);

        var result = ParseResponse(response.Value);
        result.FilesReviewed = files.Count;
        result.LinesReviewed = files.Sum(f => f.LineCount);

        if (response.Value.Usage != null)
        {
            result.TokenUsage = new TokenUsage
            {
                PromptTokens = response.Value.Usage.InputTokenCount,
                CompletionTokens = response.Value.Usage.OutputTokenCount,
                TotalTokens = response.Value.Usage.TotalTokenCount
            };
        }

        _logger.LogInformation("Generated {CommentCount} comments for PR #{PullRequestId}",
            result.Comments.Count, request.PullRequestId);

        return result;
    }

    public async Task<CodeReviewResponse> ReviewCodeInChunksAsync(
        CodeReviewRequest request,
        List<List<FileContent>> fileChunks,
        CancellationToken cancellationToken = default)
    {
        var aggregatedResponse = new CodeReviewResponse();

        foreach (var chunk in fileChunks)
        {
            var chunkResponse = await ReviewCodeAsync(request, chunk, cancellationToken);

            aggregatedResponse.Comments.AddRange(chunkResponse.Comments);
            aggregatedResponse.FilesReviewed += chunkResponse.FilesReviewed;
            aggregatedResponse.LinesReviewed += chunkResponse.LinesReviewed;

            if (chunkResponse.TokenUsage != null)
            {
                aggregatedResponse.TokenUsage ??= new TokenUsage();
                aggregatedResponse.TokenUsage.PromptTokens += chunkResponse.TokenUsage.PromptTokens;
                aggregatedResponse.TokenUsage.CompletionTokens += chunkResponse.TokenUsage.CompletionTokens;
                aggregatedResponse.TokenUsage.TotalTokens += chunkResponse.TokenUsage.TotalTokens;
            }
        }

        return aggregatedResponse;
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var delay = _options.RetryDelayMs;

        while (true)
        {
            try
            {
                return await action();
            }
            catch (RequestFailedException ex) when (ex.Status == 429 || ex.Status >= 500)
            {
                retryCount++;
                if (retryCount >= _options.MaxRetries)
                {
                    _logger.LogError(ex, "Max retries ({MaxRetries}) exceeded", _options.MaxRetries);
                    throw;
                }

                _logger.LogWarning("LLM request failed with {Status}, retrying in {Delay}ms (attempt {Attempt}/{Max})",
                    ex.Status, delay, retryCount, _options.MaxRetries);

                await Task.Delay(delay, cancellationToken);
                delay *= 2; // Exponential backoff
            }
        }
    }

    private string BuildUserPrompt(CodeReviewRequest request, List<FileContent> files)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Pull Request Information");
        sb.AppendLine();
        sb.AppendLine($"**Title**: {request.Title}");
        if (!string.IsNullOrEmpty(request.Description))
        {
            sb.AppendLine($"**Description**: {request.Description}");
        }
        sb.AppendLine($"**Source Branch**: {request.SourceBranch}");
        sb.AppendLine($"**Target Branch**: {request.TargetBranch}");
        sb.AppendLine($"**Author**: {request.AuthorName}");
        sb.AppendLine();
        sb.AppendLine("## Changed Files (Diff Format)");
        sb.AppendLine();
        sb.AppendLine("Below are the changes in unified diff format. Lines starting with `+` are additions, `-` are deletions, and unmarked lines are context.");
        sb.AppendLine("The line numbers shown (e.g., `L42`) refer to the NEW file after changes. Use these exact line numbers in your comments.");
        sb.AppendLine();

        foreach (var file in files)
        {
            var language = _codeAnalysisService.GetLanguageFromPath(file.Path);
            sb.AppendLine($"### {file.Path} ({file.ChangeType})");
            sb.AppendLine();

            if (file.DiffHunks.Count > 0)
            {
                sb.AppendLine($"```{language}");
                sb.AppendLine(FormatDiffHunks(file.DiffHunks));
                sb.AppendLine("```");
            }
            else
            {
                // Fallback to full file with line numbers if no diff available
                sb.AppendLine($"```{language}");
                sb.AppendLine(AddLineNumbers(file.Content));
                sb.AppendLine("```");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Review the changes shown above. Focus ONLY on the added (+) and modified lines.");
        sb.AppendLine("Use the exact line numbers (L###) shown in the diff for your comments.");

        return sb.ToString();
    }

    private static string FormatDiffHunks(List<DiffHunk> hunks)
    {
        var sb = new StringBuilder();

        foreach (var hunk in hunks)
        {
            // Add hunk header similar to unified diff
            sb.AppendLine($"@@ -{hunk.OldStartLine},{hunk.OldLineCount} +{hunk.NewStartLine},{hunk.NewLineCount} @@");

            foreach (var line in hunk.Lines)
            {
                var prefix = line.Type switch
                {
                    DiffLineType.Added => "+",
                    DiffLineType.Deleted => "-",
                    _ => " "
                };

                // Show line number for new file (what will exist after merge)
                var lineNum = line.NewLineNumber.HasValue ? $"L{line.NewLineNumber.Value,3}" : "    ";
                sb.AppendLine($"{lineNum} {prefix} {line.Content}");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string AddLineNumbers(string content)
    {
        var lines = content.Split('\n');
        var sb = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            sb.AppendLine($"L{i + 1,3}   {lines[i].TrimEnd('\r')}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetResponseSchema()
    {
        return """
            {
              "type": "object",
              "properties": {
                "comments": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "filePath": { "type": "string" },
                      "lineNumber": { "type": "integer" },
                      "endLineNumber": { "type": ["integer", "null"] },
                      "category": {
                        "type": "string",
                        "enum": ["Bug", "Security", "Performance", "Style", "BestPractice", "Maintainability", "ErrorHandling", "Documentation", "Testing", "Other"]
                      },
                      "severity": {
                        "type": "string",
                        "enum": ["Info", "Minor", "Major", "Critical", "Blocker"]
                      },
                      "message": { "type": "string" },
                      "suggestion": { "type": ["string", "null"] },
                      "suggestedCode": { "type": ["string", "null"] }
                    },
                    "required": ["filePath", "lineNumber", "endLineNumber", "category", "severity", "message", "suggestion", "suggestedCode"],
                    "additionalProperties": false
                  }
                }
              },
              "required": ["comments"],
              "additionalProperties": false
            }
            """;
    }

    private CodeReviewResponse ParseResponse(ChatCompletion completion)
    {
        var content = completion.Content[0].Text;

        try
        {
            var response = JsonSerializer.Deserialize<CodeReviewResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return response ?? new CodeReviewResponse();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response: {Content}", content);
            return new CodeReviewResponse();
        }
    }
}
