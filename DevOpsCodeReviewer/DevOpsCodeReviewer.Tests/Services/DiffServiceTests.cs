using DevOpsCodeReviewer.Functions.Models;
using DevOpsCodeReviewer.Functions.Services;

namespace DevOpsCodeReviewer.Tests.Services;

public class DiffServiceTests
{
    private readonly DiffService _sut = new();

    #region New File Tests

    [Fact]
    public void ComputeDiff_NewFile_ReturnsAllLinesAsAdded()
    {
        // Arrange
        var newContent = """
            line 1
            line 2
            line 3
            """;

        // Act
        var hunks = _sut.ComputeDiff(null, newContent);

        // Assert
        hunks.Should().HaveCount(1);
        var hunk = hunks[0];
        hunk.OldStartLine.Should().Be(0);
        hunk.OldLineCount.Should().Be(0);
        hunk.NewStartLine.Should().Be(1);
        hunk.NewLineCount.Should().Be(3);

        hunk.Lines.Should().HaveCount(3);
        hunk.Lines.Should().AllSatisfy(l => l.Type.Should().Be(DiffLineType.Added));

        // Verify line numbers are correct
        hunk.Lines[0].NewLineNumber.Should().Be(1);
        hunk.Lines[1].NewLineNumber.Should().Be(2);
        hunk.Lines[2].NewLineNumber.Should().Be(3);
    }

    [Fact]
    public void ComputeDiff_EmptyOriginal_TreatsAsNewFile()
    {
        // Arrange
        var newContent = "single line";

        // Act
        var hunks = _sut.ComputeDiff("", newContent);

        // Assert
        hunks.Should().HaveCount(1);
        hunks[0].Lines.Should().AllSatisfy(l => l.Type.Should().Be(DiffLineType.Added));
    }

    #endregion

    #region Edit File Tests

    [Fact]
    public void ComputeDiff_SingleLineChange_ReturnsCorrectLineNumbers()
    {
        // Arrange
        var original = """
            line 1
            line 2 original
            line 3
            """;
        var modified = """
            line 1
            line 2 modified
            line 3
            """;

        // Act
        var hunks = _sut.ComputeDiff(original, modified);

        // Assert
        hunks.Should().HaveCount(1);
        var hunk = hunks[0];

        // Should have context, deletion, addition, context
        var deletedLine = hunk.Lines.FirstOrDefault(l => l.Type == DiffLineType.Deleted);
        var addedLine = hunk.Lines.FirstOrDefault(l => l.Type == DiffLineType.Added);

        deletedLine.Should().NotBeNull();
        addedLine.Should().NotBeNull();

        deletedLine!.Content.Should().Contain("original");
        addedLine!.Content.Should().Contain("modified");

        // The added line should be at line 2 in the new file
        addedLine.NewLineNumber.Should().Be(2);
    }

    [Fact]
    public void ComputeDiff_AddedLines_HaveCorrectLineNumbers()
    {
        // Arrange
        var original = """
            function test() {
              return 1;
            }
            """;
        var modified = """
            function test() {
              // Added comment
              const value = 1;
              return value;
            }
            """;

        // Act
        var hunks = _sut.ComputeDiff(original, modified);

        // Assert
        hunks.Should().NotBeEmpty();

        var addedLines = hunks.SelectMany(h => h.Lines)
            .Where(l => l.Type == DiffLineType.Added)
            .ToList();

        addedLines.Should().NotBeEmpty();

        // All added lines should have new line numbers
        addedLines.Should().AllSatisfy(l => l.NewLineNumber.Should().NotBeNull());

        // Verify the line numbers are sequential and make sense
        var lineNumbers = addedLines.Select(l => l.NewLineNumber!.Value).ToList();
        lineNumbers.Should().BeInAscendingOrder();
    }

    [Fact]
    public void ComputeDiff_DeletedLines_HaveNullNewLineNumber()
    {
        // Arrange
        var original = """
            line 1
            line to delete
            line 3
            """;
        var modified = """
            line 1
            line 3
            """;

        // Act
        var hunks = _sut.ComputeDiff(original, modified);

        // Assert
        var deletedLines = hunks.SelectMany(h => h.Lines)
            .Where(l => l.Type == DiffLineType.Deleted)
            .ToList();

        deletedLines.Should().NotBeEmpty();
        deletedLines.Should().AllSatisfy(l => l.NewLineNumber.Should().BeNull());
        deletedLines.Should().AllSatisfy(l => l.OldLineNumber.Should().NotBeNull());
    }

    [Fact]
    public void ComputeDiff_ContextLines_HaveBothLineNumbers()
    {
        // Arrange
        var original = """
            line 1
            line 2
            line 3
            """;
        var modified = """
            line 1
            line 2 changed
            line 3
            """;

        // Act
        var hunks = _sut.ComputeDiff(original, modified);

        // Assert
        var contextLines = hunks.SelectMany(h => h.Lines)
            .Where(l => l.Type == DiffLineType.Context)
            .ToList();

        contextLines.Should().NotBeEmpty();
        contextLines.Should().AllSatisfy(l =>
        {
            l.NewLineNumber.Should().NotBeNull();
            l.OldLineNumber.Should().NotBeNull();
        });
    }

