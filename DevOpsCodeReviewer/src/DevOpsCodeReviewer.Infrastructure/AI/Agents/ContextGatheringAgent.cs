using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Infrastructure.AI.Models;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DevOpsCodeReviewer.Infrastructure.AI.Agents;

/// <summary>
/// Agent responsible for gathering contextual information about the codebase.
/// </summary>
public class ContextGatheringAgent(
    AIAgent agent,
    IAzureDevOpsService azureDevOpsService,
    ICodeAnalysisService codeAnalysisService,
    ILogger<ContextGatheringAgent> logger) : IContextGatheringAgent
{
    private readonly AIAgent _agent = agent;
    private readonly IAzureDevOpsService _azureDevOpsService = azureDevOpsService;
    private readonly ICodeAnalysisService _codeAnalysisService = codeAnalysisService;
    private readonly ILogger<ContextGatheringAgent> _logger = logger;

    public string Name => "ContextGatheringAgent";

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

        // Skip invalid paths (Windows absolute paths, empty, etc.)
        if (string.IsNullOrWhiteSpace(importPath) ||
            importPath.Contains(':') ||
            importPath.Contains('\\'))
        {
            return paths;
        }

        var sourceDir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "";
        var sourceExt = Path.GetExtension(sourcePath);

        // For relative imports (already resolved to repo paths)
        if (importPath.StartsWith("/"))
        {
            // Try with same extension as source
            if (!string.IsNullOrEmpty(sourceExt))
                paths.Add(importPath + sourceExt);

            // Try common extensions
            paths.Add(importPath + ".ts");
            paths.Add(importPath + ".tsx");
            paths.Add(importPath + ".js");
            paths.Add(importPath + ".jsx");
            paths.Add(importPath + "/index.ts");
            paths.Add(importPath + "/index.tsx");
            paths.Add(importPath + "/index.js");
        }
        else if (importPath.StartsWith("."))
        {
            var resolved = Path.Combine(sourceDir, importPath).Replace('\\', '/');

            // Try with same extension as source
            if (!string.IsNullOrEmpty(sourceExt))
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
            // For namespace imports (C# project namespaces - not system namespaces)
            // These should be project-specific namespaces that made it through filtering
            var pathFromNamespace = importPath.Replace('.', '/');
            paths.Add($"/{pathFromNamespace}.cs");
            paths.Add($"/src/{pathFromNamespace}.cs");
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
            var userMessage = contextBuilder.ToString() + "\n\nIdentify patterns and architectural insights in these files.";

            var response = await _agent.RunAsync([new UserChatMessage(userMessage)]);

            // Extract text from response and deserialize
            var responseText = response.AsChatResponse().Text;
            var parsed = JsonSerializer.Deserialize<PatternAnalysisResponse>(responseText, JsonSerializerOptions.Web);

            if (parsed != null)
            {
                // Map response to internal types
                if (parsed.Patterns != null)
                {
                    foreach (var p in parsed.Patterns)
                    {
                        patterns.Add(new CodePattern
                        {
                            Name = p.Name ?? "",
                            Description = p.Description ?? "",
                            Category = ParsePatternCategory(p.Category),
                            ImplementingFiles = p.ImplementingFiles ?? [],
                            ExampleCode = p.ExampleCode ?? ""
                        });
                    }
                }

                if (parsed.Insights != null)
                {
                    foreach (var i in parsed.Insights)
                    {
                        insights.Add(new ArchitectureInsight
                        {
                            Title = i.Title ?? "",
                            Description = $"{i.Description ?? ""} Impact: {i.Impact ?? "Unknown"}"
                        });
                    }
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

    private static PatternCategory ParsePatternCategory(string? category)
    {
        return category?.ToLowerInvariant() switch
        {
            "errorhandling" or "error handling" or "error_handling" => PatternCategory.ErrorHandling,
            "logging" => PatternCategory.Logging,
            "validation" => PatternCategory.Validation,
            "dataaccess" or "data access" or "data_access" => PatternCategory.DataAccess,
            "dependencyinjection" or "dependency injection" or "di" => PatternCategory.DependencyInjection,
            "authentication" or "auth" => PatternCategory.Authentication,
            "configuration" or "config" => PatternCategory.Configuration,
            "testing" or "test" => PatternCategory.Testing,
            _ => PatternCategory.Other
        };
    }
}
