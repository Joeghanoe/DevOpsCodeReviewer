using Azure.Security.KeyVault.Secrets;
using DevOpsCodeReviewer.Core.Models;
using DevOpsCodeReviewer.Core.Services;
using DevOpsCodeReviewer.Infrastructure.AzureDevOps.Models;
using DevOpsCodeReviewer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DevOpsCodeReviewer.Infrastructure.AzureDevOps;

/// <summary>
/// Service for Azure DevOps REST API operations.
/// </summary>
public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsOptions _options;
    private readonly SecretClient? _secretClient;
    private readonly IDiffService _diffService;
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _cachedPat;

    public AzureDevOpsService(
        HttpClient httpClient,
        IOptions<AzureDevOpsOptions> options,
        IDiffService diffService,
        ILogger<AzureDevOpsService> logger,
        SecretClient? secretClient = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _diffService = diffService;
        _secretClient = secretClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private async Task<string> GetPatAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_cachedPat))
            return _cachedPat;

        // Try direct configuration first (local development)
        if (!string.IsNullOrEmpty(_options.Pat))
        {
            _cachedPat = _options.Pat;
            return _cachedPat;
        }

        // Try Key Vault
        if (_secretClient != null && !string.IsNullOrEmpty(_options.PatSecretName))
        {
            var secret = await _secretClient.GetSecretAsync(_options.PatSecretName, cancellationToken: cancellationToken);
            _cachedPat = secret.Value.Value;
            return _cachedPat;
        }

        throw new InvalidOperationException("No PAT token configured. Set AzureDevOps:Pat or configure Key Vault.");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string url,
        CancellationToken cancellationToken)
    {
        var pat = await GetPatAsync(cancellationToken);
        var request = new HttpRequestMessage(method, url);
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private string BuildUrl(string organizationUrl, string path)
    {
        var baseUrl = organizationUrl.TrimEnd('/');
        return $"{baseUrl}/{path}?api-version={_options.ApiVersion}";
    }

    public async Task<PullRequestDetails?> GetPullRequestAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(organizationUrl, $"{projectId}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}");

        _logger.LogDebug("Getting pull request {PullRequestId} from {Url}", pullRequestId, url);

        var request = await CreateRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get pull request: {StatusCode}", response.StatusCode);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<PullRequestDetails>(content, _jsonOptions);
    }

    public async Task<List<PullRequestIteration>> GetPullRequestIterationsAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(organizationUrl, $"{projectId}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/iterations");

        _logger.LogDebug("Getting iterations for PR {PullRequestId}", pullRequestId);

        var request = await CreateRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get iterations: {StatusCode}", response.StatusCode);
            return [];
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<IterationsResponse>(content, _jsonOptions);
        return result?.Value ?? [];
    }

    public async Task<List<IterationChange>> GetIterationChangesAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(organizationUrl, $"{projectId}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/iterations/{iterationId}/changes");

        _logger.LogDebug("Getting changes for iteration {IterationId}", iterationId);

        var request = await CreateRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get iteration changes: {StatusCode}", response.StatusCode);
            return [];
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<IterationChangesResponse>(content, _jsonOptions);
        return result?.ChangeEntries ?? [];
    }

    public async Task<string?> GetFileContentAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        string path,
        string? commitId,
        CancellationToken cancellationToken = default)
    {
        var encodedPath = Uri.EscapeDataString(path);
        var url = $"{organizationUrl.TrimEnd('/')}/{projectId}/_apis/git/repositories/{repositoryId}/items?path={encodedPath}&$format=text&api-version={_options.ApiVersion}";

        if (!string.IsNullOrEmpty(commitId))
        {
            url += $"&versionDescriptor.version={commitId}&versionDescriptor.versionType=commit";
        }

        _logger.LogDebug("Getting file content: {Path} at commit {CommitId}", path, commitId ?? "latest");

        var request = await CreateRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to get file content for {Path}: {StatusCode}", path, response.StatusCode);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("Retrieved {Length} characters for {Path}", content.Length, path);

        return content;
    }

    public async Task<List<CommentThread>> GetPullRequestThreadsAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(organizationUrl, $"{projectId}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/threads");

        _logger.LogDebug("Getting threads for PR {PullRequestId}", pullRequestId);

        var request = await CreateRequestAsync(HttpMethod.Get, url, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get threads: {StatusCode}", response.StatusCode);
            return [];
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<ThreadsResponse>(content, _jsonOptions);
        return result?.Value ?? [];
    }

    public async Task<CommentThread?> CreateCommentThreadAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        CreateThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(organizationUrl, $"{projectId}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/threads");

        _logger.LogDebug("Creating comment thread on PR {PullRequestId}", pullRequestId);

        var httpRequest = await CreateRequestAsync(HttpMethod.Post, url, cancellationToken);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(request, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create thread: {StatusCode} - {Error}", response.StatusCode, errorContent);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<CommentThread>(content, _jsonOptions);
    }

    public async Task<List<FileContent>> GetChangedFilesContentAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        int iterationId,
        string? sourceCommitId,
        CancellationToken cancellationToken = default)
    {
        var changes = await GetIterationChangesAsync(
            organizationUrl, projectId, repositoryId, pullRequestId, iterationId, cancellationToken);

        // Get PR details to find target branch commit
        var prDetails = await GetPullRequestAsync(
            organizationUrl, projectId, repositoryId, pullRequestId, cancellationToken);
        var targetCommitId = prDetails?.LastMergeTargetCommit?.CommitId;

        var fileContents = new List<FileContent>();

        foreach (var change in changes)
        {
            // Skip deleted files
            if (change.ChangeType.Equals("delete", StringComparison.OrdinalIgnoreCase))
                continue;

            var path = change.Item.Path;

            // Skip if not a reviewable file
            if (!IsReviewableFile(path))
            {
                _logger.LogDebug("Skipping non-reviewable file: {Path}", path);
                continue;
            }

            // Get new file content
            var content = await GetFileContentAsync(
                organizationUrl, projectId, repositoryId, path, sourceCommitId, cancellationToken);

            if (content == null)
                continue;

            // Check file size
            if (content.Length > _options.MaxFileSizeBytes)
            {
                _logger.LogDebug("Skipping large file: {Path} ({Size} bytes)", path, content.Length);
                continue;
            }

            // Get original file content for edits (not for new files)
            string? originalContent = null;
            if (!change.ChangeType.Equals("add", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(targetCommitId))
            {
                originalContent = await GetFileContentAsync(
                    organizationUrl, projectId, repositoryId, path, targetCommitId, cancellationToken);
            }

            // Compute diff
            var diffHunks = _diffService.ComputeDiff(originalContent, content);

            fileContents.Add(new FileContent
            {
                Path = path,
                Content = content,
                OriginalContent = originalContent,
                ChangeType = change.ChangeType,
                LineCount = content.Split('\n').Length,
                ObjectId = change.Item.ObjectId,
                OriginalObjectId = change.Item.OriginalObjectId,
                DiffHunks = diffHunks
            });

            // Limit number of files
            if (fileContents.Count >= _options.MaxFilesPerReview)
            {
                _logger.LogWarning("Reached max files limit ({Max}), skipping remaining files", _options.MaxFilesPerReview);
                break;
            }
        }

        return fileContents;
    }

    private bool IsReviewableFile(string path)
    {
        // Check excluded patterns
        foreach (var pattern in _options.ExcludedPatterns)
        {
            if (pattern.StartsWith("*"))
            {
                if (path.EndsWith(pattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else if (path.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                     path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check included extensions
        var extension = Path.GetExtension(path);
        return _options.IncludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<PullRequestStatus?> SetPullRequestStatusAsync(
        string organizationUrl,
        string projectId,
        string repositoryId,
        int pullRequestId,
        string state,
        string description,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(organizationUrl, $"{projectId}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/statuses");

        _logger.LogDebug("Setting PR status for PR {PullRequestId}: {State} - {Description}",
            pullRequestId, state, description);

        var statusRequest = new CreatePullRequestStatusRequest
        {
            State = state,
            Description = description,
            Context = new StatusContext
            {
                Genre = "code-review",
                Name = "AI Code Review"
            }
        };

        var httpRequest = await CreateRequestAsync(HttpMethod.Post, url, cancellationToken);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(statusRequest, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to set PR status: {StatusCode} - {Error}", response.StatusCode, errorContent);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<PullRequestStatus>(content, _jsonOptions);
    }
}
