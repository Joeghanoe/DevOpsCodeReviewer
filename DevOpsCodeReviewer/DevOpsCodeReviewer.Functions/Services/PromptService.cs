using Microsoft.Extensions.Logging;

namespace DevOpsCodeReviewer.Functions.Services;

/// <summary>
/// Service for loading and combining code review prompts from external files.
/// </summary>
public class PromptService : IPromptService
{
    private readonly ICodeAnalysisService _codeAnalysisService;
    private readonly ILogger<PromptService> _logger;
    private readonly string _promptsDirectory;

    private readonly Dictionary<string, string> _promptCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();
    private bool _cacheInitialized;

    // Maps CodeAnalysisService language identifiers to prompt file names
    private static readonly Dictionary<string, string> LanguageToPromptFile = new(StringComparer.OrdinalIgnoreCase)
    {
        { "csharp", "csharp-code-review.md" },
        { "typescript", "typescript-code-review.md" },
        { "javascript", "typescript-code-review.md" }
    };

    private const string GenericPromptFile = "generic-code-review.md";

    public PromptService(
        ICodeAnalysisService codeAnalysisService,
        ILogger<PromptService> logger)
    {
        _codeAnalysisService = codeAnalysisService;
        _logger = logger;

        // Determine prompts directory relative to the application base directory
        var baseDirectory = AppContext.BaseDirectory;
        _promptsDirectory = Path.Combine(baseDirectory, "prompts");

        _logger.LogDebug("Prompts directory configured at: {PromptsDirectory}", _promptsDirectory);
    }

    public string GetSystemPrompt(IEnumerable<string> filePaths)
    {
        EnsureCacheInitialized();

        var dominantLanguage = GetDominantLanguage(filePaths);
        _logger.LogDebug("Dominant language for review: {Language}", dominantLanguage ?? "none");

        var genericPrompt = GetCachedPrompt(GenericPromptFile);
        if (string.IsNullOrEmpty(genericPrompt))
        {
            _logger.LogWarning("Generic prompt file not found, using fallback");
            return GetFallbackPrompt();
        }

        // If we have a language-specific prompt, combine them
        if (!string.IsNullOrEmpty(dominantLanguage) &&
            LanguageToPromptFile.TryGetValue(dominantLanguage, out var languagePromptFile))
        {
            var languagePrompt = GetCachedPrompt(languagePromptFile);
            if (!string.IsNullOrEmpty(languagePrompt))
            {
                _logger.LogDebug("Combining generic prompt with {Language} prompt", dominantLanguage);
                return CombinePrompts(genericPrompt, languagePrompt);
            }
        }

        return genericPrompt;
    }

    private void EnsureCacheInitialized()
    {
        if (_cacheInitialized)
            return;

        lock (_cacheLock)
        {
            if (_cacheInitialized)
                return;

            LoadPrompts();
            _cacheInitialized = true;
        }
    }

    private void LoadPrompts()
    {
        if (!Directory.Exists(_promptsDirectory))
        {
            _logger.LogWarning("Prompts directory not found at: {PromptsDirectory}", _promptsDirectory);
            return;
        }

        var promptFiles = Directory.GetFiles(_promptsDirectory, "*.md");
        foreach (var filePath in promptFiles)
        {
            var fileName = Path.GetFileName(filePath);
            try
            {
                var content = File.ReadAllText(filePath);
                _promptCache[fileName] = content;
                _logger.LogDebug("Loaded prompt file: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load prompt file: {FileName}", fileName);
            }
        }

        _logger.LogInformation("Loaded {Count} prompt files from {Directory}",
            _promptCache.Count, _promptsDirectory);
    }

    private string? GetCachedPrompt(string fileName)
    {
        return _promptCache.TryGetValue(fileName, out var content) ? content : null;
    }

    private string? GetDominantLanguage(IEnumerable<string> filePaths)
    {
        var languageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in filePaths)
        {
            var language = _codeAnalysisService.GetLanguageFromPath(path);

            // Only count languages that have specific prompts
            if (LanguageToPromptFile.ContainsKey(language))
            {
                languageCounts[language] = languageCounts.GetValueOrDefault(language) + 1;
            }
        }

        if (languageCounts.Count == 0)
            return null;

        // Return the language with the most files
        return languageCounts
            .OrderByDescending(kvp => kvp.Value)
            .First()
            .Key;
    }

    private static string CombinePrompts(string genericPrompt, string languagePrompt)
    {
        return $"{genericPrompt}\n\n---\n\n{languagePrompt}";
    }

    private static string GetFallbackPrompt()
    {
        return """
            You are an expert code reviewer. Your task is to review code changes in a pull request and provide constructive feedback.

            The code is shown in unified diff format with line numbers:
            - Lines with `+` are additions (new code)
            - Lines with `-` are deletions (removed code)
            - Lines show their line number as L### (e.g., L42 means line 42)

            IMPORTANT: Use the exact line numbers shown in the diff (e.g., L42 → lineNumber: 42).
            Only comment on changed lines (marked with +).

            Focus on:
            - Bugs and logic errors
            - Security vulnerabilities
            - Performance issues
            - Error handling
            - Code quality

            Respond with a JSON object containing a "comments" array. Each comment should have:
            - filePath: The file path
            - lineNumber: The exact line number from the diff (1-based)
            - endLineNumber: End line for multi-line issues (null if single line)
            - category: Bug, Security, Performance, Style, BestPractice, Maintainability, ErrorHandling, Documentation, Testing, or Other
            - severity: Info, Minor, Major, Critical, or Blocker
            - message: A clear description of the issue referencing the specific code
            - suggestion: (optional) How to fix the issue
            - suggestedCode: (optional) Code snippet showing the fix

            If there are no issues, return an empty comments array.
            """;
    }
}
