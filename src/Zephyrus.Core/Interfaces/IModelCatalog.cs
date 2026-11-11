using Zephyrus.Core.Entities;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Core.Interfaces;

/// <summary>The models this installation is configured for.</summary>
public interface IModelCatalog
{
    /// <summary>The configured choice for a stage, used when a project has none.</summary>
    ModelChoice DefaultFor(AgentJobKind stage);

    /// <summary>Models worth suggesting: the configured ones and those with a price.</summary>
    IReadOnlyList<string> KnownModels { get; }

    /// <summary>Why <paramref name="reference"/> is not a usable model reference, or null if it is.</summary>
    string? Problem(string? reference, bool allowNone);
}