    #endregion

    #region Real World Scenario Tests

    [Fact]
    public void ComputeDiff_TypeScriptHookModification_ProducesCorrectDiff()
    {
        // Arrange - simulate the useAddresses hook modification
        var original = """
            import { useState, useEffect } from 'react';
            import { Address } from '../types';

            export function useAddresses(userId: string) {
              const [addresses, setAddresses] = useState<Address[]>([]);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                loadAddresses();
              }, [userId]);

              return { addresses, loading };
            }
            """;

        var modified = """
            import { useState, useEffect, useMemo } from 'react';
            import { Address, Coordinates } from '../types';
            import { sortByDistance } from '../lib/distance';

            export interface UseAddressesOptions {
              sortByDistanceFrom?: Coordinates;
            }

            export function useAddresses(userId: string, options?: UseAddressesOptions) {
              const [addresses, setAddresses] = useState<Address[]>([]);
              const [loading, setLoading] = useState(true);

              useEffect(() => {
                loadAddresses();
              }, [userId]);

              const sorted = useMemo(() => {
                if (options?.sortByDistanceFrom) {
                  return sortByDistance(addresses, options.sortByDistanceFrom);
                }
                return addresses;
              }, [addresses, options?.sortByDistanceFrom]);

              return { addresses: sorted, loading };
            }
            """;

        // Act
        var hunks = _sut.ComputeDiff(original, modified);

        // Assert
        hunks.Should().NotBeEmpty();

        // Get all added lines
        var addedLines = hunks.SelectMany(h => h.Lines)
            .Where(l => l.Type == DiffLineType.Added)
            .ToList();

        // Should have added lines for new imports, interface, useMemo, etc.
        addedLines.Should().NotBeEmpty();

        // All added lines must have valid new line numbers (not null, not 0, not 1 for all)
        addedLines.Should().AllSatisfy(l =>
        {
            l.NewLineNumber.Should().NotBeNull();
            l.NewLineNumber.Should().BeGreaterThan(0);
        });

        // Line numbers should be diverse (not all the same)
        var distinctLineNumbers = addedLines.Select(l => l.NewLineNumber).Distinct().Count();
        distinctLineNumbers.Should().BeGreaterThan(1, "Added lines should have different line numbers");

        // Verify specific content is found with expected line numbers
        var sortByDistanceImport = addedLines.FirstOrDefault(l => l.Content.Contains("sortByDistance"));
        sortByDistanceImport.Should().NotBeNull("Should find the sortByDistance import");
        sortByDistanceImport!.NewLineNumber.Should().Be(3, "sortByDistance import should be on line 3");
    }

    #endregion

    #region Line Number Accuracy Tests

    [Fact]
    public void ComputeDiff_LineNumbersMatchActualFilePosition()
    {
        // Arrange - a file where we know exactly where changes are
        var original = """
            // Line 1: comment
            const a = 1;
            const b = 2;
            const c = 3;
            // Line 5: end
            """;

        var modified = """
            // Line 1: comment
            const a = 1;
            const b = 2;
            const NEW = 'inserted';
            const c = 3;
            // Line 6: end (shifted)
            """;

        // Act
        var hunks = _sut.ComputeDiff(original, modified);

        // Assert
        var addedLine = hunks.SelectMany(h => h.Lines)
            .First(l => l.Type == DiffLineType.Added && l.Content.Contains("NEW"));

        // The new line should be at line 4 in the modified file
        addedLine.NewLineNumber.Should().Be(4);
    }

    [Fact]
    public void ComputeDiff_MultipleHunks_AllHaveCorrectLineNumbers()
    {
        // Arrange - changes at beginning and end should create separate hunks
        var original = """
            line 1
            line 2
            line 3
            line 4
            line 5
            line 6
            line 7
            line 8
            line 9
            line 10
            """;

        var modified = """
            CHANGED line 1
            line 2
            line 3
            line 4
            line 5
            line 6
            line 7
            line 8
            line 9
            CHANGED line 10
            """;

        // Act
        var hunks = _sut.ComputeDiff(original, modified);

        // Assert
        var allAddedLines = hunks.SelectMany(h => h.Lines)
            .Where(l => l.Type == DiffLineType.Added)
            .ToList();

        allAddedLines.Should().HaveCount(2);

        var firstChange = allAddedLines.First(l => l.Content.Contains("CHANGED line 1"));
        var lastChange = allAddedLines.First(l => l.Content.Contains("CHANGED line 10"));

        firstChange.NewLineNumber.Should().Be(1);
        lastChange.NewLineNumber.Should().Be(10);
    }

    #endregion
}
