using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Zephyrus.IntegrationTests;

public class AuditExportIntegrationTests : IClassFixture<ZephyrusApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ZephyrusApiFactory _factory;
    private readonly HttpClient _client;

    public AuditExportIntegrationTests(ZephyrusApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid FeatureId)> FeatureWithApprovedPrdAsync()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Audit " + Guid.NewGuid().ToString("N")[..6],
            description = "d",
            config = "project:\n  name: audit",
            repositorySlug = "org/audit",
            gitHubToken = "ghp_secret_token_value",
        }, Ct)).Content.ReadFromJsonAsync<IdDto>(JsonOptions, Ct);
        var feature = await (await _client.PostAsJsonAsync("/api/features", new { projectId = project!.Id, prompt = "Export invoices" }, Ct))
            .Content.ReadFromJsonAsync<IdDto>(JsonOptions, Ct);

        // The test server runs jobs inline: the PRD, then on approval the ADR.
        await _client.PostAsync($"/api/features/{feature!.Id}/generate-prd", null, Ct);
        var artifacts = await _client.GetFromJsonAsync<ArtifactDto[]>($"/api/features/{feature.Id}/artifacts", JsonOptions, Ct);
        await _client.PostAsync($"/api/features/{feature.Id}/artifacts/{artifacts!.Single(a => a.Type == "Prd").Id}/approve", null, Ct);
        return (project.Id, feature.Id);
    }

    private async Task<(HttpResponseMessage Response, JsonElement Body)> ExportAsync(string path, HttpClient? client = null)
    {
        var response = await (client ?? _client).GetAsync(path, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response, response.IsSuccessStatusCode ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }

    [Fact]
    public async Task ExportFeature_ShouldDownloadTheFullTrailAsJson()
    {
        var (_, featureId) = await FeatureWithApprovedPrdAsync();

        var (response, body) = await ExportAsync($"/api/features/{featureId}/audit-export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"zephyrus-audit-feature-{featureId}.json", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("zephyrus.audit/v1", body.GetProperty("format").GetString());
        Assert.Equal("pm@test.com", body.GetProperty("exportedBy").GetString());

        var feature = Assert.Single(body.GetProperty("features").EnumerateArray());
        var events = feature.GetProperty("pipelineEvents").EnumerateArray().ToList();
        Assert.Contains(events, e => e.GetProperty("to").GetString() == "PrdApproved" && e.GetProperty("triggeredBy").GetString() == "pm@test.com");

        var prd = feature.GetProperty("artifacts").EnumerateArray().Single(a => a.GetProperty("type").GetString() == "Prd");
        Assert.Equal("pm@test.com", prd.GetProperty("approvedBy").GetString());
    }

    [Fact]
    public async Task ExportFeature_ShouldLinkEachArtifactToTheRunThatProducedIt()
    {
        var (_, featureId) = await FeatureWithApprovedPrdAsync();

        var (_, body) = await ExportAsync($"/api/features/{featureId}/audit-export");

        var feature = body.GetProperty("features")[0];
        var runs = feature.GetProperty("agentRuns").EnumerateArray().ToList();
        foreach (var artifact in feature.GetProperty("artifacts").EnumerateArray())
        {
            var producedBy = Assert.Single(artifact.GetProperty("producedBy").EnumerateArray()).GetGuid();
            var run = runs.Single(r => r.GetProperty("id").GetGuid() == producedBy);
            Assert.Equal(artifact.GetProperty("id").GetGuid(), run.GetProperty("artifactId").GetGuid());
            Assert.Matches("^[0-9a-f]{12}$", run.GetProperty("promptVersion").GetString());
            Assert.False(string.IsNullOrEmpty(run.GetProperty("systemPrompt").GetString()));
        }
        Assert.Equal(new[] { "prd", "architect" }, runs.Select(r => r.GetProperty("agent").GetString()));
    }

    [Fact]
    public async Task ExportFeature_ShouldNeverContainTheProjectsCodeHostToken()
    {
        var (_, featureId) = await FeatureWithApprovedPrdAsync();

        var response = await _client.GetAsync($"/api/features/{featureId}/audit-export", Ct);

        Assert.DoesNotContain("ghp_secret_token_value", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task ExportFeature_WhenAStepWasRerun_ShouldKeepTheEarlierRunAndWhatItProduced()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Rerun audit",
            description = "d",
            config = "c",
            repositorySlug = "org/rerun-audit",
            gitHubToken = "ghp_x",
        }, Ct)).Content.ReadFromJsonAsync<IdDto>(JsonOptions, Ct);
        var feature = await (await _client.PostAsJsonAsync("/api/features", new { projectId = project!.Id, prompt = "Rerun me" }, Ct))
            .Content.ReadFromJsonAsync<IdDto>(JsonOptions, Ct);
        await _client.PostAsync($"/api/features/{feature!.Id}/generate-prd", null, Ct);
        await _client.PostAsJsonAsync($"/api/features/{feature.Id}/rerun-step", new { step = "prd" }, Ct);

        var (_, body) = await ExportAsync($"/api/features/{feature.Id}/audit-export");

        var exported = body.GetProperty("features")[0];
        var prdRuns = exported.GetProperty("agentRuns").EnumerateArray().Where(r => r.GetProperty("agent").GetString() == "prd").ToList();
        var current = exported.GetProperty("artifacts").EnumerateArray().Single(a => a.GetProperty("type").GetString() == "Prd").GetProperty("id").GetGuid();
        Assert.Equal(2, prdRuns.Count);
        Assert.NotEqual(current, prdRuns[0].GetProperty("artifactId").GetGuid()); // replaced by the re-run
        Assert.Equal(current, prdRuns[1].GetProperty("artifactId").GetGuid());
    }

    [Fact]
    public async Task ExportProject_ShouldIncludeEveryFeatureAndTheModelChoices()
    {
        var (projectId, featureId) = await FeatureWithApprovedPrdAsync();
        await _client.PutAsJsonAsync($"/api/projects/{projectId}/models/Code", new { primary = "claude:claude-opus-5-5" }, Ct);
        await _client.PostAsJsonAsync("/api/features", new { projectId, prompt = "Second idea" }, Ct);

        var (response, body) = await ExportAsync($"/api/projects/{projectId}/audit-export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(projectId, body.GetProperty("project").GetProperty("id").GetGuid());
        Assert.Equal(2, body.GetProperty("features").GetArrayLength());
        Assert.Equal(featureId, body.GetProperty("features")[0].GetProperty("id").GetGuid());
        var change = Assert.Single(body.GetProperty("modelChoiceChanges").EnumerateArray());
        Assert.Equal("Code", change.GetProperty("stage").GetString());
        Assert.Equal("pm@test.com", change.GetProperty("changedBy").GetString());
    }

    [Fact]
    public async Task Export_WhenCallerHoldsAnyRole_ShouldBeAllowed()
    {
        var (_, featureId) = await FeatureWithApprovedPrdAsync();

        var (response, _) = await ExportAsync($"/api/features/{featureId}/audit-export", _factory.CreateClientWithToken(ZephyrusApiFactory.QaOnlyToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Export_WhenFeatureDoesNotExist_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/features/{Guid.NewGuid()}/audit-export", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record IdDto(Guid Id);

    private sealed record ArtifactDto(Guid Id, string Type);
}
