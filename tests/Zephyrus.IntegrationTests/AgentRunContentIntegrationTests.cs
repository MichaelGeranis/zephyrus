using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.Persistence;
using Zephyrus.Infrastructure.Security;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// What an agent run read and wrote is encrypted in the database, runs stored
/// before that are encrypted by the maintenance pass, content past the retention
/// period is removed, and a member configured by token hash can sign in.
/// </summary>
public class AgentRunContentIntegrationTests : IClassFixture<ZephyrusApiFactory>
{
    private const string Secret = "Constitution: internal-only payroll system";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ZephyrusApiFactory _factory;
    private readonly HttpClient _client;

    public AgentRunContentIntegrationTests(ZephyrusApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SavedRun_ShouldBeEncryptedInTheDatabase_AndReadBackAsWritten()
    {
        var run = await SaveRunAsync();

        var (userMessage, response) = await ReadStoredAsync(run.Id);
        Assert.StartsWith(DataProtectionSecretProtector.Prefix, userMessage);
        Assert.StartsWith(DataProtectionSecretProtector.Prefix, response);
        Assert.DoesNotContain("payroll", userMessage);

        using var scope = _factory.Services.CreateScope();
        var loaded = await scope.ServiceProvider.GetRequiredService<IAgentInvocationRepository>().GetByIdAsync(run.Id, Ct);
        Assert.Equal(Secret, loaded!.UserMessage);
        Assert.Equal("The PRD", loaded.Response);
    }

    [Fact]
    public async Task EncryptStoredContent_ShouldEncryptRunsStoredBeforeEncryption()
    {
        var run = await SaveRunAsync();
        await ExecuteAsync($"UPDATE agent_invocations SET user_message = 'plain {run.Id}', response = 'plain answer' WHERE id = '{run.Id.ToString().ToUpperInvariant()}'");
        Assert.Equal($"plain {run.Id}", (await ReadStoredAsync(run.Id)).UserMessage);

        using (var scope = _factory.Services.CreateScope())
        {
            var encrypted = await Maintenance(scope, retentionDays: 0, DateTime.UtcNow).EncryptStoredContentAsync(Ct);
            Assert.True(encrypted >= 1);
        }

        var (userMessage, response) = await ReadStoredAsync(run.Id);
        Assert.StartsWith(DataProtectionSecretProtector.Prefix, userMessage);
        Assert.StartsWith(DataProtectionSecretProtector.Prefix, response);
        using var read = _factory.Services.CreateScope();
        var loaded = await read.ServiceProvider.GetRequiredService<IAgentInvocationRepository>().GetByIdAsync(run.Id, Ct);
        Assert.Equal($"plain {run.Id}", loaded!.UserMessage);
    }

    [Fact]
    public async Task RemoveExpiredContent_ShouldRemoveOnlyContentOlderThanTheRetentionPeriod()
    {
        var run = await SaveRunAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            // Retention off: nothing is removed, however old
            Assert.Equal(0, await Maintenance(scope, retentionDays: 0, DateTime.UtcNow.AddYears(5)).RemoveExpiredContentAsync(Ct));
            // Within the period: kept
            await Maintenance(scope, retentionDays: 30, DateTime.UtcNow.AddDays(10)).RemoveExpiredContentAsync(Ct);
        }
        Assert.Null(await LoadRemovedAtAsync(run.Id));

        using (var scope = _factory.Services.CreateScope())
            await Maintenance(scope, retentionDays: 30, DateTime.UtcNow.AddDays(31)).RemoveExpiredContentAsync(Ct);

        using var read = _factory.Services.CreateScope();
        var loaded = await read.ServiceProvider.GetRequiredService<IAgentInvocationRepository>().GetByIdAsync(run.Id, Ct);
        Assert.Equal(AgentInvocation.RemovedContent, loaded!.UserMessage);
        Assert.Equal(AgentInvocation.RemovedContent, loaded.Response);
        Assert.NotNull(loaded.ContentRemovedAt);
        Assert.Equal("system prompt", loaded.SystemPrompt);
    }

    [Fact]
    public async Task MemberConfiguredByTokenHash_ShouldSignIn()
    {
        var response = await _factory.CreateClientWithToken(ZephyrusApiFactory.HashedToken).GetAsync("/api/projects", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<AgentInvocation> SaveRunAsync()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Content",
            description = "d",
            config = "c",
            repositorySlug = "org/content",
            gitHubToken = "ghp_test",
        }, cancellationToken: Ct)).Content.ReadFromJsonAsync<IdDto>(JsonOptions, cancellationToken: Ct);
        var feature = await (await _client.PostAsJsonAsync("/api/features", new { projectId = project!.Id, prompt = "Keep it private" }, cancellationToken: Ct))
            .Content.ReadFromJsonAsync<IdDto>(JsonOptions, cancellationToken: Ct);

        var run = AgentInvocation.Create(feature!.Id, "prd", "system prompt", Secret, "The PRD", 100);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAgentInvocationRepository>().AddAsync(run, Ct);
        return run;
    }

    private static AgentRunContentMaintenance Maintenance(IServiceScope scope, int retentionDays, DateTime now) =>
        new(
            scope.ServiceProvider.GetRequiredService<ZephyrusDbContext>(),
            Options.Create(new AgentRunContentOptions { ContentRetentionDays = retentionDays }),
            new FixedTime(now),
            NullLogger<AgentRunContentMaintenance>.Instance);

    private async Task<(string UserMessage, string Response)> ReadStoredAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<ZephyrusDbContext>().Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT user_message, response FROM agent_invocations WHERE id = '{id.ToString().ToUpperInvariant()}'";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetString(0), reader.GetString(1));
    }

    private async Task<DateTime?> LoadRemovedAtAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<IAgentInvocationRepository>().GetByIdAsync(id, Ct);
        return run!.ContentRemovedAt;
    }

    private async Task ExecuteAsync(string sql)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ZephyrusDbContext>().Database.ExecuteSqlRawAsync(sql, Ct);
    }

    private sealed class FixedTime(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed record IdDto(Guid Id);
}
