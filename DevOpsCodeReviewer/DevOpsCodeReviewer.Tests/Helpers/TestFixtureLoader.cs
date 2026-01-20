using System.Text.Json;
using DevOpsCodeReviewer.Functions.Models;

namespace DevOpsCodeReviewer.Tests.Helpers;

/// <summary>
/// Helper class to load test fixtures from the Fixtures directory.
/// </summary>
public static class TestFixtureLoader
{
    private static readonly string FixturesPath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads a CodeReviewRequest from a fixture file.
    /// </summary>
    public static CodeReviewRequest LoadRequest(string fixtureName)
    {
        var path = Path.Combine(FixturesPath, fixtureName, "request.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CodeReviewRequest>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize request from {path}");
    }

    /// <summary>
    /// Loads iteration changes from a fixture file.
    /// </summary>
    public static List<IterationChange> LoadIterationChanges(string fixtureName)
    {
        var path = Path.Combine(FixturesPath, fixtureName, "iteration_changes.json");
        var json = File.ReadAllText(path);
        var response = JsonSerializer.Deserialize<IterationChangesResponse>(json, JsonOptions);
        return response?.ChangeEntries ?? [];
    }

    /// <summary>
    /// Loads file content from a fixture.
    /// File names use underscores instead of slashes (e.g., portal_src_lib_distance.ts).
    /// </summary>
    public static string? LoadFileContent(string fixtureName, string filePath)
    {
        // Convert path to fixture filename (replace / with _)
        var fileName = filePath.TrimStart('/').Replace("/", "_");
        var path = Path.Combine(FixturesPath, fixtureName, "files", fileName);

        if (!File.Exists(path))
            return null;

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Loads original file content for edit comparisons.
    /// </summary>
    public static string? LoadOriginalFileContent(string fixtureName, string filePath)
    {
        var fileName = filePath.TrimStart('/').Replace("/", "_") + ".original";
        var path = Path.Combine(FixturesPath, fixtureName, "files", fileName);

        if (!File.Exists(path))
            return null;

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Gets all available fixture names.
    /// </summary>
    public static IEnumerable<string> GetAvailableFixtures()
    {
        if (!Directory.Exists(FixturesPath))
            return [];

        return Directory.GetDirectories(FixturesPath)
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Cast<string>();
    }

    /// <summary>
    /// Builds FileContent objects from fixture data.
    /// </summary>
    public static List<FileContent> BuildFileContents(string fixtureName)
    {
        var changes = LoadIterationChanges(fixtureName);
        var fileContents = new List<FileContent>();

        foreach (var change in changes)
        {
            if (change.ChangeType.Equals("delete", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = LoadFileContent(fixtureName, change.Item.Path);
            if (content == null)
                continue;

            var originalContent = change.ChangeType.Equals("edit", StringComparison.OrdinalIgnoreCase)
                ? LoadOriginalFileContent(fixtureName, change.Item.Path)
                : null;

            fileContents.Add(new FileContent
            {
                Path = change.Item.Path,
                Content = content,
                OriginalContent = originalContent,
                ChangeType = change.ChangeType,
                LineCount = content.Split('\n').Length,
                ObjectId = change.Item.ObjectId,
                OriginalObjectId = change.Item.OriginalObjectId
            });
        }

        return fileContents;
    }
}

/// <summary>
/// Response wrapper for iteration changes (matches Azure DevOps API).
/// </summary>
internal class IterationChangesResponse
{
    public List<IterationChange> ChangeEntries { get; set; } = [];
}
