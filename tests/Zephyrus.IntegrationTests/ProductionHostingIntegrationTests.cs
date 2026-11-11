using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Infrastructure.Persistence;
using Xunit;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// What a production deployment relies on: health endpoints for the container
/// runtime, and client addresses read through a trusted reverse proxy.
/// </summary>
public sealed class ProductionHostingIntegrationTests : IDisposable
{
    private const string ProxyAddress = "10.0.0.5";
    private readonly ZephyrusApiFactory _factory = new();

    [Fact]
    public async Task Health_WhenCalledWithoutToken_ShouldReportHealthyAndTheVersion()
    {
        var response = await _factory.CreateClientWithToken(null).GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("0.1.0", body.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Ready_WhenDatabaseIsReachable_ShouldReportItHealthy()
    {
        var response = await _factory.CreateClientWithToken(null).GetAsync("/health/ready", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Ready_WhenDatabaseIsUnreachable_ShouldReturn503WhileLivenessStaysUp()
    {
        var server = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d =>
                         d.ServiceType == typeof(DbContextOptions<ZephyrusDbContext>) ||
                         d.ServiceType == typeof(IDbContextOptionsConfiguration<ZephyrusDbContext>)).ToList())
                services.Remove(d);
            services.AddDbContext<ZephyrusDbContext>(options =>
                options.UseSqlite("Data Source=/nonexistent/zephyrus.db;Mode=ReadOnly"));
        }));
        var client = server.CreateClient();

        var ready = await client.GetAsync("/health/ready", Ct);
        var live = await client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        var body = await ready.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Unhealthy", body.GetProperty("checks").GetProperty("database").GetString());
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task FailedSignIns_WhenBehindATrustedProxy_ShouldCountEachForwardedClientSeparately()
    {
        var server = BehindProxy(knownProxy: ProxyAddress);

        await SendWrongTokens(server, forwardedFor: "203.0.113.1");
        var blocked = await Send(server, "203.0.113.1", ZephyrusApiFactory.AllRolesToken);
        var other = await Send(server, "203.0.113.2", ZephyrusApiFactory.AllRolesToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task FailedSignIns_WhenForwardedHeadersComeFromAnUnknownProxy_ShouldIgnoreThem()
    {
        var server = BehindProxy(knownProxy: "10.0.0.9");

        await SendWrongTokens(server, forwardedFor: "203.0.113.1");
        // A different X-Forwarded-For does not escape the block: the header is not trusted.
        var response = await Send(server, "203.0.113.2", ZephyrusApiFactory.AllRolesToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public void Startup_WhenReverseProxyIsEnabledWithoutKnownProxies_ShouldRefuseToStart()
    {
        var server = _factory.WithWebHostBuilder(builder => builder.UseSetting("ReverseProxy:Enabled", "true"));

        var error = Assert.Throws<InvalidOperationException>(() => server.CreateClient());
        Assert.Contains("ReverseProxy:KnownProxies", error.Message);
    }

    private WebApplicationFactory<Program> BehindProxy(string knownProxy) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:FailedSignIns:Limit", "2");
            builder.UseSetting("ReverseProxy:Enabled", "true");
            builder.UseSetting("ReverseProxy:KnownProxies:0", knownProxy);
            // Every request reaches the API from the proxy's address.
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new FromAddress(IPAddress.Parse(ProxyAddress))));
        });

    private static async Task SendWrongTokens(WebApplicationFactory<Program> server, string forwardedFor)
    {
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(server, forwardedFor, "wrong-token")).StatusCode);
    }

    private static Task<HttpResponseMessage> Send(WebApplicationFactory<Program> server, string forwardedFor, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return server.CreateClient().SendAsync(request, Ct);
    }

    private sealed class FromAddress(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    public void Dispose() => _factory.Dispose();
}
