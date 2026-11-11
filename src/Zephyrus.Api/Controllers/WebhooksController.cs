using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Zephyrus.Api.Webhooks;

namespace Zephyrus.Api.Controllers;

/// <summary>
/// Receives code-host events. This closes the pipeline's last mile: merges
/// complete tasks, and a successful deployment is what moves a feature to
/// Deployed.
/// </summary>
/// <remarks>
/// Anonymous at the HTTP layer because GitHub cannot present a team token —
/// the HMAC signature check in <see cref="Receive"/> is what authenticates it.
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/webhooks/github")]
public class WebhooksController : ControllerBase
{
    private readonly GitHubWebhookOptions _options;

    public WebhooksController(IOptions<GitHubWebhookOptions> options)
    {
        _options = options.Value;
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromServices] GitHubWebhookHandler handler, CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct);
        var signature = Request.Headers[GitHubSignatureValidator.HeaderName];
        if (!GitHubSignatureValidator.IsValid(_options.Secret, signature, body))
            return Unauthorized();

        var rejection = await handler.HandleAsync(Request.Headers["X-GitHub-Event"].ToString(), body, ct);
        return rejection is null ? Ok() : BadRequest(new { detail = rejection });
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
}
