using DevOpsCodeReviewer.Core.Configuration;
using DevOpsCodeReviewer.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DevOpsCodeReviewer.Core.Services;

/// <summary>
/// Service for code analysis operations.
/// </summary>
public class CodeAnalysisService : ICodeAnalysisService
{
    private readonly CodeAnalysisOptions _options;
    private readonly ILogger<CodeAnalysisService> _logger;

    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", "csharp" },
        { ".ts", "typescript" },
        { ".tsx", "typescript" },
        { ".js", "javascript" },
        { ".jsx", "javascript" },
        { ".py", "python" },
        { ".java", "java" },
        { ".go", "go" },
        { ".rs", "rust" },
        { ".bicep", "bicep" },
        { ".yaml", "yaml" },
        { ".yml", "yaml" },
        { ".json", "json" },
        { ".xml", "xml" },
        { ".html", "html" },
        { ".css", "css" },
        { ".scss", "scss" },
        { ".sql", "sql" },
        { ".sh", "bash" },
        { ".ps1", "powershell" },
        { ".md", "markdown" }
    };

    // Regex patterns for parsing imports
    private static readonly Regex CSharpUsingRegex = new(@"^\s*using\s+(?:static\s+)?([A-Za-z0-9_.]+);", RegexOptions.Multiline);
    private static readonly Regex TypeScriptImportRegex = new(@"import\s+.*?\s+from\s+['""]([^'""]+)['""]", RegexOptions.Multiline);
    private static readonly Regex PythonImportRegex = new(@"^\s*(?:from\s+([A-Za-z0-9_.]+)\s+import|import\s+([A-Za-z0-9_.]+))", RegexOptions.Multiline);

    public CodeAnalysisService(
        IOptions<CodeAnalysisOptions> options,
        ILogger<CodeAnalysisService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public List<FileContent> FilterReviewableFiles(List<FileContent> files)
    {
        var filtered = new List<FileContent>();

        foreach (var file in files)
        {
            if (IsReviewable(file))
            {
                filtered.Add(file);
            }
            else
            {
                _logger.LogDebug("Filtered out file: {Path}", file.Path);
            }
        }

        return filtered;
    }

    private bool IsReviewable(FileContent file)
    {
        var extension = Path.GetExtension(file.Path);

        // Check if extension is in the included list
        if (!_options.IncludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return false;

        // Check excluded patterns
        foreach (var pattern in _options.ExcludedPatterns)
        {
            if (MatchesPattern(file.Path, pattern))
                return false;
        }

        // Check file size
        if (file.Content.Length > _options.MaxFileSizeBytes)
            return false;

        // Skip binary files (simple heuristic: check for null bytes)
        if (file.Content.Contains('\0'))
            return false;

        return true;
    }

    private static bool MatchesPattern(string path, string pattern)
    {
        if (pattern.StartsWith("*."))
        {
            return path.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        return path.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
               path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    public List<List<FileContent>> ChunkFilesForReview(
        List<FileContent> files,
        int maxFilesPerChunk,
        int maxLinesPerChunk)
    {
        var chunks = new List<List<FileContent>>();
        var currentChunk = new List<FileContent>();
        var currentLineCount = 0;

        foreach (var file in files)
        {
            // Start a new chunk if adding this file would exceed limits
            if (currentChunk.Count >= maxFilesPerChunk ||
                (currentLineCount + file.LineCount > maxLinesPerChunk && currentChunk.Count > 0))
            {
                chunks.Add(currentChunk);
                currentChunk = [];
                currentLineCount = 0;
            }

            currentChunk.Add(file);
            currentLineCount += file.LineCount;
        }

        // Add the last chunk if not empty
        if (currentChunk.Count > 0)
        {
            chunks.Add(currentChunk);
        }

        _logger.LogInformation("Split {FileCount} files into {ChunkCount} chunks", files.Count, chunks.Count);

        return chunks;
    }

    public string GenerateCommentFingerprint(ReviewComment comment)
    {
        // Normalize the message for fingerprinting
        var normalizedMessage = NormalizeMessage(comment.Message);

        var input = $"{comment.FilePath}|{comment.LineNumber}|{comment.Category}|{normalizedMessage}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }

    private static string NormalizeMessage(string message)
    {
        // Remove extra whitespace and normalize
        var normalized = Regex.Replace(message, @"\s+", " ").Trim().ToLowerInvariant();

        // Remove common filler words
        normalized = Regex.Replace(normalized, @"\b(please|consider|should|could|might|may)\b", "", RegexOptions.IgnoreCase);

        return normalized;
    }

    public bool IsDuplicateComment(
        ReviewComment newComment,
        List<ExistingComment> existingComments,
        string botSignature)
    {
        foreach (var existing in existingComments)
        {
            // Check if this is a bot comment
            if (!existing.Content.Contains(botSignature))
                continue;

            // Check file path match
            if (!existing.FilePath.Equals(newComment.FilePath, StringComparison.OrdinalIgnoreCase))
                continue;

            // Check line proximity (within 3 lines)
            if (existing.LineNumber.HasValue && Math.Abs(existing.LineNumber.Value - newComment.LineNumber) > 3)
                continue;

            // Check category if present in comment
            var commentContent = existing.Content.ToLowerInvariant();
            var categoryStr = newComment.Category.ToString().ToLowerInvariant();
            if (commentContent.Contains(categoryStr))
            {
                _logger.LogDebug("Found duplicate comment at {FilePath}:{Line}", newComment.FilePath, newComment.LineNumber);
                return true;
            }

            // Semantic similarity check (simple word overlap)
            if (HasHighWordOverlap(existing.Content, newComment.Message))
            {
                _logger.LogDebug("Found semantically similar comment at {FilePath}:{Line}", newComment.FilePath, newComment.LineNumber);
                return true;
            }
        }

        return false;
    }

    private static bool HasHighWordOverlap(string existing, string newMessage)
    {
        var existingWords = ExtractWords(existing);
        var newWords = ExtractWords(newMessage);

        if (newWords.Count == 0)
            return false;

        var overlap = newWords.Count(w => existingWords.Contains(w));
        var overlapRatio = (double)overlap / newWords.Count;

        return overlapRatio > 0.6; // 60% word overlap threshold
    }

    private static HashSet<string> ExtractWords(string text)
    {
        var words = Regex.Matches(text.ToLowerInvariant(), @"\b[a-z]{3,}\b")
            .Select(m => m.Value)
            .Where(w => !IsStopWord(w));
        return [.. words];
    }

    private static bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string>
        {
            "the", "this", "that", "with", "from", "have", "has", "had",
            "are", "was", "were", "been", "being", "will", "would", "could",
            "should", "may", "might", "can", "must", "for", "and", "but",
            "not", "you", "all", "any", "use", "used", "using"
        };
        return stopWords.Contains(word);
    }

    public List<ReviewComment> FilterDuplicateComments(
        List<ReviewComment> newComments,
        List<ExistingComment> existingComments,
        string botSignature)
    {
        var filtered = new List<ReviewComment>();
        var seenFingerprints = new HashSet<string>();

        foreach (var comment in newComments)
        {
            // Generate fingerprint
            comment.Fingerprint = GenerateCommentFingerprint(comment);

            // Check for duplicates within the new comments
            if (seenFingerprints.Contains(comment.Fingerprint))
            {
                _logger.LogDebug("Skipping duplicate comment (same fingerprint): {FilePath}:{Line}", comment.FilePath, comment.LineNumber);
                continue;
            }

            // Check for duplicates against existing comments
            if (IsDuplicateComment(comment, existingComments, botSignature))
            {
                continue;
            }

            seenFingerprints.Add(comment.Fingerprint);
            filtered.Add(comment);
        }

        _logger.LogInformation("Filtered {Original} comments to {Filtered} unique comments",
            newComments.Count, filtered.Count);

        return filtered;
    }

    public List<FileContent> PrioritizeFiles(List<FileContent> files)
    {
        // Priority order:
        // 1. New files (add)
        // 2. Source files (src/, lib/, app/)
        // 3. Modified files
        // 4. Test files

        return files
            .OrderByDescending(f => f.ChangeType.Equals("add", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(f => IsSourceFile(f.Path) ? 1 : 0)
            .ThenByDescending(f => !IsTestFile(f.Path) ? 1 : 0)
            .ToList();
    }

    private static bool IsSourceFile(string path)
    {
        var lowerPath = path.ToLowerInvariant();
        return lowerPath.Contains("/src/") ||
               lowerPath.Contains("/lib/") ||
               lowerPath.Contains("/app/") ||
               lowerPath.StartsWith("src/") ||
               lowerPath.StartsWith("lib/") ||
               lowerPath.StartsWith("app/");
    }

    private static bool IsTestFile(string path)
    {
        var lowerPath = path.ToLowerInvariant();
        return lowerPath.Contains("/test") ||
               lowerPath.Contains(".test.") ||
               lowerPath.Contains(".spec.") ||
               lowerPath.Contains("_test.") ||
               lowerPath.EndsWith("tests.cs") ||
               lowerPath.EndsWith("test.cs");
    }

    public string GetLanguageFromPath(string path)
    {
        var extension = Path.GetExtension(path);
        return ExtensionToLanguage.GetValueOrDefault(extension, "text");
    }

    public List<string> ParseImports(string content, string filePath)
    {
        var imports = new List<string>();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        switch (extension)
        {
            case ".cs":
                imports.AddRange(ParseCSharpUsings(content));
                break;
            case ".ts":
            case ".tsx":
            case ".js":
            case ".jsx":
                imports.AddRange(ParseTypeScriptImports(content, filePath));
                break;
            case ".py":
                imports.AddRange(ParsePythonImports(content));
                break;
        }

        return imports;
    }

    // Prefixes that indicate system/framework namespaces (not user code)
    private static readonly string[] SystemNamespacePrefixes =
    [
        "System",
        "Microsoft",
        "Azure",
        "Newtonsoft",
        "NuGet",
        "Polly",
        "Serilog",
        "AutoMapper",
        "FluentValidation",
        "MediatR",
        "Moq",
        "xunit",
        "NUnit",
        "MSTest"
    ];

    private static List<string> ParseCSharpUsings(string content)
    {
        var usings = new List<string>();
        var matches = CSharpUsingRegex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                var ns = match.Groups[1].Value;

                // Skip system/framework namespaces - they're from NuGet packages, not the repo
                if (IsSystemNamespace(ns))
                    continue;

                usings.Add(ns);
            }
        }

        return usings;
    }

    private static bool IsSystemNamespace(string ns)
    {
        foreach (var prefix in SystemNamespacePrefixes)
        {
            if (ns.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                ns.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static List<string> ParseTypeScriptImports(string content, string currentFile)
    {
        var imports = new List<string>();
        var matches = TypeScriptImportRegex.Matches(content);
        var currentDir = Path.GetDirectoryName(currentFile)?.Replace('\\', '/') ?? "";

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                var importPath = match.Groups[1].Value;

                // Skip node_modules imports (non-relative paths)
                if (!importPath.StartsWith(".") && !importPath.StartsWith("/"))
                    continue;

                // Skip absolute Windows paths (these are invalid for repo paths)
                if (importPath.Contains(':') || importPath.Contains('\\'))
                    continue;

                // Normalize path separators
                importPath = importPath.Replace('\\', '/');

                // Resolve relative path manually to keep it as a repo path
                string resolvedPath;
                if (importPath.StartsWith("./"))
                {
                    resolvedPath = $"{currentDir}/{importPath[2..]}";
                }
                else if (importPath.StartsWith("../"))
                {
                    // Handle parent directory references
                    var parts = currentDir.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
                    var importParts = importPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var part in importParts)
                    {
                        if (part == ".." && parts.Count > 0)
                            parts.RemoveAt(parts.Count - 1);
                        else if (part != ".")
                            parts.Add(part);
                    }
                    resolvedPath = "/" + string.Join("/", parts);
                }
                else
                {
                    resolvedPath = importPath;
                }

                // Ensure path starts with /
                if (!resolvedPath.StartsWith('/'))
                    resolvedPath = "/" + resolvedPath;

                imports.Add(resolvedPath);
            }
        }

        return imports;
    }

    private static List<string> ParsePythonImports(string content)
    {
        var imports = new List<string>();
        var matches = PythonImportRegex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Success)
            {
                var moduleName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (!string.IsNullOrEmpty(moduleName))
                {
                    imports.Add(moduleName);
                }
            }
        }

        return imports;
    }
}
