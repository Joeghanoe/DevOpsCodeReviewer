using DevOpsCodeReviewer.Core.Models;

namespace DevOpsCodeReviewer.Core.Services;

/// <summary>
/// Interface for code analysis operations.
/// </summary>
public interface ICodeAnalysisService
{
    /// <summary>
    /// Filters files to only include reviewable ones.
    /// </summary>
    List<FileContent> FilterReviewableFiles(List<FileContent> files);

    /// <summary>
    /// Chunks files into batches for LLM processing.
    /// </summary>
    List<List<FileContent>> ChunkFilesForReview(List<FileContent> files, int maxFilesPerChunk, int maxLinesPerChunk);

    /// <summary>
    /// Generates a fingerprint for duplicate detection.
    /// </summary>
    string GenerateCommentFingerprint(ReviewComment comment);

    /// <summary>
    /// Checks if a new comment is a duplicate of existing comments.
    /// </summary>
    bool IsDuplicateComment(ReviewComment newComment, List<ExistingComment> existingComments, string botSignature);

    /// <summary>
    /// Filters out duplicate comments from the review response.
    /// </summary>
    List<ReviewComment> FilterDuplicateComments(
        List<ReviewComment> newComments,
        List<ExistingComment> existingComments,
        string botSignature);

    /// <summary>
    /// Prioritizes files for review (new files first, then modified).
    /// </summary>
    List<FileContent> PrioritizeFiles(List<FileContent> files);

    /// <summary>
    /// Gets the programming language for a file based on extension.
    /// </summary>
    string GetLanguageFromPath(string path);

    /// <summary>
    /// Parses imports/usings from a file to find related files.
    /// </summary>
    List<string> ParseImports(string content, string filePath);
}

/// <summary>
/// Represents an existing comment on a PR for duplicate detection.
/// </summary>
public class ExistingComment
{
    public string FilePath { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
}
