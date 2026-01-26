using DevOpsCodeReviewer.Core.Agents;
using DevOpsCodeReviewer.Core.Models;
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
/// Agent responsible for generating PR overview and validating scores.
/// Runs after CodeReviewAgent to synthesize findings.
/// </summary>
public class OverviewAgent(
    AIAgent agent,
    ILogger<OverviewAgent> logger) : IOverviewAgent
{
    private static readonly HashSet<string> ValidRiskAssessments = new(StringComparer.OrdinalIgnoreCase)
    {
        "safe", "low-risk", "medium-risk", "high-risk", "critical-risk"
    };

    public string Name => "OverviewAgent";

    public async Task<ReviewOverview> ExecuteAsync(OverviewInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("[{Agent}] Generating overview for PR #{PullRequestId}",
            Name, input.Request.PullRequestId);

        try
        {
            var contextBuilder = BuildContext(input);

            var parsed = await LlmRetryHandler.ExecuteWithRetryAsync<OverviewLlmResponse>(
                agent,
                contextBuilder,
                ValidateOverviewResponse,
                logger,
                cancellationToken);

            var overview = MapAndValidateResponse(parsed, input);

            stopwatch.Stop();
            logger.LogInformation("[{Agent}] Completed in {ElapsedMs}ms. Risk: {Risk}, Confidence: {Confidence}/5",
                Name, stopwatch.ElapsedMilliseconds, overview.RiskAssessment, overview.ConfidenceScore);

            return overview;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Agent}] Error generating overview", Name);
            
            // Return a fallback overview rather than failing
            return GenerateFallbackOverview(input);
        }
    }

    private static string BuildContext(OverviewInput input)
    {
        var sb = new StringBuilder();

        // PR Information
        sb.AppendLine("## Pull Request Information");
        sb.AppendLine($"**Title**: {input.Request.Title}");
        if (!string.IsNullOrEmpty(input.Request.Description))
        {
            sb.AppendLine($"**Description**: {input.Request.Description}");
        }
        sb.AppendLine($"**Author**: {input.Request.AuthorName}");
        sb.AppendLine($"**Change Category**: {input.Analysis.ChangeCategory}");
        sb.AppendLine();

        // Change Summary
        if (!string.IsNullOrEmpty(input.Analysis.ChangeSummary))
        {
            sb.AppendLine("## Change Summary");
            sb.AppendLine(input.Analysis.ChangeSummary);
            sb.AppendLine();
        }

        // Files Changed
        sb.AppendLine("## Files Changed");
        foreach (var file in input.Analysis.Context.ChangedFiles.Take(20))
        {
            sb.AppendLine($"- `{file.Path}` ({file.ChangeType}, {file.LineCount} lines)");
        }
        if (input.Analysis.Context.ChangedFiles.Count > 20)
        {
            sb.AppendLine($"- ... and {input.Analysis.Context.ChangedFiles.Count - 20} more files");
        }
        sb.AppendLine();

        // Review Comments Summary (condensed to save tokens)
        sb.AppendLine("## Review Comments Generated");
        sb.AppendLine($"Total: {input.Comments.Count} comments");
        sb.AppendLine();

        // Group by severity
        var bySeverity = input.Comments.GroupBy(c => c.Severity).OrderByDescending(g => (int)g.Key);
        foreach (var group in bySeverity)
        {
            sb.AppendLine($"### {group.Key} ({group.Count()})");
            foreach (var comment in group.Take(5))
            {
                sb.AppendLine($"- `{comment.FilePath}:{comment.LineNumber}` [{comment.Category}] {TruncateMessage(comment.Message, 100)}");
            }
            if (group.Count() > 5)
            {
                sb.AppendLine($"  ... and {group.Count() - 5} more {group.Key} comments");
            }
            sb.AppendLine();
        }

        // Areas of Concern from Analysis
        if (input.Analysis.AreasOfConcern.Count > 0)
        {
            sb.AppendLine("## Pre-identified Concerns");
            foreach (var concern in input.Analysis.AreasOfConcern.Take(5))
            {
                sb.AppendLine($"- **{concern.Type}** in `{concern.FilePath}`: {concern.Description}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private ReviewOverview MapAndValidateResponse(OverviewLlmResponse? parsed, OverviewInput input)
    {
        var overview = new ReviewOverview();

        if (parsed == null)
        {
            logger.LogWarning("[{Agent}] Received null response, using fallback", Name);
            return GenerateFallbackOverview(input);
        }

        // Summary
        overview.Summary = !string.IsNullOrWhiteSpace(parsed.Summary) 
            ? parsed.Summary 
            : GenerateFallbackSummary(input);

        // Confidence Score - CLAMP to 1-5
        overview.ConfidenceScore = ClampScore(parsed.ConfidenceScore, "ConfidenceScore");
        overview.ConfidenceRationale = parsed.ConfidenceRationale;

        // Risk Assessment - VALIDATE enum
        overview.RiskAssessment = ValidateRiskAssessment(parsed.RiskAssessment, input);

        // Key Changes
        overview.KeyChanges = parsed.KeyChanges?
            .Where(k => !string.IsNullOrWhiteSpace(k.Description))
            .Select(k => new KeyChange
            {
                Description = k.Description!,
                Rationale = k.Rationale
            })
            .ToList() ?? [];

        // Important Files - CLAMP scores
        overview.ImportantFiles = parsed.ImportantFiles?
            .Where(f => !string.IsNullOrWhiteSpace(f.FilePath))
            .Select(f => new ImportantFile
            {
                FilePath = f.FilePath!,
                Score = ClampScore(f.Score, $"ImportantFiles[{f.FilePath}].Score"),
                Description = f.Description
            })
            .ToList() ?? [];

        // Log any validation warnings from the LLM
        if (parsed.ValidationWarnings?.Count > 0)
        {
            foreach (var warning in parsed.ValidationWarnings)
            {
                logger.LogWarning("[{Agent}] LLM validation warning: {Warning}", Name, warning);
            }
        }

        return overview;
    }

    private int ClampScore(int? value, string fieldName)
    {
        if (value == null)
        {
            logger.LogDebug("[{Agent}] {Field} was null, defaulting to 3", Name, fieldName);
            return 3;
        }

        if (value < 1)
        {
            logger.LogWarning("[{Agent}] {Field} was {Value}, clamping to 1", Name, fieldName, value);
            return 1;
        }

        if (value > 5)
        {
            logger.LogWarning("[{Agent}] {Field} was {Value}, clamping to 5", Name, fieldName, value);
            return 5;
        }

        return value.Value;
    }

    private string ValidateRiskAssessment(string? value, OverviewInput input)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DeriveRiskFromComments(input.Comments);
        }

        // Normalize common variations
        var normalized = value.ToLowerInvariant().Trim();
        
        // Handle common LLM mistakes
        normalized = normalized switch
        {
            "low" or "low risk" => "low-risk",
            "medium" or "moderate" or "medium risk" or "moderate risk" => "medium-risk",
            "high" or "high risk" => "high-risk",
            "critical" or "critical risk" => "critical-risk",
            _ => normalized
        };

        if (ValidRiskAssessments.Contains(normalized))
        {
            return normalized;
        }

        logger.LogWarning("[{Agent}] Invalid RiskAssessment '{Value}', deriving from comments", Name, value);
        return DeriveRiskFromComments(input.Comments);
    }

    private static string DeriveRiskFromComments(List<ReviewComment> comments)
    {
        if (comments.Count == 0)
            return "safe";

        var maxSeverity = comments.Max(c => (int)c.Severity);
        var blockerCount = comments.Count(c => c.Severity == ReviewSeverity.Blocker);
        var criticalCount = comments.Count(c => c.Severity == ReviewSeverity.Critical);

        if (blockerCount > 0)
            return "critical-risk";
        if (criticalCount >= 2)
            return "high-risk";
        if (criticalCount == 1 || maxSeverity >= (int)ReviewSeverity.Major)
            return "medium-risk";
        if (maxSeverity >= (int)ReviewSeverity.Minor)
            return "low-risk";
        
        return "safe";
    }

    private ReviewOverview GenerateFallbackOverview(OverviewInput input)
    {
        logger.LogInformation("[{Agent}] Generating fallback overview", Name);

        return new ReviewOverview
        {
            Summary = GenerateFallbackSummary(input),
            ConfidenceScore = 2, // Low confidence for fallback
            ConfidenceRationale = "Fallback overview generated due to processing error",
            RiskAssessment = DeriveRiskFromComments(input.Comments),
            KeyChanges = [],
            ImportantFiles = input.Analysis.Context.ChangedFiles
                .OrderByDescending(f => f.LineCount)
                .Take(5)
                .Select(f => new ImportantFile
                {
                    FilePath = f.Path,
                    Score = 3,
                    Description = $"{f.ChangeType} - {f.LineCount} lines"
                })
                .ToList()
        };
    }

    private static string GenerateFallbackSummary(OverviewInput input)
    {
        var fileCount = input.Analysis.Context.ChangedFiles.Count;
        var commentCount = input.Comments.Count;
        var category = input.Analysis.ChangeCategory;

        return $"This PR contains {fileCount} changed files categorized as '{category}'. " +
               $"The code review generated {commentCount} comment(s). " +
               $"Please review the individual comments for specific feedback.";
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            return message;

        return message[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Validates the LLM response structure for overview generation.
    /// Returns (isValid, errors) for the retry handler.
    /// </summary>
    private static (bool isValid, List<string> errors) ValidateOverviewResponse(
        string json,
        OverviewLlmResponse? response)
    {
        var errors = new List<string>();

        if (response == null)
        {
            errors.Add("Response deserialized to null");
            return (false, errors);
        }

        // Summary is the most important field
        if (string.IsNullOrWhiteSpace(response.Summary))
        {
            errors.Add("Missing required 'summary' field");
        }

        // RiskAssessment should be present
        if (string.IsNullOrWhiteSpace(response.RiskAssessment))
        {
            errors.Add("Missing 'riskAssessment' field (expected: safe, low-risk, medium-risk, high-risk, or critical-risk)");
        }
        else if (!ValidRiskAssessments.Contains(response.RiskAssessment.ToLowerInvariant().Trim()))
        {
            // We normalize these, but if it's completely wrong, flag it
            var normalizedCheck = response.RiskAssessment.ToLowerInvariant().Trim() switch
            {
                "low" or "low risk" or "medium" or "moderate" or "medium risk" or "moderate risk"
                    or "high" or "high risk" or "critical" or "critical risk" => true,
                _ => false
            };

            if (!normalizedCheck)
            {
                errors.Add($"Invalid 'riskAssessment' value: '{response.RiskAssessment}'. Expected: safe, low-risk, medium-risk, high-risk, or critical-risk");
            }
        }

        return (errors.Count == 0, errors);
    }
}
