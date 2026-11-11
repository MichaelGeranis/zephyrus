using Zephyrus.Core.Entities;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// The model choices of the project whose agent job is running in this scope.
/// Set once per job, before the agent runs; stages with no choice use the
/// configured default.
/// </summary>
public interface IProjectModelSelection
{
    void Select(IReadOnlyDictionary<AgentJobKind, ModelChoice> choices);

    ModelChoice? For(AgentJobKind stage);
}
