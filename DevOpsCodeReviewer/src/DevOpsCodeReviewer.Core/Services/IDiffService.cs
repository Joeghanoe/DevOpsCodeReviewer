using DevOpsCodeReviewer.Core.Models;

namespace DevOpsCodeReviewer.Core.Services;

/// <summary>
/// Service for computing file diffs.
/// </summary>
public interface IDiffService
{
    /// <summary>
    /// Computes the diff between original and new content.
    /// </summary>
    /// <param name="originalContent">The original file content (null for new files).</param>
    /// <param name="newContent">The new file content.</param>
    /// <returns>List of diff hunks.</returns>
    List<DiffHunk> ComputeDiff(string? originalContent, string newContent);
}
