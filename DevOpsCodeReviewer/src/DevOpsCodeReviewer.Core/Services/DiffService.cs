using DevOpsCodeReviewer.Core.Models;

namespace DevOpsCodeReviewer.Core.Services;

/// <summary>
/// Service for computing file diffs using LCS algorithm.
/// </summary>
public class DiffService : IDiffService
{
    private const int ContextLines = 3;

    public List<DiffHunk> ComputeDiff(string? originalContent, string newContent)
    {
        var hunks = new List<DiffHunk>();

        // If no original content, the entire file is new
        if (string.IsNullOrEmpty(originalContent))
        {
            return CreateAddedFileHunks(newContent);
        }

        var oldLines = originalContent.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var newLines = newContent.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        // Use Myers diff algorithm (simplified LCS-based approach)
        var diffLines = ComputeLcsDiff(oldLines, newLines);

        // Group diff lines into hunks with context
        hunks = GroupIntoHunks(diffLines, oldLines.Length, newLines.Length);

        return hunks;
    }

    private static List<DiffHunk> CreateAddedFileHunks(string content)
    {
        var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var hunk = new DiffHunk
        {
            OldStartLine = 0,
            OldLineCount = 0,
            NewStartLine = 1,
            NewLineCount = lines.Length
        };

        for (var i = 0; i < lines.Length; i++)
        {
            hunk.Lines.Add(new DiffLine
            {
                NewLineNumber = i + 1,
                OldLineNumber = null,
                Type = DiffLineType.Added,
                Content = lines[i]
            });
        }

        return [hunk];
    }

    private static List<(DiffLineType Type, int? OldLine, int? NewLine, string Content)> ComputeLcsDiff(
        string[] oldLines, string[] newLines)
    {
        var result = new List<(DiffLineType, int?, int?, string)>();

        // Compute LCS table
        var lcs = new int[oldLines.Length + 1, newLines.Length + 1];
        for (var i = 1; i <= oldLines.Length; i++)
        {
            for (var j = 1; j <= newLines.Length; j++)
            {
                if (oldLines[i - 1] == newLines[j - 1])
                    lcs[i, j] = lcs[i - 1, j - 1] + 1;
                else
                    lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
            }
        }

        // Backtrack to find the diff
        var diffItems = new List<(DiffLineType, int?, int?, string)>();
        var oi = oldLines.Length;
        var ni = newLines.Length;

        while (oi > 0 || ni > 0)
        {
            if (oi > 0 && ni > 0 && oldLines[oi - 1] == newLines[ni - 1])
            {
                diffItems.Add((DiffLineType.Context, oi, ni, oldLines[oi - 1]));
                oi--;
                ni--;
            }
            else if (ni > 0 && (oi == 0 || lcs[oi, ni - 1] >= lcs[oi - 1, ni]))
            {
                diffItems.Add((DiffLineType.Added, null, ni, newLines[ni - 1]));
                ni--;
            }
            else
            {
                diffItems.Add((DiffLineType.Deleted, oi, null, oldLines[oi - 1]));
                oi--;
            }
        }

        diffItems.Reverse();
        return diffItems;
    }

    private List<DiffHunk> GroupIntoHunks(
        List<(DiffLineType Type, int? OldLine, int? NewLine, string Content)> diffLines,
        int oldLineCount, int newLineCount)
    {
        var hunks = new List<DiffHunk>();

        // Find changed regions and expand with context
        var changeIndices = new List<int>();
        for (var i = 0; i < diffLines.Count; i++)
        {
            if (diffLines[i].Type != DiffLineType.Context)
                changeIndices.Add(i);
        }

        if (changeIndices.Count == 0)
            return hunks;

        // Group nearby changes into hunks
        var hunkRanges = new List<(int Start, int End)>();
        var rangeStart = Math.Max(0, changeIndices[0] - ContextLines);
        var rangeEnd = Math.Min(diffLines.Count - 1, changeIndices[0] + ContextLines);

        for (var i = 1; i < changeIndices.Count; i++)
        {
            var changeStart = Math.Max(0, changeIndices[i] - ContextLines);
            var changeEnd = Math.Min(diffLines.Count - 1, changeIndices[i] + ContextLines);

            if (changeStart <= rangeEnd + 1)
            {
                // Merge with current range
                rangeEnd = changeEnd;
            }
            else
            {
                // Start new range
                hunkRanges.Add((rangeStart, rangeEnd));
                rangeStart = changeStart;
                rangeEnd = changeEnd;
            }
        }
        hunkRanges.Add((rangeStart, rangeEnd));

        // Create hunks from ranges
        foreach (var (start, end) in hunkRanges)
        {
            var hunkLines = diffLines.Skip(start).Take(end - start + 1).ToList();

            var firstOldLine = hunkLines.FirstOrDefault(l => l.OldLine.HasValue).OldLine ?? 1;
            var firstNewLine = hunkLines.FirstOrDefault(l => l.NewLine.HasValue).NewLine ?? 1;
            var oldCount = hunkLines.Count(l => l.Type != DiffLineType.Added);
            var newCount = hunkLines.Count(l => l.Type != DiffLineType.Deleted);

            var hunk = new DiffHunk
            {
                OldStartLine = firstOldLine,
                OldLineCount = oldCount,
                NewStartLine = firstNewLine,
                NewLineCount = newCount,
                Lines = hunkLines.Select(l => new DiffLine
                {
                    OldLineNumber = l.OldLine,
                    NewLineNumber = l.NewLine,
                    Type = l.Type,
                    Content = l.Content
                }).ToList()
            };

            hunks.Add(hunk);
        }

        return hunks;
    }
}
