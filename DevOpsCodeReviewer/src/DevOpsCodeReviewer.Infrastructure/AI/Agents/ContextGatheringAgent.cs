using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Agents.AI;
using OpenAI.Chat;

namespace DevOpsCodeReviewer.Infrastructure.AI.Agents;

/// <summary>
/// Agent responsible for gathering contextual information about the codebase.
/// </summary>
public class ContextGatheringAgent : IContextGatheringAgent
{
    private readonly AIAgent _agent;
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly LlmOptions _options;
    private readonly ILogger<ContextGatheringAgent> _logger;

    public string Name => "ContextGatheringAgent";

    public ContextGatheringAgent(
        AIAgent agent,
        IAzureDevOpsService azureDevOpsService,
        ICodeAnalysisService codeAnalysisService,
        IPromptService promptService,
        IOptions<LlmOptions> options,
        ILogger<ContextGatheringAgent> logger)
    {
        _agent = agent;
        _azureDevOpsService = azureDevOpsService;
        _codeAnalysisService = codeAnalysisService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ContextResult> ExecuteAsync(ContextGatheringInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("[{Agent}] Starting context gathering for {FileCount} files",
            Name, input.ChangedFiles.Count);

        var result = new ContextResult
        {
            ChangedFiles = input.ChangedFiles
        };

        try
        {
            // Step 1: Parse imports from all changed files
            var allImports = new Dictionary<string, List<string>>();
            foreach (var file in input.ChangedFiles)
            {
                var imports = _codeAnalysisService.ParseImports(file.Content, file.Path);
                if (imports.Count > 0)
                {
                    allImports[file.Path] = imports;
                    _logger.LogDebug("Found {Count} imports in {File}", imports.Count, file.Path);
                }
            }

            // Step 2: Fetch related files
            var relatedFiles = await FetchRelatedFilesAsync(input, allImports, cancellationToken);
            result.RelatedFiles = relatedFiles;

            // Step 3: Use LLM to identify patterns
            var patterns = await IdentifyPatternsAsync(input.ChangedFiles, relatedFiles, cancellationToken);
            result.IdentifiedPatterns = patterns.Patterns;
            result.ArchitectureInsights = patterns.Insights;

            stopwatch.Stop();
            _logger.LogInformation("[{Agent}] Completed in {ElapsedMs}ms. Found {RelatedCount} related files, {PatternCount} patterns",
                Name, stopwatch.ElapsedMilliseconds, relatedFiles.Count, patterns.Patterns.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Agent}] Error during context gathering", Name);
            throw;
        }
    }

    private async Task<List<RelatedFile>> FetchRelatedFilesAsync(
        ContextGatheringInput input,
        Dictionary<string, List<string>> imports,
        CancellationToken cancellationToken)
    {
        var relatedFiles = new List<RelatedFile>();
        var fetchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add changed files to fetched set to avoid re-fetching
        foreach (var file in input.ChangedFiles)
        {
            fetchedPaths.Add(file.Path);
        }

        // Process imports to find related files
        foreach (var (sourcePath, sourceImports) in imports)
        {
            foreach (var importPath in sourceImports)
            {
                // Skip if already fetched or if it's a system import
                if (fetchedPaths.Contains(importPath))
                    continue;

                // Try to determine the actual file path
                var possiblePaths = GetPossibleFilePaths(importPath, sourcePath);

                foreach (var possiblePath in possiblePaths)
                {
                    if (fetchedPaths.Contains(possiblePath))
                        continue;

                    var content = await _azureDevOpsService.GetFileContentAsync(
                        input.Context.OrganizationUrl,
                        input.Context.ProjectId,
                        input.Context.RepositoryId,
                        possiblePath,
                        input.Context.SourceCommitId,
                        cancellationToken);

                    if (content != null)
                    {
                        fetchedPaths.Add(possiblePath);
                        relatedFiles.Add(new RelatedFile
                        {
                            Path = possiblePath,
                            Content = content,
                            Relationship = RelationshipType.Import,
                            ReferencedBy = sourcePath
                        });

                        _logger.LogDebug("Fetched related file: {Path} (imported by {Source})",
                            possiblePath, sourcePath);

                        // Limit related files to prevent too much context
                        if (relatedFiles.Count >= 10)
                            return relatedFiles;

                        break; // Found the file, no need to try other paths
                    }
                }
            }
        }

        return relatedFiles;
    }

