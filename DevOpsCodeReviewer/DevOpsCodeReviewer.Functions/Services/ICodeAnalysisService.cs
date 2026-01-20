using DevOpsCodeReviewer.Functions.Models;

namespace DevOpsCodeReviewer.Functions.Services;

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
    bool IsDuplicateComment(ReviewComment newComment, List<CommentThread> existingThreads, string botSignature);

    /// <summary>
    /// Filters out duplicate comments from the LLM response.
    /// </summary>
    List<ReviewComment> FilterDuplicateComments(
        List<ReviewComment> newComments,
        List<CommentThread> existingThreads,
        string botSignature);

    /// <summary>
    /// Prioritizes files for review (new files first, then modified).
    /// </summary>
    List<FileContent> PrioritizeFiles(List<FileContent> files);

    /// <summary>
    /// Gets the programming language for a file based on extension.
    /// </summary>
    string GetLanguageFromPath(string path);
}
