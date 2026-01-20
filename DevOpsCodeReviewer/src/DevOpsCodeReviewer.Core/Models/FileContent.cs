namespace DevOpsCodeReviewer.Core.Models;

/// <summary>
/// File content with metadata for code review.
/// </summary>
public class FileContent
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public string? ObjectId { get; set; }
    public string? OriginalObjectId { get; set; }

    /// <summary>
    /// The original file content before changes (null for new files).
    /// </summary>
    public string? OriginalContent { get; set; }

    /// <summary>
    /// List of changed hunks with line number information.
    /// </summary>
    public List<DiffHunk> DiffHunks { get; set; } = [];
}

/// <summary>
/// Represents a chunk of changed code with line information.
/// </summary>
public class DiffHunk
{
    /// <summary>
    /// Starting line number in the new file.
    /// </summary>
    public int NewStartLine { get; set; }

    /// <summary>
    /// Number of lines in the new file for this hunk.
    /// </summary>
    public int NewLineCount { get; set; }

    /// <summary>
    /// Starting line number in the original file.
    /// </summary>
    public int OldStartLine { get; set; }

    /// <summary>
    /// Number of lines in the original file for this hunk.
    /// </summary>
    public int OldLineCount { get; set; }

    /// <summary>
    /// Lines in this hunk with their change types.
    /// </summary>
    public List<DiffLine> Lines { get; set; } = [];
}

/// <summary>
/// A single line in a diff hunk.
/// </summary>
public class DiffLine
{
    /// <summary>
    /// The line number in the new file (null for deleted lines).
    /// </summary>
    public int? NewLineNumber { get; set; }

    /// <summary>
    /// The line number in the original file (null for added lines).
    /// </summary>
    public int? OldLineNumber { get; set; }

    /// <summary>
    /// The change type: Added, Deleted, or Context.
    /// </summary>
    public DiffLineType Type { get; set; }

    /// <summary>
    /// The content of the line.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Type of change for a diff line.
/// </summary>
public enum DiffLineType
{
    Context,
    Added,
    Deleted
}
