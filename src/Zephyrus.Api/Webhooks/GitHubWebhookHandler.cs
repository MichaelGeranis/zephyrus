using System.Text.Json;
using Zephyrus.Application.UseCases;

namespace Zephyrus.Api.Webhooks;

/// <summary>
/// Turns a verified GitHub delivery into the use case it is about: a merged
/// pull request completes its task, a deployment status may deploy a feature.
/// This is HTTP mapping only; the use cases hold the rules.
/// </summary>
public sealed class GitHubWebhookHandler
{
    private readonly HandlePullRequestClosedUseCase _pullRequestClosed;
    private readonly HandleDeploymentStatusUseCase _deploymentStatus;

    public GitHubWebhookHandler(
        HandlePullRequestClosedUseCase pullRequestClosed,
        HandleDeploymentStatusUseCase deploymentStatus)
    {
        _pullRequestClosed = pullRequestClosed;
        _deploymentStatus = deploymentStatus;
    }

    /// <summary>
    /// Handles one delivery. Returns the reason it was rejected, or null when it
    /// was accepted. Event types Zephyrus does not use are accepted and ignored,
    /// so GitHub does not retry them.
    /// </summary>
    public async Task<string?> HandleAsync(string eventName, byte[] body, CancellationToken ct)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return "Payload is not valid JSON.";
        }

        using (document)
        {
            var payload = document.RootElement;
            var repositorySlug = ReadString(payload, "repository", "full_name");
            if (repositorySlug is null)
                return "Payload has no repository.";

            switch (eventName)
            {
                case "pull_request":
                    await HandlePullRequestAsync(payload, repositorySlug, ct);
                    break;
                case "deployment_status":
                    await HandleDeploymentStatusAsync(payload, ct);
                    break;
            }

            return null;
        }
    }

    private async Task HandlePullRequestAsync(JsonElement payload, string repositorySlug, CancellationToken ct)
    {
        if (ReadString(payload, "action") != "closed")
            return;

        if (!payload.TryGetProperty("pull_request", out var pr))
            return;

        if (!pr.TryGetProperty("number", out var number) || number.ValueKind != JsonValueKind.Number)
            return;

        var merged = pr.TryGetProperty("merged", out var mergedElement)
                     && mergedElement.ValueKind == JsonValueKind.True;

        await _pullRequestClosed.ExecuteAsync(
            repositorySlug, number.GetInt32(), merged, ReadString(pr, "merge_commit_sha"), ct);
    }

    private async Task HandleDeploymentStatusAsync(JsonElement payload, CancellationToken ct)
    {
        var sha = ReadString(payload, "deployment", "sha");
        var state = ReadString(payload, "deployment_status", "state");

        if (sha is null || state is null)
            return;

        await _deploymentStatus.ExecuteAsync(sha, state, ct);
    }

    private static string? ReadString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
