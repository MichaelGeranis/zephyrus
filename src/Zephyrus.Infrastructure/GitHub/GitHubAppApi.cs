using Octokit;

namespace Zephyrus.Infrastructure.GitHub;

/// <summary>The two GitHub App calls Zephyrus makes, behind a seam for tests.</summary>
public interface IGitHubAppApi
{
    /// <summary>The App's installation on the repository, or null when it is not installed there.</summary>
    Task<long?> FindInstallationAsync(string owner, string repo, string appJwt, CancellationToken ct = default);

    Task<InstallationAccess> CreateInstallationTokenAsync(long installationId, string appJwt, CancellationToken ct = default);
}

public sealed record InstallationAccess(string Token, DateTimeOffset ExpiresAt);

/// <summary>Octokit implementation of <see cref="IGitHubAppApi"/>.</summary>
public sealed class OctokitGitHubAppApi : IGitHubAppApi
{
    public async Task<long?> FindInstallationAsync(string owner, string repo, string appJwt, CancellationToken ct = default)
    {
        try
        {
            var installation = await AppClient(appJwt).GitHubApps.GetRepositoryInstallationForCurrent(owner, repo);
            return installation.Id;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<InstallationAccess> CreateInstallationTokenAsync(long installationId, string appJwt, CancellationToken ct = default)
    {
        var token = await AppClient(appJwt).GitHubApps.CreateInstallationToken(installationId);
        return new InstallationAccess(token.Token, token.ExpiresAt);
    }

    private static GitHubClient AppClient(string appJwt) =>
        new(new ProductHeaderValue("Zephyrus")) { Credentials = new Credentials(appJwt, AuthenticationType.Bearer) };
}
