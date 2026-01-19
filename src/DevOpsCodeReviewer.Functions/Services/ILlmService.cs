using DevOpsCodeReviewer.Functions.Models;

namespace DevOpsCodeReviewer.Functions.Services;

/// <summary>
/// Interface for LLM operations.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Reviews code files and returns comments.
    /// </summary>
    Task<CodeReviewResponse> ReviewCodeAsync(
        CodeReviewRequest request,
        List<FileContent> files,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reviews code in chunks and aggregates results.
    /// </summary>
    Task<CodeReviewResponse> ReviewCodeInChunksAsync(
        CodeReviewRequest request,
        List<List<FileContent>> fileChunks,
        CancellationToken cancellationToken = default);
}
