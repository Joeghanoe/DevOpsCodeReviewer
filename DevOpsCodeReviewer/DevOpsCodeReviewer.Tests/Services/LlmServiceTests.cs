using System.Text.Json;
using DevOpsCodeReviewer.Functions.Models;

namespace DevOpsCodeReviewer.Tests.Services;

public class LlmServiceTests
{
    #region Response Parsing Tests

    [Fact]
    public void ParseResponse_ValidJson_ReturnsComments()
    {
        // Arrange
        var json = """
            {
                "comments": [
                    {
                        "filePath": "src/app.cs",
                        "lineNumber": 42,
                        "category": "Bug",
                        "severity": "Major",
                        "message": "Null reference possible",
                        "suggestion": "Add null check"
                    }
                ]
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<CodeReviewResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        response.Should().NotBeNull();
        response!.Comments.Should().HaveCount(1);
        response.Comments[0].FilePath.Should().Be("src/app.cs");
        response.Comments[0].LineNumber.Should().Be(42);
        response.Comments[0].Category.Should().Be(ReviewCategory.Bug);
        response.Comments[0].Severity.Should().Be(ReviewSeverity.Major);
        response.Comments[0].Message.Should().Be("Null reference possible");
        response.Comments[0].Suggestion.Should().Be("Add null check");
    }

    [Fact]
    public void ParseResponse_EmptyComments_ReturnsEmptyList()
    {
        // Arrange
        var json = """
            {
                "comments": []
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<CodeReviewResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        response.Should().NotBeNull();
        response!.Comments.Should().BeEmpty();
    }

    [Fact]
    public void ParseResponse_MultipleComments_ReturnsAll()
    {
        // Arrange
        var json = """
            {
                "comments": [
                    {
                        "filePath": "src/app.cs",
                        "lineNumber": 10,
                        "category": "Security",
                        "severity": "Critical",
                        "message": "SQL injection vulnerability"
                    },
                    {
                        "filePath": "src/app.cs",
                        "lineNumber": 25,
                        "category": "Performance",
                        "severity": "Minor",
                        "message": "Consider caching"
                    }
                ]
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<CodeReviewResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        response.Should().NotBeNull();
        response!.Comments.Should().HaveCount(2);
    }

    [Fact]
    public void ParseResponse_WithSuggestedCode_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
                "comments": [
                    {
                        "filePath": "src/app.cs",
                        "lineNumber": 10,
                        "category": "Bug",
                        "severity": "Major",
                        "message": "Missing null check",
                        "suggestion": "Add null check before accessing property",
                        "suggestedCode": "if (user != null) { return user.Name; }"
                    }
                ]
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<CodeReviewResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        response.Should().NotBeNull();
        response!.Comments[0].SuggestedCode.Should().NotBeNull();
        response.Comments[0].SuggestedCode.Should().Contain("user != null");
    }

    [Fact]
    public void ParseResponse_AllSeverityLevels_ParseCorrectly()
    {
        // Arrange
        var severities = new[] { "Info", "Minor", "Major", "Critical", "Blocker" };

        foreach (var severity in severities)
        {
            var json = $$"""
                {
                    "comments": [
                        {
                            "filePath": "test.cs",
                            "lineNumber": 1,
                            "category": "Bug",
                            "severity": "{{severity}}",
                            "message": "Test"
                        }
                    ]
                }
                """;

            // Act
            var response = JsonSerializer.Deserialize<CodeReviewResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Assert
            response.Should().NotBeNull();
            response!.Comments.Should().HaveCount(1);
            response.Comments[0].Severity.ToString().Should().Be(severity);
        }
    }

    [Fact]
    public void ParseResponse_AllCategories_ParseCorrectly()
    {
        // Arrange
        var categories = new[]
        {
            "Bug", "Security", "Performance", "Style", "BestPractice",
            "Maintainability", "ErrorHandling", "Documentation", "Testing", "Other"
        };

        foreach (var category in categories)
        {
            var json = $$"""
                {
                    "comments": [
                        {
                            "filePath": "test.cs",
                            "lineNumber": 1,
                            "category": "{{category}}",
                            "severity": "Minor",
                            "message": "Test"
                        }
                    ]
                }
                """;

            // Act
            var response = JsonSerializer.Deserialize<CodeReviewResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Assert
            response.Should().NotBeNull();
            response!.Comments.Should().HaveCount(1);
            response.Comments[0].Category.ToString().Should().Be(category);
        }
    }

    #endregion

    #region Severity Filtering Tests

    [Fact]
    public void FilterBySeverity_FiltersLowerSeverities()
    {
        // Arrange
        var comments = new List<ReviewComment>
        {
            new() { Severity = ReviewSeverity.Info, Message = "Info" },
            new() { Severity = ReviewSeverity.Minor, Message = "Minor" },
            new() { Severity = ReviewSeverity.Major, Message = "Major" },
            new() { Severity = ReviewSeverity.Critical, Message = "Critical" }
        };

        var minSeverity = ReviewSeverity.Major;

        // Act
        var filtered = comments.Where(c => (int)c.Severity >= (int)minSeverity).ToList();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().Contain(c => c.Severity == ReviewSeverity.Major);
        filtered.Should().Contain(c => c.Severity == ReviewSeverity.Critical);
    }

    #endregion
}
