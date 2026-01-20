using Azure.AI.OpenAI;
using Azure.Identity;
using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Infrastructure.AI.Agents;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace DevOpsCodeReviewer.Infrastructure.AI;

/// <summary>
/// Factory for creating agents using Azure OpenAI via Microsoft Agent Framework.
/// </summary>
public class AzureOpenAIAgentFactory : IAgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly IPromptService _promptService;
    private readonly IOptions<LlmOptions> _options;
    private readonly ILoggerFactory _loggerFactory;

    public AzureOpenAIAgentFactory(
        IChatClient chatClient,
        IAzureDevOpsService azureDevOpsService,
        ICodeAnalysisService codeAnalysisService,
        IPromptService promptService,
        IOptions<LlmOptions> options,
        ILoggerFactory loggerFactory)
    {
        _chatClient = chatClient;
        _azureDevOpsService = azureDevOpsService;
        _codeAnalysisService = codeAnalysisService;
        _promptService = promptService;
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public IContextGatheringAgent CreateContextGatheringAgent()
    {
        var agent = new ChatClientAgent(
            _chatClient, 
            _promptService.GetContextGatheringPrompt());

        return new ContextGatheringAgent(
            agent,
            _azureDevOpsService,
            _codeAnalysisService,
            _promptService,
            _options,
            _loggerFactory.CreateLogger<ContextGatheringAgent>());
    }

    public IDiffAnalyzerAgent CreateDiffAnalyzerAgent()
    {
        var agent = new ChatClientAgent(
            _chatClient,
            _promptService.GetDiffAnalysisPrompt());

        return new DiffAnalyzerAgent(
            agent,
            _codeAnalysisService,
            _promptService,
            _options,
            _loggerFactory.CreateLogger<DiffAnalyzerAgent>());
    }

    public ICodeReviewAgent CreateCodeReviewAgent()
    {
        var agent = new ChatClientAgent(
            _chatClient,
            _promptService.GetCodeReviewPrompt());

        return new CodeReviewAgent(
            agent,
            _codeAnalysisService,
            _promptService,
            _options,
            _loggerFactory.CreateLogger<CodeReviewAgent>());
    }
}