    private static List<string> GetPossibleFilePaths(string importPath, string sourcePath)
    {
        var paths = new List<string>();
        var sourceDir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "";
        var sourceExt = Path.GetExtension(sourcePath);

        // For relative imports
        if (importPath.StartsWith("."))
        {
            var resolved = Path.Combine(sourceDir, importPath).Replace('\\', '/');

            // Try with same extension as source
            paths.Add(resolved + sourceExt);

            // Try common extensions
            paths.Add(resolved + ".ts");
            paths.Add(resolved + ".tsx");
            paths.Add(resolved + ".js");
            paths.Add(resolved + ".jsx");
            paths.Add(resolved + "/index.ts");
            paths.Add(resolved + "/index.tsx");
            paths.Add(resolved + "/index.js");
        }
        else
        {
            // For namespace imports (C#) or module imports
            var pathFromNamespace = importPath.Replace('.', '/');
            paths.Add($"/{pathFromNamespace}.cs");
            paths.Add($"/src/{pathFromNamespace}.cs");
            paths.Add($"/src/{importPath.Replace('.', '/')}.cs");
        }

        return paths.Distinct().ToList();
    }

    private async Task<(List<CodePattern> Patterns, List<ArchitectureInsight> Insights)> IdentifyPatternsAsync(
        List<FileContent> changedFiles,
        List<RelatedFile> relatedFiles,
        CancellationToken cancellationToken)
    {
        var patterns = new List<CodePattern>();
        var insights = new List<ArchitectureInsight>();

        // Build context for LLM
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("## Changed Files");
        foreach (var file in changedFiles.Take(5))
        {
            contextBuilder.AppendLine($"\n### {file.Path}");
            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine(TruncateContent(file.Content, 100));
            contextBuilder.AppendLine("```");
        }

        contextBuilder.AppendLine("\n## Related Files");
        foreach (var file in relatedFiles.Take(5))
        {
            contextBuilder.AppendLine($"\n### {file.Path} (referenced by {file.ReferencedBy})");
            contextBuilder.AppendLine("```");
            contextBuilder.AppendLine(TruncateContent(file.Content, 100));
            contextBuilder.AppendLine("```");
        }

        try
        {
            var userMessage = contextBuilder.ToString() + "\n\nIdentify patterns and architectural insights in these files. Return a JSON object with 'patterns' and 'insights' arrays.";

            var chatCompletion = await _agent.RunAsync([new UserChatMessage(userMessage)]);
            var responseText = chatCompletion.Content.Last().Text;

            // Parse the response
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<PatternResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null)
                {
                    patterns = parsed.Patterns ?? [];
                    insights = parsed.Insights ?? [];
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to identify patterns via LLM, using heuristics");

            // Fallback: Use simple heuristics
            patterns = IdentifyPatternsHeuristically(changedFiles, relatedFiles);
        }

        return (patterns, insights);
    }

    private static string TruncateContent(string content, int maxLines)
    {
        var lines = content.Split('\n');
        if (lines.Length <= maxLines)
            return content;

        return string.Join('\n', lines.Take(maxLines)) + $"\n... ({lines.Length - maxLines} more lines)";
    }

    private static List<CodePattern> IdentifyPatternsHeuristically(
        List<FileContent> changedFiles,
        List<RelatedFile> relatedFiles)
    {
        var patterns = new List<CodePattern>();
        var allContent = changedFiles.Select(f => f.Content)
            .Concat(relatedFiles.Select(f => f.Content));

        // Check for common patterns
        if (allContent.Any(c => c.Contains("try") && c.Contains("catch")))
        {
            patterns.Add(new CodePattern
            {
                Name = "TryCatch",
                Description = "Exception handling with try-catch blocks",
                Category = PatternCategory.ErrorHandling
            });
        }

        if (allContent.Any(c => c.Contains("ILogger") || c.Contains("_logger")))
        {
            patterns.Add(new CodePattern
            {
                Name = "ILoggerPattern",
                Description = "Structured logging using ILogger",
                Category = PatternCategory.Logging
            });
        }

        if (allContent.Any(c => c.Contains("Validate") || c.Contains("Validator")))
        {
            patterns.Add(new CodePattern
            {
                Name = "ValidationService",
                Description = "Input validation through validator classes",
                Category = PatternCategory.Validation
            });
        }

        return patterns;
    }

    private class PatternResponse
    {
        public List<CodePattern>? Patterns { get; set; }
        public List<ArchitectureInsight>? Insights { get; set; }
    }
}
