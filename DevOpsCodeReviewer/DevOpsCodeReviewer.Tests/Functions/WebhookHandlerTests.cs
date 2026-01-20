using System.Text.Json;
using DevOpsCodeReviewer.Functions.Models;

namespace DevOpsCodeReviewer.Tests.Functions;

public class WebhookHandlerTests
{
    #region Payload Parsing Tests

    [Fact]
    public void ParsePayload_ValidPullRequestCreated_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
                "subscriptionId": "00000000-0000-0000-0000-000000000000",
                "notificationId": 1,
                "id": "00000000-0000-0000-0000-000000000000",
                "eventType": "git.pullrequest.created",
                "publisherId": "tfs",
                "message": {
                    "text": "PR created"
                },
                "resource": {
                    "repository": {
                        "id": "repo-id",
                        "name": "MyRepo",
                        "project": {
                            "id": "project-id",
                            "name": "MyProject"
                        }
                    },
                    "pullRequestId": 123,
                    "status": "active",
                    "title": "Add new feature",
                    "description": "This PR adds a new feature",
                    "sourceRefName": "refs/heads/feature/new-feature",
                    "targetRefName": "refs/heads/main",
                    "isDraft": false,
                    "createdBy": {
                        "displayName": "John Doe",
                        "id": "user-id"
                    }
                },
                "createdDate": "2024-01-15T10:30:00Z"
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<PullRequestPayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        payload.Should().NotBeNull();
        payload!.EventType.Should().Be("git.pullrequest.created");
        payload.Resource.PullRequestId.Should().Be(123);
        payload.Resource.Status.Should().Be("active");
        payload.Resource.Title.Should().Be("Add new feature");
        payload.Resource.Repository.Name.Should().Be("MyRepo");
        payload.Resource.Repository.Project.Name.Should().Be("MyProject");
        payload.Resource.SourceRefName.Should().Be("refs/heads/feature/new-feature");
        payload.Resource.TargetRefName.Should().Be("refs/heads/main");
        payload.Resource.IsDraft.Should().BeFalse();
        payload.Resource.CreatedBy!.DisplayName.Should().Be("John Doe");
    }

    [Fact]
    public void ParsePayload_PullRequestUpdated_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
                "eventType": "git.pullrequest.updated",
                "resource": {
                    "repository": {
                        "id": "repo-id",
                        "name": "MyRepo",
                        "project": {
                            "id": "project-id",
                            "name": "MyProject"
                        }
                    },
                    "pullRequestId": 456,
                    "status": "active",
                    "title": "Updated feature",
                    "sourceRefName": "refs/heads/feature/update",
                    "targetRefName": "refs/heads/main",
                    "isDraft": false
                }
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<PullRequestPayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        payload.Should().NotBeNull();
        payload!.EventType.Should().Be("git.pullrequest.updated");
        payload.Resource.PullRequestId.Should().Be(456);
    }

    [Fact]
    public void ParsePayload_DraftPullRequest_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
                "eventType": "git.pullrequest.created",
                "resource": {
                    "repository": {
                        "id": "repo-id",
                        "name": "MyRepo",
                        "project": {
                            "id": "project-id",
                            "name": "MyProject"
                        }
                    },
                    "pullRequestId": 789,
                    "status": "active",
                    "title": "Draft PR",
                    "sourceRefName": "refs/heads/feature/draft",
                    "targetRefName": "refs/heads/main",
                    "isDraft": true
                }
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<PullRequestPayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        payload.Should().NotBeNull();
        payload!.Resource.IsDraft.Should().BeTrue();
    }

    [Fact]
    public void ParsePayload_WithResourceContainers_ParsesCorrectly()
    {
        // Arrange
        var json = """
            {
                "eventType": "git.pullrequest.created",
                "resource": {
                    "repository": {
                        "id": "repo-id",
                        "name": "MyRepo",
                        "project": {
                            "id": "project-id",
                            "name": "MyProject"
                        }
                    },
                    "pullRequestId": 123,
                    "status": "active",
                    "title": "Test PR",
                    "sourceRefName": "refs/heads/feature",
                    "targetRefName": "refs/heads/main",
                    "isDraft": false
                },
                "resourceContainers": {
                    "collection": {
                        "id": "collection-id",
                        "baseUrl": "https://dev.azure.com/myorg/"
                    },
                    "account": {
                        "id": "account-id",
                        "baseUrl": "https://dev.azure.com/myorg/"
                    },
                    "project": {
                        "id": "project-id",
                        "baseUrl": "https://dev.azure.com/myorg/MyProject/"
                    }
                }
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<PullRequestPayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        payload.Should().NotBeNull();
        payload!.ResourceContainers.Should().NotBeNull();
        payload.ResourceContainers!.Account!.BaseUrl.Should().Be("https://dev.azure.com/myorg/");
    }

    #endregion

    #region Event Type Validation Tests

    [Theory]
    [InlineData("git.pullrequest.created", true)]
    [InlineData("git.pullrequest.updated", true)]
    [InlineData("git.push", false)]
    [InlineData("build.complete", false)]
    [InlineData("workitem.created", false)]
    public void IsSupportedEventType_ReturnsCorrectResult(string eventType, bool expected)
    {
        // Arrange
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "git.pullrequest.created",
            "git.pullrequest.updated"
        };

        // Act
        var result = supportedTypes.Contains(eventType);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Status Validation Tests

    [Theory]
    [InlineData("active", true)]
    [InlineData("Active", true)]
    [InlineData("completed", false)]
    [InlineData("abandoned", false)]
    public void IsActivePR_ReturnsCorrectResult(string status, bool expected)
    {
        // Act
        var result = status.Equals("active", StringComparison.OrdinalIgnoreCase);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region CodeReviewRequest Creation Tests

    [Fact]
    public void CreateCodeReviewRequest_FromPayload_MapsCorrectly()
    {
        // Arrange
        var payload = new PullRequestPayload
        {
            EventType = "git.pullrequest.created",
            Resource = new PullRequestResource
            {
                Repository = new Repository
                {
                    Id = "repo-id",
                    Name = "MyRepo",
                    Project = new Project
                    {
                        Id = "project-id",
                        Name = "MyProject"
                    }
                },
                PullRequestId = 123,
                Title = "Test PR",
                Description = "Test description",
                SourceRefName = "refs/heads/feature",
                TargetRefName = "refs/heads/main",
                CreatedBy = new IdentityRef
                {
                    DisplayName = "John Doe"
                }
            }
        };

        // Act
        var request = new CodeReviewRequest
        {
            OrganizationUrl = "https://dev.azure.com/myorg",
            ProjectId = payload.Resource.Repository.Project.Id,
            ProjectName = payload.Resource.Repository.Project.Name,
            RepositoryId = payload.Resource.Repository.Id,
            RepositoryName = payload.Resource.Repository.Name,
            PullRequestId = payload.Resource.PullRequestId,
            Title = payload.Resource.Title,
            Description = payload.Resource.Description,
            SourceBranch = payload.Resource.SourceRefName,
            TargetBranch = payload.Resource.TargetRefName,
            AuthorName = payload.Resource.CreatedBy?.DisplayName ?? "Unknown",
            EventType = payload.EventType
        };

        // Assert
        request.OrganizationUrl.Should().Be("https://dev.azure.com/myorg");
        request.ProjectId.Should().Be("project-id");
        request.ProjectName.Should().Be("MyProject");
        request.RepositoryId.Should().Be("repo-id");
        request.RepositoryName.Should().Be("MyRepo");
        request.PullRequestId.Should().Be(123);
        request.Title.Should().Be("Test PR");
        request.Description.Should().Be("Test description");
        request.SourceBranch.Should().Be("refs/heads/feature");
        request.TargetBranch.Should().Be("refs/heads/main");
        request.AuthorName.Should().Be("John Doe");
        request.EventType.Should().Be("git.pullrequest.created");
        request.CorrelationId.Should().NotBeNullOrEmpty();
    }

    #endregion
}
