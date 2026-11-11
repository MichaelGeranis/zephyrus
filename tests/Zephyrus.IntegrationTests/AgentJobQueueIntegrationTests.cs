using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.Infrastructure.Persistence;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// The EF Core job store, against a real (SQLite) database: claiming, the
/// concurrency guard between workers, cascade delete and the API.
/// </summary>
public class AgentJobQueueIntegrationTests : IClassFixture<ZephyrusApiFactory>
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ZephyrusApiFactory _factory;
    private readonly HttpClient _client;

    public AgentJobQueueIntegrationTests(ZephyrusApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateFeatureAsync()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Jobs " + Guid.NewGuid().ToString("N")[..6],
            description = "d",
            config = "project:\n  name: jobs",
            repositorySlug = "org/jobs-" + Guid.NewGuid().ToString("N")[..6],
            gitHubToken = "ghp_test",
        })).Content.ReadFromJsonAsync<IdDto>(JsonOptions);

        var feature = await (await _client.PostAsJsonAsync("/api/features", new { projectId = project!.Id, prompt = "Queue a job" }))
            .Content.ReadFromJsonAsync<IdDto>(JsonOptions);
        return feature!.Id;
    }

    private async Task<AgentJobRecord> AddJobAsync(Guid featureId, AgentJobKind kind, DateTime enqueuedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var record = AgentJobRecord.Create(new AgentJob(featureId, kind), enqueuedAt);
        await scope.ServiceProvider.GetRequiredService<IAgentJobRepository>().AddAsync(record);
        return record;
    }

    [Fact]
    public async Task AddAsync_WhenJobIsAForcedRerun_ShouldStoreAndReturnTheFlag()
    {
        var featureId = await CreateFeatureAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAgentJobRepository>().AddAsync(
                AgentJobRecord.Create(new AgentJob(featureId, AgentJobKind.Prd, ForceRerun: true), DateTime.UtcNow), Ct);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var stored = Assert.Single(await scope.ServiceProvider.GetRequiredService<IAgentJobRepository>()
                .GetByFeatureIdAsync(featureId, Ct));
            Assert.Equal(new AgentJob(featureId, AgentJobKind.Prd, ForceRerun: true), stored.ToJob());
        }
    }

    [Fact]
    public async Task TryClaimNextAsync_WhenJobsAreDue_ShouldClaimTheOldestAndPersistTheClaim()
    {
        var featureId = await CreateFeatureAsync();
        var now = DateTime.UtcNow.AddYears(-1); // older than any other test's jobs
        var older = await AddJobAsync(featureId, AgentJobKind.Architect, now);
        await AddJobAsync(featureId, AgentJobKind.Task, now.AddSeconds(1));

        using (var scope = _factory.Services.CreateScope())
        {
            var claimed = await scope.ServiceProvider.GetRequiredService<IAgentJobRepository>()
                .TryClaimNextAsync(now.AddSeconds(2), Lease, Ct);
            Assert.Equal(older.Id, claimed!.Id);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var stored = (await scope.ServiceProvider.GetRequiredService<IAgentJobRepository>().GetByFeatureIdAsync(featureId, Ct))
                .Single(j => j.Id == older.Id);
            Assert.Equal(AgentJobStatus.Running, stored.Status);
            Assert.Equal(1, stored.Attempts);
        }
    }

    [Fact]
    public async Task SaveChanges_WhenTwoWorkersClaimTheSameJob_ShouldRejectTheSecond()
    {
        var featureId = await CreateFeatureAsync();
        var record = await AddJobAsync(featureId, AgentJobKind.Code, DateTime.UtcNow);
        var now = DateTime.UtcNow.AddMinutes(1);

        // Worker B has read the job but not saved yet...
        using var scopeB = _factory.Services.CreateScope();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ZephyrusDbContext>();
        var seenByB = await dbB.AgentJobs.SingleAsync(j => j.Id == record.Id, cancellationToken: Ct);

        // ...when worker A claims it.
        using (var scopeA = _factory.Services.CreateScope())
        {
            var dbA = scopeA.ServiceProvider.GetRequiredService<ZephyrusDbContext>();
            var seenByA = await dbA.AgentJobs.SingleAsync(j => j.Id == record.Id, cancellationToken: Ct);
            Assert.True(seenByA.TryClaim(now, Lease));
            await dbA.SaveChangesAsync(Ct);
        }

        Assert.True(seenByB.TryClaim(now, Lease));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task DeleteFeature_WhenItHasJobs_ShouldDeleteTheJobs()
    {
        var featureId = await CreateFeatureAsync();
        await AddJobAsync(featureId, AgentJobKind.Qa, DateTime.UtcNow);

        (await _client.DeleteAsync($"/api/features/{featureId}", Ct)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IAgentJobRepository>().GetByFeatureIdAsync(featureId, Ct));
    }

    [Fact]
    public async Task GetAgentJobs_WhenFeatureHasJobs_ShouldReturnTheirStatus()
    {
        var featureId = await CreateFeatureAsync();
        var record = await AddJobAsync(featureId, AgentJobKind.DevOps, DateTime.UtcNow);

        var response = await _client.GetAsync($"/api/features/{featureId}/agent-jobs", Ct);
        response.EnsureSuccessStatusCode();
        var jobs = await response.Content.ReadFromJsonAsync<JobDto[]>(JsonOptions, cancellationToken: Ct);

        var job = Assert.Single(jobs!);
        Assert.Equal(record.Id, job.Id);
        Assert.Equal("DevOps", job.Kind);
        Assert.Equal("Queued", job.Status);
        Assert.Equal(AgentJobRecord.MaxAttempts, job.MaxAttempts);
    }

    [Fact]
    public async Task GetAgentJobs_WhenFeatureIsUnknown_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}/agent-jobs", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record IdDto(Guid Id);

    private sealed record JobDto(Guid Id, string Kind, string Status, int Attempts, int MaxAttempts, string? LastError);
}
