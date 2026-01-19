using DevOpsCodeReviewer.Functions.Configuration;
using DevOpsCodeReviewer.Functions.Models;
using DevOpsCodeReviewer.Functions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsCodeReviewer.Tests.Services;

public class CodeAnalysisServiceTests
{
    private readonly CodeAnalysisService _service;
    private readonly AzureDevOpsOptions _options;

    public CodeAnalysisServiceTests()
    {
        _options = new AzureDevOpsOptions
        {
            IncludedExtensions = [".cs", ".ts", ".tsx", ".js", ".jsx", ".py"],
            ExcludedPatterns = ["package-lock.json", "*.min.js", "*.designer.cs"],
            MaxFileSizeBytes = 100 * 1024
        };

        var optionsMock = new Mock<IOptions<AzureDevOpsOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);

        var loggerMock = new Mock<ILogger<CodeAnalysisService>>();

        _service = new CodeAnalysisService(optionsMock.Object, loggerMock.Object);
    }

    #region FilterReviewableFiles Tests

    [Fact]
    public void FilterReviewableFiles_IncludesValidFiles()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "src/Services/UserService.cs", Content = "public class UserService { }" },
            new() { Path = "src/components/App.tsx", Content = "export const App = () => <div />;" }
        };

        // Act
        var result = _service.FilterReviewableFiles(files);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void FilterReviewableFiles_ExcludesLockFiles()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "package-lock.json", Content = "{}" },
            new() { Path = "src/app.ts", Content = "const x = 1;" }
        };

        // Act
        var result = _service.FilterReviewableFiles(files);

        // Assert
        result.Should().HaveCount(1);
        result[0].Path.Should().Be("src/app.ts");
    }

    [Fact]
    public void FilterReviewableFiles_ExcludesMinifiedFiles()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "dist/bundle.min.js", Content = "!function(){}" },
            new() { Path = "src/main.js", Content = "console.log('hello');" }
        };

        // Act
        var result = _service.FilterReviewableFiles(files);

        // Assert
        result.Should().HaveCount(1);
        result[0].Path.Should().Be("src/main.js");
    }

    [Fact]
    public void FilterReviewableFiles_ExcludesGeneratedFiles()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "Models/User.designer.cs", Content = "// Generated" },
            new() { Path = "Models/User.cs", Content = "public class User { }" }
        };

        // Act
        var result = _service.FilterReviewableFiles(files);

        // Assert
        result.Should().HaveCount(1);
        result[0].Path.Should().Be("Models/User.cs");
    }

    [Fact]
    public void FilterReviewableFiles_ExcludesUnsupportedExtensions()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "image.png", Content = "binary" },
            new() { Path = "document.pdf", Content = "binary" },
            new() { Path = "src/app.cs", Content = "public class App { }" }
        };

        // Act
        var result = _service.FilterReviewableFiles(files);

        // Assert
        result.Should().HaveCount(1);
        result[0].Path.Should().Be("src/app.cs");
    }

    #endregion

    #region ChunkFilesForReview Tests

    [Fact]
    public void ChunkFilesForReview_SingleChunkWhenUnderLimits()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "file1.cs", Content = "line1", LineCount = 10 },
            new() { Path = "file2.cs", Content = "line2", LineCount = 20 }
        };

        // Act
        var result = _service.ChunkFilesForReview(files, maxFilesPerChunk: 10, maxLinesPerChunk: 100);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().HaveCount(2);
    }

    [Fact]
    public void ChunkFilesForReview_SplitsByFileCount()
    {
        // Arrange
        var files = Enumerable.Range(1, 5)
            .Select(i => new FileContent { Path = $"file{i}.cs", Content = "x", LineCount = 10 })
            .ToList();

        // Act
        var result = _service.ChunkFilesForReview(files, maxFilesPerChunk: 2, maxLinesPerChunk: 1000);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().HaveCount(2);
        result[1].Should().HaveCount(2);
        result[2].Should().HaveCount(1);
    }

    [Fact]
    public void ChunkFilesForReview_SplitsByLineCount()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "file1.cs", Content = "x", LineCount = 100 },
            new() { Path = "file2.cs", Content = "x", LineCount = 100 },
            new() { Path = "file3.cs", Content = "x", LineCount = 100 }
        };

        // Act
        var result = _service.ChunkFilesForReview(files, maxFilesPerChunk: 10, maxLinesPerChunk: 150);

        // Assert
        result.Should().HaveCount(3); // Each file in its own chunk due to line limit
    }

    #endregion

    #region GenerateCommentFingerprint Tests

    [Fact]
    public void GenerateCommentFingerprint_SameInputProducesSameHash()
    {
        // Arrange
        var comment1 = new ReviewComment
        {
            FilePath = "src/app.cs",
            LineNumber = 10,
            Category = ReviewCategory.Bug,
            Message = "This is a bug"
        };

        var comment2 = new ReviewComment
        {
            FilePath = "src/app.cs",
            LineNumber = 10,
            Category = ReviewCategory.Bug,
            Message = "This is a bug"
        };

        // Act
        var hash1 = _service.GenerateCommentFingerprint(comment1);
        var hash2 = _service.GenerateCommentFingerprint(comment2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GenerateCommentFingerprint_DifferentInputProducesDifferentHash()
    {
        // Arrange
        var comment1 = new ReviewComment
        {
            FilePath = "src/app.cs",
            LineNumber = 10,
            Category = ReviewCategory.Bug,
            Message = "This is a bug"
        };

        var comment2 = new ReviewComment
        {
            FilePath = "src/app.cs",
            LineNumber = 11, // Different line
            Category = ReviewCategory.Bug,
            Message = "This is a bug"
        };

        // Act
        var hash1 = _service.GenerateCommentFingerprint(comment1);
        var hash2 = _service.GenerateCommentFingerprint(comment2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    #endregion

    #region PrioritizeFiles Tests

    [Fact]
    public void PrioritizeFiles_NewFilesFirst()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "file1.cs", ChangeType = "edit" },
            new() { Path = "file2.cs", ChangeType = "add" },
            new() { Path = "file3.cs", ChangeType = "edit" }
        };

        // Act
        var result = _service.PrioritizeFiles(files);

        // Assert
        result[0].ChangeType.Should().Be("add");
    }

    [Fact]
    public void PrioritizeFiles_SourceFilesBeforeTests()
    {
        // Arrange
        var files = new List<FileContent>
        {
            new() { Path = "tests/UserTests.cs", ChangeType = "edit" },
            new() { Path = "src/User.cs", ChangeType = "edit" }
        };

        // Act
        var result = _service.PrioritizeFiles(files);

        // Assert
        result[0].Path.Should().Contain("src/");
    }

    #endregion

    #region GetLanguageFromPath Tests

    [Theory]
    [InlineData("file.cs", "csharp")]
    [InlineData("file.ts", "typescript")]
    [InlineData("file.tsx", "typescript")]
    [InlineData("file.js", "javascript")]
    [InlineData("file.py", "python")]
    [InlineData("file.go", "go")]
    [InlineData("file.unknown", "text")]
    public void GetLanguageFromPath_ReturnsCorrectLanguage(string path, string expectedLanguage)
    {
        // Act
        var result = _service.GetLanguageFromPath(path);

        // Assert
        result.Should().Be(expectedLanguage);
    }

    #endregion
}
