using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Infrastructure.AI.Models;
using DevOpsCodeReviewer.Infrastructure.AI.Validation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DevOpsCodeReviewer.Infrastructure.AI.Agents;

/// <summary>
/// Agent responsible for analyzing diff changes and identifying areas of concern.
/// </summary>
public class DiffAnalyzerAgent(
    AIAgent agent,
    ICodeAnalysisService codeAnalysisService,
    ILogger<DiffAnalyzerAgent> logger) : IDiffAnalyzerAgent
{
    private readonly AIAgent _agent = agent;
    private readonly ICodeAnalysisService _codeAnalysisService = codeAnalysisService;
    private readonly ILogger<DiffAnalyzerAgent> _logger = logger;

    public string Name => "DiffAnalyzerAgent";

    public async Task<DiffAnalysis> ExecuteAsync(DiffAnalyzerInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("[{Agent}] Starting diff analysis for {FileCount} files",
            Name, input.Context.ChangedFiles.Count);

        var result = new DiffAnalysis
        {
            Context = input.Context
        };

        try
        {
            // Step 1: Prioritize files
            var prioritizedFiles = PrioritizeFiles(input.Context.ChangedFiles);
            result.PrioritizedFiles = prioritizedFiles;

            // Step 2: Categorize the change
            result.ChangeCategory = DetermineChangeCategory(input.Context.ChangedFiles);

            // Step 3: Use LLM to identify areas of concern
            var (concerns, focusAreas, summary) = await AnalyzeDiffAsync(input.Context, cancellationToken);
            result.AreasOfConcern = concerns;
            result.ReviewFocusAreas = focusAreas;
            result.ChangeSummary = summary;

            stopwatch.Stop();
            _logger.LogInformation("[{Agent}] Completed in {ElapsedMs}ms. Category: {Category}, Concerns: {ConcernCount}",
                Name, stopwatch.ElapsedMilliseconds, result.ChangeCategory, concerns.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Agent}] Error during diff analysis", Name);
            throw;
        }
    }

    private List<PrioritizedFile> PrioritizeFiles(List<FileContent> files)
    {
        var prioritized = _codeAnalysisService.PrioritizeFiles(files);
        var result = new List<PrioritizedFile>();

        var priority = 1;
        foreach (var file in prioritized)
        {
            var reason = GetPriorityReason(file);
            result.Add(new PrioritizedFile
            {
                File = file,
                Priority = priority,
                PriorityReason = reason
            });
            priority++;
        }

        return result;
    }

    private static string GetPriorityReason(FileContent file)
    {
        var reasons = new List<string>();

        if (file.ChangeType.Equals("add", StringComparison.OrdinalIgnoreCase))
            reasons.Add("New file");

        var lowerPath = file.Path.ToLowerInvariant();

        if (lowerPath.Contains("/src/") || lowerPath.StartsWith("src/"))
            reasons.Add("Source code");

        if (lowerPath.Contains("service") || lowerPath.Contains("controller"))
            reasons.Add("Business logic");

        if (lowerPath.Contains("auth") || lowerPath.Contains("security"))
            reasons.Add("Security-sensitive");

        if (lowerPath.Contains("/test"))
            reasons.Add("Test file");

        return reasons.Count > 0 ? string.Join(", ", reasons) : "Modified file";
    }

    private static ChangeCategory DetermineChangeCategory(List<FileContent> files)
    {
        var hasNewFiles = files.Any(f => f.ChangeType.Equals("add", StringComparison.OrdinalIgnoreCase));
        var hasTestFiles = files.Any(f => f.Path.ToLowerInvariant().Contains("/test"));
        var hasConfigFiles = files.Any(f =>
            f.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            f.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
            f.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));
        var hasDocFiles = files.Any(f => f.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));

        // Check content for clues
        var allContent = string.Join("\n", files.Select(f => f.Content));
        var hasBugFixIndicators = allContent.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
                                   allContent.Contains("bug", StringComparison.OrdinalIgnoreCase);
        var hasPerformanceIndicators = allContent.Contains("perf", StringComparison.OrdinalIgnoreCase) ||
                                        allContent.Contains("optimize", StringComparison.OrdinalIgnoreCase);

        if (hasNewFiles && !hasTestFiles)
            return ChangeCategory.NewFeature;

        if (hasTestFiles && files.All(f => f.Path.ToLowerInvariant().Contains("/test")))
            return ChangeCategory.Testing;

        if (hasDocFiles && files.All(f => f.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            return ChangeCategory.Documentation;

        if (hasConfigFiles && !hasNewFiles)
            return ChangeCategory.Configuration;

        if (hasBugFixIndicators)
            return ChangeCategory.BugFix;

        if (hasPerformanceIndicators)
            return ChangeCategory.Performance;

        return ChangeCategory.Mixed;
    }

    private async Task<(List<AreaOfConcern> Concerns, List<ReviewFocus> FocusAreas, string Summary)> AnalyzeDiffAsync(
        ContextResult context,
        CancellationToken cancellationToken)
    {
        var concerns = new List<AreaOfConcern>();
        var focusAreas = new List<ReviewFocus>();
        var summary = "";

        // Build context for LLM
        var contextBuilder = new StringBuilder();

        contextBuilder.AppendLine("## Changed Files with Diffs");
        foreach (var file in context.ChangedFiles.Take(5))
        {
            contextBuilder.AppendLine($"\n### {file.Path} ({file.ChangeType})");
            if (file.DiffHunks.Count > 0)
            {
                contextBuilder.AppendLine("```diff");
                contextBuilder.AppendLine(FormatDiffHunks(file.DiffHunks));
                contextBuilder.AppendLine("```");
            }
        }

        if (context.IdentifiedPatterns.Count > 0)
        {
            contextBuilder.AppendLine("\n## Identified Patterns in Codebase");
            foreach (var pattern in context.IdentifiedPatterns)
            {
                contextBuilder.AppendLine($"- **{pattern.Name}**: {pattern.Description}");
            }
        }

        if (context.RelatedFiles.Count > 0)
        {
            contextBuilder.AppendLine("\n## Related Files (for context)");
            foreach (var file in context.RelatedFiles.Take(3))
            {
                contextBuilder.AppendLine($"- {file.Path} (referenced by {file.ReferencedBy})");
            }
        }

        try
        {
            var userMessage = contextBuilder.ToString() + @"

Analyze these changes and identify:
1. Areas of concern (potential bugs, security issues, performance problems)
2. Focus areas for review (what aspects need careful review)
3. A brief summary of what the changes accomplish

Provide severity levels (Low, Medium, High) for concerns and review depth (Surface, Standard, Deep) for focus areas.";

            var parsed = await LlmRetryHandler.ExecuteWithRetryAsync<DiffAnalysisResponse>(
                _agent,
                userMessage,
                ValidateDiffAnalysisResponse,
                _logger,
                cancellationToken);

            if (parsed != null)
            {
                // Map DiffAnalysisResponse to internal types, skipping invalid entries
                if (parsed.Concerns != null)
                {
                    foreach (var c in parsed.Concerns)
                    {
                        // Skip concerns missing required fields
                        if (string.IsNullOrWhiteSpace(c.Area))
                        {
                            _logger.LogDebug("Skipping concern with missing Area");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(c.Reason))
                        {
                            _logger.LogDebug("Skipping concern with missing Reason for {Area}", c.Area);
                            continue;
                        }

                        concerns.Add(new AreaOfConcern
                        {
                            FilePath = c.Area,
                            Description = c.Reason,
                            Severity = ParseSeverity(c.Severity),
                            Type = ConcernType.Other
                        });
                    }
                }

                if (parsed.FocusAreas != null)
                {
                    foreach (var f in parsed.FocusAreas)
                    {
                        // Skip focus areas missing required fields
                        if (string.IsNullOrWhiteSpace(f.Area))
                        {
                            _logger.LogDebug("Skipping focus area with missing Area");
                            continue;
                        }

                        focusAreas.Add(new ReviewFocus
                        {
                            Title = f.Area,
                            Description = $"{f.Reason ?? ""} (Review Depth: {f.SuggestedReviewDepth ?? "Standard"})"
                        });
                    }
                }

                summary = parsed.Summary ?? "";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze diff via LLM, using heuristics");

            // Fallback: Use simple heuristics
            concerns = IdentifyConcernsHeuristically(context.ChangedFiles);
            summary = $"Analysis of {context.ChangedFiles.Count} changed files";
        }

        return (concerns, focusAreas, summary);
    }

    private static string FormatDiffHunks(List<DiffHunk> hunks)
    {
        var sb = new StringBuilder();

        foreach (var hunk in hunks.Take(3)) // Limit hunks
        {
            sb.AppendLine($"@@ -{hunk.OldStartLine},{hunk.OldLineCount} +{hunk.NewStartLine},{hunk.NewLineCount} @@");

            foreach (var line in hunk.Lines.Take(30)) // Limit lines per hunk
            {
                var prefix = line.Type switch
                {
                    DiffLineType.Added => "+",
                    DiffLineType.Deleted => "-",
                    _ => " "
                };

                var lineNum = line.NewLineNumber.HasValue ? $"L{line.NewLineNumber.Value,3}" : "    ";
                sb.AppendLine($"{lineNum} {prefix} {line.Content}");
            }

            if (hunk.Lines.Count > 30)
            {
                sb.AppendLine($"... ({hunk.Lines.Count - 30} more lines)");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static List<AreaOfConcern> IdentifyConcernsHeuristically(List<FileContent> files)
    {
        var concerns = new List<AreaOfConcern>();

        foreach (var file in files)
        {
            var content = file.Content;
            var lineNumber = 1;

            // Check for potential null reference issues
            if (content.Contains("null!") || content.Contains("?.") == false && content.Contains("null"))
            {
                concerns.Add(new AreaOfConcern
                {
                    FilePath = file.Path,
                    StartLine = lineNumber,
                    EndLine = lineNumber,
                    Type = ConcernType.PotentialNullReference,
                    Description = "Potential null reference - check null handling",
                    Severity = ConcernSeverity.Medium
                });
            }

            // Check for hardcoded strings that might be secrets
            if (content.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("apikey", StringComparison.OrdinalIgnoreCase))
            {
                concerns.Add(new AreaOfConcern
                {
                    FilePath = file.Path,
                    StartLine = lineNumber,
                    EndLine = lineNumber,
                    Type = ConcernType.SecurityRisk,
                    Description = "Potential hardcoded secret or sensitive data",
                    Severity = ConcernSeverity.High
                });
            }
        }

        return concerns;
    }

    private static ConcernSeverity ParseSeverity(string? severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "low" => ConcernSeverity.Low,
            "medium" => ConcernSeverity.Medium,
            "high" => ConcernSeverity.High,
            _ => ConcernSeverity.Medium
        };
    }

    /// <summary>
    /// Validates the LLM response structure for diff analysis.
    /// Returns (isValid, errors) for the retry handler.
    /// </summary>
    private static (bool isValid, List<string> errors) ValidateDiffAnalysisResponse(
        string json,
        DiffAnalysisResponse? response)
    {
        var errors = new List<string>();

        if (response == null)
        {
            errors.Add("Response deserialized to null");
            return (false, errors);
        }

        // A response with no concerns/focus areas is valid (no issues found)
        // We just need to ensure the structure is correct

        if (response.Concerns != null)
        {
            var validConcerns = response.Concerns.Count(c =>
                !string.IsNullOrWhiteSpace(c.Area) && !string.IsNullOrWhiteSpace(c.Reason));

            if (response.Concerns.Count > 0 && validConcerns == 0)
            {
                errors.Add("All concerns are missing required fields (Area or Reason)");
            }
        }

        if (response.FocusAreas != null)
        {
            var validFocusAreas = response.FocusAreas.Count(f => !string.IsNullOrWhiteSpace(f.Area));

            if (response.FocusAreas.Count > 0 && validFocusAreas == 0)
            {
                errors.Add("All focus areas are missing required 'Area' field");
            }
        }

        return (errors.Count == 0, errors);
    }
}
