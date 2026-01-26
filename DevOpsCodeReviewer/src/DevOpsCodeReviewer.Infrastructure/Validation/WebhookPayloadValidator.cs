using DevOpsCodeReviewer.Infrastructure.AzureDevOps.Models;

namespace DevOpsCodeReviewer.Infrastructure.Validation;

/// <summary>
/// Validates Azure DevOps webhook payloads at the boundary.
/// </summary>
public static class WebhookPayloadValidator
{
    /// <summary>
    /// Validates that the webhook payload contains all required fields.
    /// </summary>
    /// <param name="payload">The deserialized webhook payload (may be null).</param>
    /// <returns>Validation result with any errors found.</returns>
    public static ValidationResult Validate(PullRequestPayload? payload)
    {
        if (payload == null)
            return ValidationResult.Failure("Payload is null");

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(payload.EventType))
            errors.Add("EventType is required");

        if (payload.Resource == null)
        {
            errors.Add("Resource is required");
        }
        else
        {
            ValidateResource(payload.Resource, errors);
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    private static void ValidateResource(PullRequestResource resource, List<string> errors)
    {
        if (resource.PullRequestId <= 0)
            errors.Add("PullRequestId must be positive");

        if (string.IsNullOrWhiteSpace(resource.Title))
            errors.Add("Title is required");

        if (string.IsNullOrWhiteSpace(resource.Status))
            errors.Add("Status is required");

        if (string.IsNullOrWhiteSpace(resource.SourceRefName))
            errors.Add("SourceRefName is required");

        if (string.IsNullOrWhiteSpace(resource.TargetRefName))
            errors.Add("TargetRefName is required");

        if (resource.Repository == null)
        {
            errors.Add("Repository is required");
        }
        else
        {
            ValidateRepository(resource.Repository, errors);
        }
    }

    private static void ValidateRepository(Repository repository, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(repository.Id))
            errors.Add("Repository.Id is required");

        if (string.IsNullOrWhiteSpace(repository.Name))
            errors.Add("Repository.Name is required");

        if (repository.Project == null)
        {
            errors.Add("Project is required");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(repository.Project.Id))
                errors.Add("Project.Id is required");

            if (string.IsNullOrWhiteSpace(repository.Project.Name))
                errors.Add("Project.Name is required");
        }
    }
}
