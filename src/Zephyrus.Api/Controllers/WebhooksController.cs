using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Zephyrus.Api.Webhooks;
using Zephyrus.Application.UseCases;

namespace Zephyrus.Api.Controllers;

/// <summary>
/// Receives code-host events. This closes the pipeline's last mile: merges
/// complete tasks, and a successful deployment is what moves a feature to
/// Deployed.
/// </summary>
[ApiController]
[Route("api/webhooks/github")]
public class WebhooksController : ControllerBase
{
    private readonly GitHubWebhookOptions _options;

    public WebhooksController(IOptions<GitHubWebhookOptions> options)
    {
        _options = options.Value;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(
        [FromServices] HandlePullRequestClosedUseCase pullRequestClosed,
        [FromServices] HandleDeploymentStatusUseCase deploymentStatus,
        CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct);

        if (!GitHubSignatureValidator.IsValid(
                _options.Secret,
                Request.Headers[GitHubSignatureValidator.HeaderName],
                body))
        {
            return Unauthorized();
        }

        var eventName = Request.Headers["X-GitHub-Event"].ToString();

        using var document = JsonDocument.Parse(body);
        var payload = document.RootElement;

        var repositorySlug = ReadString(payload, "repository", "full_name");
        if (repositorySlug is null)
            return BadRequest(new { detail = "Payload has no repository." });

        switch (eventName)
        {
            case "pull_request":
                await HandlePullRequestAsync(payload, repositorySlug, pullRequestClosed, ct);
                break;

            case "deployment_status":
                await HandleDeploymentStatusAsync(payload, deploymentStatus, ct);
                break;
        }

        // Unhandled event types are acknowledged so GitHub does not retry them.
        return Ok();
    }

    private static async Task HandlePullRequestAsync(
        JsonElement payload,
        string repositorySlug,
        HandlePullRequestClosedUseCase useCase,
        CancellationToken ct)
    {
        if (ReadString(payload, "action") != "closed")
            return;

        if (!payload.TryGetProperty("pull_request", out var pr))
            return;

        if (!pr.TryGetProperty("number", out var number) || number.ValueKind != JsonValueKind.Number)
            return;

        var merged = pr.TryGetProperty("merged", out var mergedElement)
                     && mergedElement.ValueKind == JsonValueKind.True;

        var mergeCommitSha = pr.TryGetProperty("merge_commit_sha", out var shaElement)
                             && shaElement.ValueKind == JsonValueKind.String
            ? shaElement.GetString()
            : null;

        await useCase.ExecuteAsync(repositorySlug, number.GetInt32(), merged, mergeCommitSha, ct);
    }

    private static async Task HandleDeploymentStatusAsync(
        JsonElement payload,
        HandleDeploymentStatusUseCase useCase,
        CancellationToken ct)
    {
        var sha = ReadString(payload, "deployment", "sha");
        var state = ReadString(payload, "deployment_status", "state");

        if (sha is null || state is null)
            return;

        await useCase.ExecuteAsync(sha, state, ct);
    }

    /// <summary>
    /// The raw body is needed byte-for-byte: the signature covers exactly what
    /// GitHub sent, so it cannot be re-serialised from a parsed model.
    /// </summary>
    private async Task<byte[]> ReadBodyAsync(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        return buffer.ToArray();
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
