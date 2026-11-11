using Zephyrus.Application.Exceptions;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.Core.Pipeline;

namespace Zephyrus.Application.Managers;

/// <summary>
/// Which models each stage of a project uses. Every change is recorded with
/// who made it; only a Tech Lead may make one.
/// </summary>
public sealed class ModelChoiceManager
{
    /// <summary>The stages, in pipeline order.</summary>
    public static readonly IReadOnlyList<AgentJobKind> Stages = new[]
    {
        AgentJobKind.Prd, AgentJobKind.Architect, AgentJobKind.Task,
        AgentJobKind.Code, AgentJobKind.Qa, AgentJobKind.DevOps,
    };

    private readonly IProjectRepository _projects;
    private readonly IModelChoiceRepository _changes;
    private readonly IModelCatalog _catalog;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public ModelChoiceManager(
        IProjectRepository projects,
        IModelChoiceRepository changes,
        IModelCatalog catalog,
        IUserContext user,
        TimeProvider time)
    {
        _projects = projects;
        _changes = changes;
        _catalog = catalog;
        _user = user;
        _time = time;
    }

    public async Task<ProjectModels> GetAsync(Guid projectId, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        var history = await _changes.GetByProjectIdAsync(projectId, ct);
        var current = ModelChoices.Current(history);

        var stages = Stages.Select(stage =>
        {
            var last = history.LastOrDefault(c => c.Stage == stage);
            return current.TryGetValue(stage, out var chosen)
                ? new StageModels(stage, chosen, IsProjectChoice: true, last!.ChangedBy, last.ChangedAt)
                : new StageModels(stage, _catalog.DefaultFor(stage), IsProjectChoice: false, null, null);
        }).ToList();

        return new ProjectModels(stages, ModelChoiceAuthority.CanChoose(_user.Roles), _catalog.KnownModels);
    }

    public async Task<ProjectModels> ChooseAsync(Guid projectId, AgentJobKind stage, ModelChoice choice, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        RequireTechLead();

        var fallback = string.IsNullOrWhiteSpace(choice.Fallback) ? null : choice.Fallback.Trim();
        var normalised = new ModelChoice(choice.Primary?.Trim() ?? string.Empty, fallback);

        if (_catalog.Problem(normalised.Primary, allowNone: false) is { } primaryProblem)
            throw new RequestRejectedException($"Primary model: {primaryProblem}");
        if (normalised.Fallback is not null && _catalog.Problem(normalised.Fallback, allowNone: true) is { } fallbackProblem)
            throw new RequestRejectedException($"Fallback model: {fallbackProblem}");

        await _changes.AddAsync(ModelChoiceChange.Choose(projectId, stage, normalised, _user.UserId!, Now), ct);
        return await GetAsync(projectId, ct);
    }

    public async Task<ProjectModels> ResetAsync(Guid projectId, AgentJobKind stage, CancellationToken ct = default)
    {
        await RequireProjectAsync(projectId, ct);
        RequireTechLead();

        await _changes.AddAsync(ModelChoiceChange.Reset(projectId, stage, _user.UserId!, Now), ct);
        return await GetAsync(projectId, ct);
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private void RequireTechLead()
    {
        if (!_user.IsAuthenticated || !ModelChoiceAuthority.CanChoose(_user.Roles))
            throw new NotPermittedException("Only a Tech Lead may change which models a project uses.");
    }

    private async Task RequireProjectAsync(Guid projectId, CancellationToken ct)
    {
        _ = await _projects.GetByIdAsync(projectId, ct)
            ?? throw new ResourceNotFoundException($"Project '{projectId}' not found.");
    }
}

/// <summary>A project's model per stage, and whether the caller may change them.</summary>
public sealed record ProjectModels(IReadOnlyList<StageModels> Stages, bool CanChange, IReadOnlyList<string> KnownModels);

/// <summary>
/// The models a stage uses. <paramref name="IsProjectChoice"/> is false when
/// the stage uses the configured default.
/// </summary>
public sealed record StageModels(AgentJobKind Stage, ModelChoice Models, bool IsProjectChoice, string? ChangedBy, DateTime? ChangedAt);
