using System.Text;
using DevOpsCodeReviewer.Functions.Configuration;
using DevOpsCodeReviewer.Functions.Models;
using DevOpsCodeReviewer.Functions.Services;
using DevOpsCodeReviewer.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DevOpsCodeReviewer.Tests.Integration;

/// <summary>
/// Integration tests for the code review pipeline using real PR fixtures.
/// These tests verify that the diff computation and prompt building work correctly
/// without requiring network access to Azure DevOps or LLM services.
/// </summary>
public class CodeReviewIntegrationTests
{
    private const string PR4Fixture = "PR4";

    private static ICodeAnalysisService CreateCodeAnalysisService()
    {
        var options = Options.Create(new AzureDevOpsOptions
        {
            IncludedExtensions = [".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".go"],
            ExcludedPatterns = ["*.generated.cs", "*.Designer.cs", "node_modules/", "bin/", "obj/"],
            MaxFileSizeBytes = 100000,
            MaxFilesPerReview = 20
        });
        var logger = NullLogger<CodeAnalysisService>.Instance;
        return new CodeAnalysisService(options, logger);
    }

    #region Fixture Loading Tests

    [Fact]
    public void LoadFixtures_PR4_LoadsRequestCorrectly()
    {
        // Act
        var request = TestFixtureLoader.LoadRequest(PR4Fixture);

        // Assert
        request.Should().NotBeNull();
        request.PullRequestId.Should().Be(4);
        request.ProjectName.Should().Be("Joeghanoe");
        request.Title.Should().Contain("Distancing");
    }

    [Fact]
    public void LoadFixtures_PR4_LoadsIterationChanges()
    {
        // Act
        var changes = TestFixtureLoader.LoadIterationChanges(PR4Fixture);

        // Assert
        changes.Should().NotBeEmpty();
        changes.Should().Contain(c => c.Item.Path.Contains("distance.ts"));
        changes.Should().Contain(c => c.Item.Path.Contains("useAddresses.ts"));
        changes.Should().Contain(c => c.Item.Path.Contains("DistanceService.cs"));
    }

    [Fact]
    public void LoadFixtures_PR4_LoadsFileContents()
    {
        // Act
        var fileContents = TestFixtureLoader.BuildFileContents(PR4Fixture);

        // Assert
        fileContents.Should().NotBeEmpty();
        fileContents.Should().AllSatisfy(f =>
        {
            f.Content.Should().NotBeNullOrEmpty("File content should not be empty");
            f.Path.Should().NotBeNullOrEmpty();
            f.LineCount.Should().BeGreaterThan(0);
        });
    }

    #endregion

    #region Diff Computation Integration Tests

    [Fact]
    public void DiffComputation_NewFile_AllLinesAreAddedWithCorrectNumbers()
    {
        // Arrange
        var diffService = new DiffService();
        var fileContents = TestFixtureLoader.BuildFileContents(PR4Fixture);
        var distanceFile = fileContents.First(f => f.Path.Contains("distance.ts"));

        // Act
        var hunks = diffService.ComputeDiff(null, distanceFile.Content);

        // Assert
        hunks.Should().NotBeEmpty();

        var allLines = hunks.SelectMany(h => h.Lines).ToList();
        allLines.Should().AllSatisfy(l => l.Type.Should().Be(DiffLineType.Added));

        // Verify line numbers are sequential starting from 1
        var lineNumbers = allLines.Select(l => l.NewLineNumber!.Value).ToList();
        lineNumbers.First().Should().Be(1);
        lineNumbers.Should().BeInAscendingOrder();
        lineNumbers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void DiffComputation_EditedFile_HasCorrectLineNumbersForChanges()
    {
        // Arrange
        var diffService = new DiffService();
        var fileContents = TestFixtureLoader.BuildFileContents(PR4Fixture);
        var useAddressesFile = fileContents.First(f => f.Path.Contains("useAddresses.ts"));

        // Act
        var hunks = diffService.ComputeDiff(useAddressesFile.OriginalContent, useAddressesFile.Content);

        // Assert
        hunks.Should().NotBeEmpty();

        var addedLines = hunks.SelectMany(h => h.Lines)
            .Where(l => l.Type == DiffLineType.Added)
            .ToList();

        var contextLines = hunks.SelectMany(h => h.Lines)
            .Where(l => l.Type == DiffLineType.Context)
            .ToList();

        // Should have both added and context lines for an edit
        addedLines.Should().NotBeEmpty("Edited file should have added lines");

        // Added lines should have valid line numbers (not all 1!)
        var distinctLineNumbers = addedLines.Select(l => l.NewLineNumber).Distinct().Count();
        distinctLineNumbers.Should().BeGreaterThan(1,
            "Added lines should have different line numbers, not all be line 1");

        // Context lines should have both old and new line numbers
        if (contextLines.Any())
        {
            contextLines.Should().AllSatisfy(l =>
            {
                l.NewLineNumber.Should().NotBeNull();
                l.OldLineNumber.Should().NotBeNull();
            });
        }
    }

    [Fact]
    public void DiffComputation_CSharpFile_ProducesValidDiff()
    {
        // Arrange
        var diffService = new DiffService();
        var fileContents = TestFixtureLoader.BuildFileContents(PR4Fixture);
        var csFile = fileContents.First(f => f.Path.EndsWith(".cs"));

        // Act
        var hunks = diffService.ComputeDiff(csFile.OriginalContent, csFile.Content);

        // Assert
        hunks.Should().NotBeEmpty();

        // For a new C# file, all lines should be additions with correct numbers
        var allLines = hunks.SelectMany(h => h.Lines).ToList();
        allLines.Should().NotBeEmpty();

        // Verify we can find specific code elements at expected lines
        var classLine = allLines.FirstOrDefault(l => l.Content.Contains("class DistanceService"));
        classLine.Should().NotBeNull("Should find the class declaration");
        classLine!.NewLineNumber.Should().BeGreaterThan(5, "Class should not be at line 1");
    }

    #endregion

    #region Prompt Building Integration Tests

    [Fact]
    public void PromptBuilding_WithDiffs_IncludesLineNumbers()
    {
        // Arrange
        var diffService = new DiffService();
        var codeAnalysisService = CreateCodeAnalysisService();
        var fileContents = TestFixtureLoader.BuildFileContents(PR4Fixture);

        // Compute diffs for all files
        foreach (var file in fileContents)
        {
            file.DiffHunks = diffService.ComputeDiff(file.OriginalContent, file.Content);
        }

        // Act - simulate prompt building (simplified version of LlmService.BuildUserPrompt)
        var prompt = BuildTestPrompt(fileContents, codeAnalysisService);

        // Assert
        prompt.Should().NotBeNullOrEmpty();

        // Should contain line number markers like "L 42" or "L42"
        prompt.Should().MatchRegex(@"L\s*\d+", "Prompt should contain line number markers");

        // Should contain diff markers
        prompt.Should().Contain("+", "Prompt should contain addition markers");

        // Should NOT have all line numbers be 1
        var lineMatches = System.Text.RegularExpressions.Regex.Matches(prompt, @"L\s*(\d+)");
        var lineNumbers = lineMatches.Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList();
        lineNumbers.Count.Should().BeGreaterThan(1, "Should have diverse line numbers, not all 1");
    }

    [Fact]
    public void PromptBuilding_FilePathsAreCorrect()
    {
        // Arrange
        var diffService = new DiffService();
        var codeAnalysisService = CreateCodeAnalysisService();
        var fileContents = TestFixtureLoader.BuildFileContents(PR4Fixture);

        foreach (var file in fileContents)
        {
            file.DiffHunks = diffService.ComputeDiff(file.OriginalContent, file.Content);
        }

        // Act
        var prompt = BuildTestPrompt(fileContents, codeAnalysisService);

        // Assert
        prompt.Should().Contain("/portal/src/lib/distance.ts");
        prompt.Should().Contain("/portal/src/hooks/useAddresses.ts");
        prompt.Should().Contain("/api-dotnet/Services/Implementations/DistanceService.cs");
    }

    #endregion

    #region End-to-End Simulation Tests

    [Fact]
    public void EndToEnd_SimulateReviewPipeline_ProducesValidOutput()
    {
        // Arrange
        var diffService = new DiffService();
        var codeAnalysisService = CreateCodeAnalysisService();
        var request = TestFixtureLoader.LoadRequest(PR4Fixture);
        var fileContents = TestFixtureLoader.BuildFileContents(PR4Fixture);

        // Compute diffs
        foreach (var file in fileContents)
        {
            file.DiffHunks = diffService.ComputeDiff(file.OriginalContent, file.Content);
        }

        // Act - Filter and prioritize (like CodeReviewProcessor does)
        var filteredFiles = codeAnalysisService.FilterReviewableFiles(fileContents);
        var prioritizedFiles = codeAnalysisService.PrioritizeFiles(filteredFiles);

        // Assert
        prioritizedFiles.Should().NotBeEmpty();
        prioritizedFiles.Should().AllSatisfy(f =>
        {
            f.DiffHunks.Should().NotBeEmpty($"File {f.Path} should have diff hunks");

            var addedLines = f.DiffHunks.SelectMany(h => h.Lines)
                .Where(l => l.Type == DiffLineType.Added)
                .ToList();

            addedLines.Should().NotBeEmpty($"File {f.Path} should have added lines");

            // Critical assertion: line numbers should not all be 1
            var lineNumbers = addedLines.Select(l => l.NewLineNumber!.Value).ToList();
            if (lineNumbers.Count > 1)
            {
                lineNumbers.Distinct().Count().Should().BeGreaterThan(1,
                    $"File {f.Path} should have varying line numbers, not all 1");
            }
        });
    }

    [Fact]
    public void EndToEnd_LineNumbersCorrespondToActualCode()
    {
        // Arrange
        var diffService = new DiffService();
        var fileContents = TestFixtureLoader.BuildFileContents(PR4Fixture);
        var distanceFile = fileContents.First(f => f.Path.Contains("distance.ts"));

        // Act
        var hunks = diffService.ComputeDiff(null, distanceFile.Content);
        var allLines = hunks.SelectMany(h => h.Lines).ToList();

        // Assert - verify line numbers match actual content
        var contentLines = distanceFile.Content.Split('\n');

        foreach (var diffLine in allLines.Where(l => l.NewLineNumber.HasValue))
        {
            var lineNum = diffLine.NewLineNumber!.Value;
            lineNum.Should().BeGreaterThan(0);
            lineNum.Should().BeLessThanOrEqualTo(contentLines.Length);

            // The content should match (trimming CR)
            var actualLine = contentLines[lineNum - 1].TrimEnd('\r');
            diffLine.Content.Should().Be(actualLine,
                $"Line {lineNum} content should match actual file content");
        }
    }

    #endregion

    #region Helper Methods

    private static string BuildTestPrompt(List<FileContent> files, ICodeAnalysisService codeAnalysisService)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Changed Files (Diff Format)");
        sb.AppendLine();

        foreach (var file in files)
        {
            var language = codeAnalysisService.GetLanguageFromPath(file.Path);
            sb.AppendLine($"### {file.Path} ({file.ChangeType})");
            sb.AppendLine();
            sb.AppendLine($"```{language}");

            foreach (var hunk in file.DiffHunks)
            {
                sb.AppendLine($"@@ -{hunk.OldStartLine},{hunk.OldLineCount} +{hunk.NewStartLine},{hunk.NewLineCount} @@");

                foreach (var line in hunk.Lines)
                {
                    var prefix = line.Type switch
                    {
                        DiffLineType.Added => "+",
                        DiffLineType.Deleted => "-",
                        _ => " "
                    };

                    var lineNum = line.NewLineNumber.HasValue ? $"L{line.NewLineNumber.Value,3}" : "    ";
                    sb.AppendLine($"{lineNum} {prefix} {line.Content}");
                }
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    #endregion
}
