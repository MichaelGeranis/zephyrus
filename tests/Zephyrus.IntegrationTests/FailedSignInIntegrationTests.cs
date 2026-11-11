using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// Runs on its own server with a low limit, so counting failures here cannot
/// block the other test classes.
/// </summary>
public sealed class FailedSignInIntegrationTests : IDisposable
{
    private const int Limit = 3;

    private readonly ZephyrusApiFactory _factory = new();
    private readonly WebApplicationFactory<Program> _server;

    public FailedSignInIntegrationTests()
    {
        _server = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authentication:FailedSignIns:Limit", Limit.ToString()));
    }

    private HttpClient ClientWithToken(string? token)
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SendWrongTokens(int count)
    {
        var stranger = ClientWithToken("not-a-team-token");
        for (var i = 0; i < count; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/projects", Ct)).StatusCode);
    }

    [Fact]
    public async Task Request_WhenWrongTokensReachTheLimit_ShouldBeRefusedWith429AndRetryAfter()
    {
        await SendWrongTokens(Limit);

        var response = await ClientWithToken("not-a-team-token").GetAsync("/api/projects", Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Request_WhenClientIsBlocked_ShouldRefuseEvenAValidToken()
    {
        await SendWrongTokens(Limit);

        var response = await ClientWithToken(ZephyrusApiFactory.AllRolesToken).GetAsync("/api/projects", Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Request_WhenWrongTokensAreBelowTheLimit_ShouldStillAcceptAValidToken()
    {
        await SendWrongTokens(Limit - 1);

        var response = await ClientWithToken(ZephyrusApiFactory.AllRolesToken).GetAsync("/api/projects", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_WhenClientIsBlockedButSendsNoToken_ShouldReachTheEndpoint()
    {
        await SendWrongTokens(Limit);

        // Webhooks carry no bearer token and are authenticated by signature.
        var response = await ClientWithToken(null).GetAsync("/api/projects", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        _server.Dispose();
        _factory.Dispose();
    }
}
