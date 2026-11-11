using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// Usage recorded by a language model call is attached to the agent run saved
/// next in the same scope, stored, and served by the API.
/// </summary>
public class AgentUsageIntegrationTests : IClassFixture<ZephyrusApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ZephyrusApiFactory _factory;
    private readonly HttpClient _client;

    public AgentUsageIntegrationTests(ZephyrusApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SavingARun_WhenModelCallsWereRecorded_ShouldStoreAndServeTheirUsage()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Usage",
            description = "d",
            config = "c",
            repositorySlug = "org/usage",
            gitHubToken = "ghp_test",
        }, cancellationToken: Ct)).Content.ReadFromJsonAsync<IdDto>(JsonOptions, cancellationToken: Ct);
        var feature = await (await _client.PostAsJsonAsync("/api/features", new { projectId = project!.Id, prompt = "Track usage" }, cancellationToken: Ct))
            .Content.ReadFromJsonAsync<IdDto>(JsonOptions, cancellationToken: Ct);

        using (var scope = _factory.Services.CreateScope())
        {
            var tracker = scope.ServiceProvider.GetRequiredService<LanguageModelUsageTracker>();
            tracker.Record("llama3.1:8b", 3_000, 500, free: true);
            tracker.Record("claude-opus-5-5", 1_000_000, 50_000, free: false);

            await scope.ServiceProvider.GetRequiredService<IAgentInvocationRepository>()
                .AddAsync(AgentInvocation.Create(feature!.Id, "task", "s", "u", "r", 1200), Ct);
        }

        var runs = await _client.GetFromJsonAsync<RunDto[]>($"/api/features/{feature!.Id}/agent-invocations", JsonOptions, cancellationToken: Ct);
        var run = Assert.Single(runs!);
        Assert.Equal("llama3.1:8b → claude-opus-5-5", run.Model);
        Assert.Equal(1_003_000, run.InputTokens);
        Assert.Equal(5m, run.EstimatedCostUsd); // priced by the default table: $4 + $1

        var usage = await _client.GetFromJsonAsync<UsageDto>($"/api/features/{feature.Id}/usage", JsonOptions, cancellationToken: Ct);
        Assert.Equal(1, usage!.Runs);
        Assert.Equal(5m, usage.EstimatedCostUsd);
        Assert.True(usage.CostIsComplete);
    }

    private sealed record IdDto(Guid Id);

    private sealed record RunDto(Guid Id, string? Model, int? InputTokens, int? OutputTokens, decimal? EstimatedCostUsd);

    private sealed record UsageDto(int Runs, long InputTokens, long OutputTokens, decimal EstimatedCostUsd, bool CostIsComplete);
}
