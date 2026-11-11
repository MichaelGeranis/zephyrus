using Zephyrus.Application.Exceptions;
using Zephyrus.Application.Managers;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.UnitTests.Jobs;

namespace Zephyrus.UnitTests.Managers;

public class ModelChoiceManagerTests
{
    private readonly InMemoryProjectRepository _projects = new();
    private readonly InMemoryModelChoiceRepository _changes = new();
    private readonly FakeUserContext _user = new() { UserId = "taylor@test.com", Roles = new[] { TeamRole.TechLead } };
    private readonly ManualTimeProvider _clock = new(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));
    private readonly ModelChoiceManager _sut;
    private readonly Project _project = Project.Create("Billing", "d", "config", "org/billing", "token");

    public ModelChoiceManagerTests()
    {
        _sut = new ModelChoiceManager(_projects, _changes, new FakeModelCatalog(), _user, _clock);
        _projects.AddAsync(_project).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetAsync_WhenProjectChoseNothing_ShouldShowTheConfiguredDefaultForEveryStage()
    {
        var models = await _sut.GetAsync(_project.Id, Ct);

        Assert.Equal(ModelChoiceManager.Stages, models.Stages.Select(s => s.Stage));
        Assert.All(models.Stages, s =>
        {
            Assert.False(s.IsProjectChoice);
            Assert.Equal(FakeModelCatalog.Default, s.Models);
        });
    }

    [Fact]
    public async Task ChooseAsync_WhenCallerIsTechLead_ShouldRecordTheChoiceAndWhoMadeIt()
    {
        var models = await _sut.ChooseAsync(_project.Id, AgentJobKind.Code, new ModelChoice(" claude:claude-opus-5-5 ", "none"), Ct);

        var code = models.Stages.Single(s => s.Stage == AgentJobKind.Code);
        Assert.True(code.IsProjectChoice);
        Assert.Equal(new ModelChoice("claude:claude-opus-5-5", "none"), code.Models);
        Assert.Equal("taylor@test.com", code.ChangedBy);
        Assert.Equal(_clock.Now, code.ChangedAt);
        Assert.False(models.Stages.Single(s => s.Stage == AgentJobKind.Prd).IsProjectChoice);
    }

    [Theory]
    [InlineData(TeamRole.PmEm)]
    [InlineData(TeamRole.Qa)]
    public async Task ChooseAsync_WhenCallerIsNotTechLead_ShouldThrowNotPermittedAndRecordNothing(TeamRole role)
    {
        _user.Roles = new[] { role };

        await Assert.ThrowsAsync<NotPermittedException>(() =>
            _sut.ChooseAsync(_project.Id, AgentJobKind.Code, new ModelChoice("local:qwen3:32b", null), Ct));
        Assert.Empty(await _changes.GetByProjectIdAsync(_project.Id, Ct));
    }

    [Fact]
    public async Task ChooseAsync_WhenPrimaryIsInvalid_ShouldThrowRequestRejected()
    {
        var error = await Assert.ThrowsAsync<RequestRejectedException>(() =>
            _sut.ChooseAsync(_project.Id, AgentJobKind.Code, new ModelChoice("gpt:whatever", null), Ct));

        Assert.StartsWith("Primary model:", error.Message);
    }

    [Fact]
    public async Task ChooseAsync_WhenProjectDoesNotExist_ShouldThrowResourceNotFound()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _sut.ChooseAsync(Guid.NewGuid(), AgentJobKind.Code, new ModelChoice("local:qwen3:32b", null), Ct));
    }

    [Fact]
    public async Task ResetAsync_WhenStageHadAChoice_ShouldReturnItToTheDefaultAndKeepTheHistory()
    {
        await _sut.ChooseAsync(_project.Id, AgentJobKind.Code, new ModelChoice("local:qwen3:32b", null), Ct);
        _clock.Advance(TimeSpan.FromMinutes(1));

        var models = await _sut.ResetAsync(_project.Id, AgentJobKind.Code, Ct);

        Assert.False(models.Stages.Single(s => s.Stage == AgentJobKind.Code).IsProjectChoice);
        Assert.Equal(2, (await _changes.GetByProjectIdAsync(_project.Id, Ct)).Count);
    }

    [Fact]
    public async Task GetAsync_ShouldTellTheCallerWhetherTheyMayChange()
    {
        Assert.True((await _sut.GetAsync(_project.Id, Ct)).CanChange);

        _user.Roles = new[] { TeamRole.PmEm };
        Assert.False((await _sut.GetAsync(_project.Id, Ct)).CanChange);
    }
}

public class ModelChoicesTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Current_WhenAStageChangedTwice_ShouldUseTheLatestChange()
    {
        var changes = new[]
        {
            ModelChoiceChange.Choose(ProjectId, AgentJobKind.Code, new ModelChoice("local:a", null), "tl", T0.AddMinutes(1)),
            ModelChoiceChange.Choose(ProjectId, AgentJobKind.Code, new ModelChoice("local:b", "none"), "tl", T0.AddMinutes(2)),
        };

        Assert.Equal(new ModelChoice("local:b", "none"), ModelChoices.Current(changes)[AgentJobKind.Code]);
    }

    [Fact]
    public void Current_WhenTheLatestChangeIsAReset_ShouldHaveNoChoiceForThatStage()
    {
        var changes = new[]
        {
            ModelChoiceChange.Choose(ProjectId, AgentJobKind.Code, new ModelChoice("local:a", null), "tl", T0),
            ModelChoiceChange.Reset(ProjectId, AgentJobKind.Code, "tl", T0.AddMinutes(1)),
            ModelChoiceChange.Choose(ProjectId, AgentJobKind.Qa, new ModelChoice("local:c", null), "tl", T0.AddMinutes(2)),
        };

        var current = ModelChoices.Current(changes);

        Assert.False(current.ContainsKey(AgentJobKind.Code));
        Assert.Equal("local:c", current[AgentJobKind.Qa].Primary);
    }

    [Fact]
    public void CanChoose_ShouldAllowOnlyTechLeads()
    {
        Assert.True(Zephyrus.Core.Pipeline.ModelChoiceAuthority.CanChoose(new[] { TeamRole.TechLead }));
        Assert.False(Zephyrus.Core.Pipeline.ModelChoiceAuthority.CanChoose(new[] { TeamRole.PmEm, TeamRole.Qa }));
        Assert.False(Zephyrus.Core.Pipeline.ModelChoiceAuthority.CanChoose(Array.Empty<TeamRole>()));
    }
}

internal sealed class InMemoryModelChoiceRepository : IModelChoiceRepository
{
    private readonly List<ModelChoiceChange> _changes = new();

    public Task AddAsync(ModelChoiceChange change, CancellationToken ct = default)
    {
        _changes.Add(change);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModelChoiceChange>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ModelChoiceChange>>(_changes.Where(c => c.ProjectId == projectId).OrderBy(c => c.ChangedAt).ToList());
}

internal sealed class FakeModelCatalog : IModelCatalog
{
    public static readonly ModelChoice Default = new("local:llama3.1:8b", "claude:claude-opus-5-5");

    public ModelChoice DefaultFor(AgentJobKind stage) => Default;

    public IReadOnlyList<string> KnownModels { get; } = new[] { "claude:claude-opus-5-5", "local:llama3.1:8b" };

    public string? Problem(string? reference, bool allowNone) =>
        reference is not null && (reference.StartsWith("local:") || reference.StartsWith("claude:") || (allowNone && reference == "none"))
            ? null
            : "not a model reference.";
}
