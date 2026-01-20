using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Agents.AI;
using OpenAI.Chat;

namespace DevOpsCodeReviewer.Infrastructure.AI.Agents;

/// <summary>
/// Agent responsible for performing contextual code review.
/// </summary>
public class CodeReviewAgent : ICodeReviewAgent
{
    private readonly AIAgent _agent;
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly LlmOptions _options;
    private readonly ILogger<CodeReviewAgent> _logger;

    public string Name => "CodeReviewAgent";

    public CodeReviewAgent(
        AIAgent agent,
        ICodeAnalysisService codeAnalysisService,
        IPromptService promptService,
        IOptions<LlmOptions> options,
        ILogger<CodeReviewAgent> logger)
    {
        _agent = agent;
        _codeAnalysisService = codeAnalysisService;
        _options = options.Value;
        _logger = logger;
    }

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
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Return a JSON object with 'comments' array containing your review comments.");

        try
        {
            var chatCompletion = await _agent.RunAsync([new UserChatMessage(contextBuilder.ToString())]);
            var responseText = chatCompletion.Content.Last().Text;

            // Parse the response
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<ReviewResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed?.Comments != null)
                {
                    response.Comments = parsed.Comments;
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

    private class ReviewResponse
    {
        public List<ReviewComment>? Comments { get; set; }
    }
}
