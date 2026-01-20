using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Infrastructure.AI.Agents;
using DevOpsCodeReviewer.Infrastructure.AI.Models;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DevOpsCodeReviewer.Infrastructure.AI;

/// <summary>
/// Factory for creating agents using Azure OpenAI via Microsoft Agent Framework.
/// </summary>
public class AzureOpenAIAgentFactory(
    IChatClient chatClient,
    IAzureDevOpsService azureDevOpsService,
    ICodeAnalysisService codeAnalysisService,
    IPromptService promptService,
    IOptions<LlmOptions> options,
    ILoggerFactory loggerFactory) : IAgentFactory
{
    private readonly IChatClient _chatClient = chatClient;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;
    private readonly ICodeAnalysisService _codeAnalysisService = codeAnalysisService;
    private readonly IPromptService _promptService = promptService;
    private readonly IOptions<LlmOptions> _options = options;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public IContextGatheringAgent CreateContextGatheringAgent()
    {
        var chatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            Instructions = _promptService.GetContextGatheringPrompt(),
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<PatternAnalysisResponse>(
                schemaDescription: "Identified code patterns and architectural insights")
        };

        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            Name = "ContextGatheringAgent",
            ChatOptions = chatOptions
        });

        return new ContextGatheringAgent(
            agent,
            _azureDevOpsService,
            _codeAnalysisService,
            _loggerFactory.CreateLogger<ContextGatheringAgent>());
    }

    public IDiffAnalyzerAgent CreateDiffAnalyzerAgent()
    {
        var chatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            Instructions = _promptService.GetDiffAnalysisPrompt(),
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<DiffAnalysisResponse>(
                schemaDescription: "Analysis of code diff changes including concerns, focus areas, and summary")
        };

        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            Name = "DiffAnalyzerAgent",
            ChatOptions = chatOptions
        });

        return new DiffAnalyzerAgent(
            agent,
            _codeAnalysisService,
            _loggerFactory.CreateLogger<DiffAnalyzerAgent>());
    }

    public ICodeReviewAgent CreateCodeReviewAgent()
    {
        var chatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            Instructions = _promptService.GetCodeReviewPrompt(),
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<CodeReviewLlmResponse>(
                schemaDescription: "Code review comments with severity and suggestions")
        };

        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            Name = "CodeReviewAgent",
            ChatOptions = chatOptions
        });

        return new CodeReviewAgent(
            agent,
            _codeAnalysisService,
            _options,
            _loggerFactory.CreateLogger<CodeReviewAgent>());
    }
}
