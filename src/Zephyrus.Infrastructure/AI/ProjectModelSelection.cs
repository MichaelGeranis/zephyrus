using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Infrastructure.AI;

/// <inheritdoc />
public sealed class ProjectModelSelection : IProjectModelSelection
{
    private IReadOnlyDictionary<AgentJobKind, ModelChoice> _choices = new Dictionary<AgentJobKind, ModelChoice>();

    public void Select(IReadOnlyDictionary<AgentJobKind, ModelChoice> choices) => _choices = choices;

    public ModelChoice? For(AgentJobKind stage) => _choices.TryGetValue(stage, out var choice) ? choice : null;
}
