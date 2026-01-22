using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsCodeReviewer.Infrastructure.AI;

/// <summary>
/// Service for loading and managing prompt templates.
/// </summary>
public interface IPromptService
{
    /// <summary>
    /// Gets the system prompt for code review based on file types.
    /// </summary>
    string GetSystemPrompt(IEnumerable<string> filePaths);

    /// <summary>
    /// Gets the context gathering prompt.
    /// </summary>
    string GetContextGatheringPrompt();

    /// <summary>
    /// Gets the diff analysis prompt.
    /// </summary>
    string GetDiffAnalysisPrompt();

    /// <summary>
    /// Gets the code review prompt.
    /// </summary>
    string GetCodeReviewPrompt();
}

/// <summary>
/// Implementation of prompt service.
/// </summary>
public class PromptService : IPromptService
{
    private readonly LlmOptions _options;
    private readonly ILogger<PromptService> _logger;
    private readonly Dictionary<string, string> _promptCache = new();
    private readonly object _cacheLock = new();

    private static readonly Dictionary<string, string> ExtensionToPromptFile = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", "csharp-code-review.md" },
        { ".ts", "typescript-code-review.md" },
        { ".tsx", "typescript-code-review.md" },
        { ".js", "typescript-code-review.md" },
        { ".jsx", "typescript-code-review.md" }
    };

    public PromptService(
        IOptions<LlmOptions> options,
        ILogger<PromptService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GetSystemPrompt(IEnumerable<string> filePaths)
    {
        var extensions = filePaths
            .Select(p => Path.GetExtension(p))
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .ToList();

        // Find dominant language
        var dominantExtension = extensions
            .GroupBy(e => e.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? ".txt";

        var promptBuilder = new System.Text.StringBuilder();

        // Load generic prompt
        promptBuilder.AppendLine(LoadPromptFile("generic-code-review.md"));

        // Load language-specific prompt if available
        if (ExtensionToPromptFile.TryGetValue(dominantExtension, out var promptFile))
        {
            var languagePrompt = LoadPromptFile(promptFile);
            if (!string.IsNullOrEmpty(languagePrompt))
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine(languagePrompt);
            }
        }

        // Always load core principles (Architecture, Security, Cloud)
        var architecturalPrinciples = LoadPromptFile("principles/architectural-principles.md");
        if (!string.IsNullOrEmpty(architecturalPrinciples))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(architecturalPrinciples);
        }

        var securityPrinciples = LoadPromptFile("principles/security-principles.md");
        if (!string.IsNullOrEmpty(securityPrinciples))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(securityPrinciples);
        }

        var cloudPrinciples = LoadPromptFile("principles/cloud-principles.md");
        if (!string.IsNullOrEmpty(cloudPrinciples))
        {
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(cloudPrinciples);
        }

        return promptBuilder.ToString();
    }

    public string GetContextGatheringPrompt()
    {
        return """
            You are a Context Gathering Agent. Your task is to analyze changed files and identify:

            1. **Imports and Dependencies**: Extract all imports, usings, and references from each changed file.

            2. **Patterns to Look For**:
               - Error handling patterns (try-catch blocks, error boundaries, global handlers)
               - Logging patterns (how logging is done elsewhere)
               - Validation patterns (input validation, schema validation)
               - Authentication/Authorization patterns
               - Data access patterns (repositories, services)

            3. **Relationships**:
               - Which files import the changed files
               - Which interfaces the changed files implement
               - Which base classes the changed files extend

            4. **Architectural Insights**:
               - How the changed files fit into the overall architecture
               - What conventions are used in similar files
               - What shared utilities or helpers exist

            Output your findings in a structured format that helps the next agent understand:
            - What context files should be fetched
            - What patterns exist in the codebase
            - What the reviewer should be aware of
            """;
    }

    public string GetDiffAnalysisPrompt()
    {
        return """
            You are a Diff Analyzer Agent. Your task is to analyze code changes and identify areas of concern.

            Given the changed files and their context (related files, patterns), you should:

            1. **Categorize the Change**:
               - New Feature: Adding new functionality
               - Bug Fix: Fixing an existing issue
               - Refactoring: Restructuring without changing behavior
               - Performance: Optimizing performance
               - Security: Addressing security concerns
               - Documentation: Updating docs/comments
               - Testing: Adding or modifying tests
               - Configuration: Changing settings/config

            2. **Identify Areas of Concern**:
               - Missing error handling (but check if global handlers exist!)
               - Security risks (SQL injection, XSS, etc.)
               - Performance issues (N+1 queries, unnecessary loops)
               - Inconsistent patterns (not following existing conventions)
               - Missing validation (but check if validation exists elsewhere!)
               - Potential null references
               - Resource leaks (undisposed resources)
               - Thread safety issues

            3. **Prioritize Files for Review**:
               - Critical business logic first
               - Security-sensitive code
               - New functionality over modifications
               - Source files over test files

            4. **Structure Review Focus**:
               For each concern, note:
               - The existing patterns that might address it
               - Related context files the reviewer should see
               - Whether the concern is actually valid given the context

            IMPORTANT: Do not flag generic issues like "add error handling" if error handling patterns exist elsewhere in the codebase.
            """;
    }

    public string GetCodeReviewPrompt()
    {
        return """
            You are a Code Review Agent performing contextual code reviews.

            You have access to:
            1. The changed files with their diffs
            2. Related context files (imported modules, interfaces, base classes)
            3. Identified patterns in the codebase
            4. Areas of concern identified by the Diff Analyzer

            Your review should be CONTEXTUAL, not generic. For each issue:

            1. **Reference Related Code**:
               - "Consider using the existing `ErrorHandlerMiddleware` from `/src/middleware/ErrorHandler.cs`"
               - "The `UserValidator` class at `/src/validators/UserValidator.cs` already handles this validation"
               - "Follow the logging pattern used in `/src/services/OrderService.cs:45`"

            2. **Be Specific**:
               - Bad: "Add error handling"
               - Good: "This method should use the try-catch pattern from `ServiceBase.ExecuteAsync()` to handle exceptions consistently"

            3. **Consider Context**:
               - If there's a global error boundary, don't suggest adding try-catch everywhere
               - If there's a validation library, reference it instead of suggesting inline validation
               - If there's a logging service, reference it instead of suggesting Console.WriteLine

            4. **Prioritize Appropriately**:
               - Critical: Security vulnerabilities, data loss risks
               - Major: Bugs, significant performance issues
               - Minor: Style issues, minor improvements
               - Info: Suggestions, nice-to-haves

            Output in JSON format with:
            - filePath: The file being commented on
            - lineNumber: The specific line number
            - category: Bug, Security, Performance, Style, BestPractice, Maintainability, ErrorHandling, Documentation, Testing, Other
            - severity: Info, Minor, Major, Critical, Blocker
            - message: The review comment
            - suggestion: How to fix it (optional)
            - suggestedCode: Code snippet for the fix (optional)
            - relatedCodeReference: Reference to related existing code (optional)
            """;
    }

    private string LoadPromptFile(string fileName)
    {
        lock (_cacheLock)
        {
            if (_promptCache.TryGetValue(fileName, out var cached))
                return cached;

            var promptPath = Path.Combine(_options.PromptsDirectory, fileName);

            if (!File.Exists(promptPath))
            {
                _logger.LogWarning("Prompt file not found: {Path}", promptPath);
                return string.Empty;
            }

            try
            {
                var content = File.ReadAllText(promptPath);
                _promptCache[fileName] = content;
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load prompt file: {Path}", promptPath);
                return string.Empty;
            }
        }
    }
}
