namespace Zephyrus.Api.Webhooks;

/// <summary>
/// Settings for inbound GitHub webhooks.
/// </summary>
public sealed class GitHubWebhookOptions
{
    public const string SectionName = "GitHub:Webhook";

    /// <summary>
    /// Shared secret configured on the GitHub webhook. Deliveries are rejected
    /// unless their signature matches. An empty secret rejects everything, so
    /// the shipped defaults do not accept unsigned traffic.
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}
