using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Infrastructure.AI.Models;
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
/// Agent responsible for performing contextual code review.
/// </summary>
public class CodeReviewAgent(
    AIAgent agent,
    ICodeAnalysisService codeAnalysisService,
    IOptions<LlmOptions> options,
    ILogger<CodeReviewAgent> logger) : ICodeReviewAgent
{
    private readonly AIAgent _agent = agent;
    private readonly ICodeAnalysisService _codeAnalysisService = codeAnalysisService;
    private readonly LlmOptions _options = options.Value;
    private readonly ILogger<CodeReviewAgent> _logger = logger;

    public string Name => "CodeReviewAgent";

    public async Task<CodeReviewResponse> ExecuteAsync(CodeReviewInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("[{Agent}] Starting code review for PR #{PullRequestId}",
            Name, input.Request.PullRequestId);

        var response = new CodeReviewResponse();

        try
        {
            // Chunk files for processing
            var chunks = _codeAnalysisService.ChunkFilesForReview(
                input.Analysis.Context.ChangedFiles,
                _options.MaxFilesPerRequest,
                _options.MaxLinesPerRequest);

            _logger.LogInformation("[{Agent}] Processing {ChunkCount} chunks", Name, chunks.Count);

            foreach (var chunk in chunks)
            {
                var chunkResponse = await ReviewChunkAsync(input, chunk, cancellationToken);
                response.Comments.AddRange(chunkResponse.Comments);
                response.FilesReviewed += chunkResponse.FilesReviewed;
                response.LinesReviewed += chunkResponse.LinesReviewed;

                // Capture overview from first chunk (contains full PR context)
                if (response.Overview == null && chunkResponse.Overview != null)
                {
                    response.Overview = chunkResponse.Overview;
                }

                if (chunkResponse.TokenUsage != null)
                {
                    response.TokenUsage ??= new TokenUsage();
                    response.TokenUsage.PromptTokens += chunkResponse.TokenUsage.PromptTokens;
                    response.TokenUsage.CompletionTokens += chunkResponse.TokenUsage.CompletionTokens;
                    response.TokenUsage.TotalTokens += chunkResponse.TokenUsage.TotalTokens;
                }
            }

            // Filter by severity
            response.Comments = response.Comments
                .Where(c => (int)c.Severity >= _options.MinSeverityLevel)
                .ToList();

            // Add context summary
            response.ContextSummary = new ContextSummary
            {
                RelatedFilesCount = input.Analysis.Context.RelatedFiles.Count,
                IdentifiedPatterns = input.Analysis.Context.IdentifiedPatterns.Select(p => p.Name).ToList(),
                ArchitecturalInsights = input.Analysis.Context.ArchitectureInsights.Select(i => i.Title).ToList()
            };

            response.Summary = GenerateSummary(input.Analysis, response.Comments);

            stopwatch.Stop();
            _logger.LogInformation("[{Agent}] Completed in {ElapsedMs}ms. Generated {CommentCount} comments",
                Name, stopwatch.ElapsedMilliseconds, response.Comments.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Agent}] Error during code review", Name);
            throw;
        }
    }

    private async Task<CodeReviewResponse> ReviewChunkAsync(
        CodeReviewInput input,
        List<FileContent> files,
        CancellationToken cancellationToken)
    {
        var response = new CodeReviewResponse
        {
            FilesReviewed = files.Count,
            LinesReviewed = files.Sum(f => f.LineCount)
        };

        // Build comprehensive context for LLM
        var contextBuilder = new StringBuilder();

        // PR Information
        contextBuilder.AppendLine("## Pull Request Information");
        contextBuilder.AppendLine($"**Title**: {input.Request.Title}");
        if (!string.IsNullOrEmpty(input.Request.Description))
        {
            contextBuilder.AppendLine($"**Description**: {input.Request.Description}");
        }
        contextBuilder.AppendLine($"**Author**: {input.Request.AuthorName}");
        contextBuilder.AppendLine($"**Change Category**: {input.Analysis.ChangeCategory}");
        contextBuilder.AppendLine();

        // Analysis Summary
        if (!string.IsNullOrEmpty(input.Analysis.ChangeSummary))
        {
            contextBuilder.AppendLine("## Change Analysis Summary");
            contextBuilder.AppendLine(input.Analysis.ChangeSummary);
            contextBuilder.AppendLine();
        }

        // Identified Patterns
        if (input.Analysis.Context.IdentifiedPatterns.Count > 0)
        {
            contextBuilder.AppendLine("## Existing Patterns in Codebase");
            contextBuilder.AppendLine("IMPORTANT: Reference these patterns instead of suggesting generic alternatives.");
            contextBuilder.AppendLine();
            foreach (var pattern in input.Analysis.Context.IdentifiedPatterns)
            {
                contextBuilder.AppendLine($"### {pattern.Name}");
                contextBuilder.AppendLine($"- **Description**: {pattern.Description}");
                contextBuilder.AppendLine($"- **Category**: {pattern.Category}");
                if (pattern.ImplementingFiles.Count > 0)
                {
                    contextBuilder.AppendLine($"- **Example Files**: {string.Join(", ", pattern.ImplementingFiles.Take(3))}");
                }
                if (!string.IsNullOrEmpty(pattern.ExampleCode))
                {
                    contextBuilder.AppendLine($"- **Example**: ```{pattern.ExampleCode}```");
                }
                contextBuilder.AppendLine();
            }
        }

        // Areas of Concern
        if (input.Analysis.AreasOfConcern.Count > 0)
        {
            contextBuilder.AppendLine("## Pre-identified Areas of Concern");
            contextBuilder.AppendLine("These have been flagged for attention, but verify against existing patterns:");
            contextBuilder.AppendLine();
            foreach (var concern in input.Analysis.AreasOfConcern.Take(5))
            {
                contextBuilder.AppendLine($"- **{concern.Type}** in `{concern.FilePath}:{concern.StartLine}`: {concern.Description}");
                if (concern.RelevantPatterns.Count > 0)
                {
                    contextBuilder.AppendLine($"  - Consider existing patterns: {string.Join(", ", concern.RelevantPatterns)}");
                }
            }
            contextBuilder.AppendLine();
        }

        // Related Context Files
        if (input.Analysis.Context.RelatedFiles.Count > 0)
        {
            contextBuilder.AppendLine("## Related Files (Context)");
            contextBuilder.AppendLine("Reference these when suggesting improvements:");
            contextBuilder.AppendLine();
            foreach (var file in input.Analysis.Context.RelatedFiles.Take(3))
            {
                contextBuilder.AppendLine($"### {file.Path}");
                contextBuilder.AppendLine($"*Referenced by: {file.ReferencedBy}*");
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine(TruncateContent(file.Content, 50));
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine();
            }
        }

        // Changed Files with Diffs
        contextBuilder.AppendLine("## Files to Review");
        foreach (var file in files)
        {
            var language = _codeAnalysisService.GetLanguageFromPath(file.Path);
            contextBuilder.AppendLine($"\n### {file.Path} ({file.ChangeType})");

            if (file.DiffHunks.Count > 0)
            {
                contextBuilder.AppendLine($"```{language}");
                contextBuilder.AppendLine(FormatDiffHunks(file.DiffHunks));
                contextBuilder.AppendLine("```");
            }
            else
            {
                contextBuilder.AppendLine($"```{language}");
                contextBuilder.AppendLine(AddLineNumbers(file.Content));
                contextBuilder.AppendLine("```");
            }
        }

        contextBuilder.AppendLine();
        contextBuilder.AppendLine("## Review Instructions");
        contextBuilder.AppendLine("1. Focus on the added (+) and modified lines");
        contextBuilder.AppendLine("2. Reference existing patterns when suggesting improvements");
        contextBuilder.AppendLine("3. Use exact line numbers from the diff (L###)");
        contextBuilder.AppendLine("4. Only flag issues that aren't already handled by existing patterns");

        try
        {
            ChatCompletion agentResponse = await _agent.RunAsync([new UserChatMessage(contextBuilder.ToString())]);

            // Extract text from response and deserialize
            var responseText = agentResponse.AsChatResponse().Text;
            var parsed = JsonSerializer.Deserialize<CodeReviewLlmResponse>(responseText, JsonSerializerOptions.Web);

            // Map overview if present
            if (parsed?.Overview != null)
            {
                response.Overview = new ReviewOverview
                {
                    Summary = parsed.Overview.Summary ?? "",
                    ConfidenceScore = parsed.Overview.ConfidenceScore ?? 3,
                    ConfidenceRationale = parsed.Overview.ConfidenceRationale,
                    RiskAssessment = parsed.Overview.RiskAssessment ?? "low-risk",
                    KeyChanges = parsed.Overview.KeyChanges?
                        .Select(k => new KeyChange
                        {
                            Description = k.Description ?? "",
                            Rationale = k.Rationale
                        }).ToList() ?? [],
                    ImportantFiles = parsed.Overview.ImportantFiles?
                        .Select(f => new ImportantFile
                        {
                            FilePath = f.FilePath ?? "",
                            Score = f.Score ?? 3,
                            Description = f.Description
                        }).ToList() ?? []
                };
            }

            if (parsed?.Comments != null)
            {
                // Map response to internal types
                foreach (var c in parsed.Comments)
                {
                    response.Comments.Add(new ReviewComment
                    {
                        FilePath = c.FilePath ?? "",
                        LineNumber = c.LineNumber ?? 0,
                        Severity = ParseSeverity(c.Severity),
                        Category = ParseCategory(c.Category),
                        Message = c.Message ?? "",
                        ImpactExample = c.ImpactExample,
                        Suggestion = c.Suggestion ?? "",
                        RelatedCodeReference = c.RelatedPattern
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get review from LLM");
        }

        return response;
    }

    private static string FormatDiffHunks(List<DiffHunk> hunks)
    {
        var sb = new StringBuilder();

        foreach (var hunk in hunks)
        {
            sb.AppendLine($"@@ -{hunk.OldStartLine},{hunk.OldLineCount} +{hunk.NewStartLine},{hunk.NewLineCount} @@");

            foreach (var line in hunk.Lines)
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

    private static string TruncateContent(string content, int maxLines)
    {
        var lines = content.Split('\n');
        if (lines.Length <= maxLines)
            return content;

        return string.Join('\n', lines.Take(maxLines)) + $"\n... ({lines.Length - maxLines} more lines)";
    }

    private static string GenerateSummary(DiffAnalysis analysis, List<ReviewComment> comments)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"## Code Review Summary");
        sb.AppendLine();
        sb.AppendLine($"**Change Type**: {analysis.ChangeCategory}");
        sb.AppendLine($"**Files Analyzed**: {analysis.Context.ChangedFiles.Count}");
        sb.AppendLine($"**Related Files Considered**: {analysis.Context.RelatedFiles.Count}");
        sb.AppendLine($"**Comments Generated**: {comments.Count}");
        sb.AppendLine();

        // Group comments by severity
        var bySeverity = comments.GroupBy(c => c.Severity).OrderByDescending(g => g.Key);
        foreach (var group in bySeverity)
        {
            sb.AppendLine($"- **{group.Key}**: {group.Count()} issues");
        }

        // Group comments by category
        sb.AppendLine();
        sb.AppendLine("**By Category**:");
        var byCategory = comments.GroupBy(c => c.Category).OrderByDescending(g => g.Count());
        foreach (var group in byCategory)
        {
            sb.AppendLine($"- {group.Key}: {group.Count()}");
        }

        if (analysis.Context.IdentifiedPatterns.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Patterns Considered**:");
            foreach (var pattern in analysis.Context.IdentifiedPatterns.Take(3))
            {
                sb.AppendLine($"- {pattern.Name}");
            }
        }

        return sb.ToString();
    }

    private static ReviewSeverity ParseSeverity(string? severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "info" => ReviewSeverity.Info,
            "minor" => ReviewSeverity.Minor,
            "major" => ReviewSeverity.Major,
            "critical" => ReviewSeverity.Critical,
            "blocker" => ReviewSeverity.Blocker,
            _ => ReviewSeverity.Minor
        };
    }

    private static ReviewCategory ParseCategory(string? category)
    {
        return category?.ToLowerInvariant() switch
        {
            "bug" => ReviewCategory.Bug,
            "security" => ReviewCategory.Security,
            "performance" => ReviewCategory.Performance,
            "style" => ReviewCategory.Style,
            "bestpractice" or "best practice" or "best_practice" => ReviewCategory.BestPractice,
            "maintainability" => ReviewCategory.Maintainability,
            "errorhandling" or "error handling" or "error_handling" => ReviewCategory.ErrorHandling,
            "documentation" => ReviewCategory.Documentation,
            "testing" => ReviewCategory.Testing,
            "architecture" or "architectural" => ReviewCategory.Architecture,
            "cloudcompliance" or "cloud compliance" or "cloud_compliance" or "cloud" => ReviewCategory.CloudCompliance,
            _ => ReviewCategory.Other
        };
    }
}
