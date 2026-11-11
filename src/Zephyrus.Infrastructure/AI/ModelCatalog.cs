using Microsoft.Extensions.Options;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.Infrastructure.Demo;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// The models this installation knows about: the configured chains and the
/// models with a price. A project may name any valid reference; these are the
/// suggestions.
/// </summary>
public sealed class ModelCatalog : IModelCatalog
{
    private const int MaxReferenceLength = 200;

    private readonly LanguageModelOptions _options;
    private readonly bool _demo;

    public ModelCatalog(IOptions<LanguageModelOptions> options, IOptions<ModelPricingOptions> pricing, IOptions<DemoOptions> demo)
    {
        _options = options.Value;
        _demo = demo.Value.Enabled;

        var configured = new[] { _options.Default }
            .Concat(LanguageModelOptions.StageNames.Select(_options.For))
            .SelectMany(chain => new[] { chain.Primary, chain.Fallback });
        var priced = pricing.Value.Models.Keys
            .Where(model => model.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            .Select(model => $"claude:{model}");

        KnownModels = configured.Concat(priced)
            .Where(reference => ModelReference.TryParse(reference, out var parsed) && parsed.Provider != LanguageModelProvider.None)
            .Select(reference => reference!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> KnownModels { get; }

    /// <summary>In demo mode every stage gets the canned demo answers, whatever is configured.</summary>
    public ModelChoice DefaultFor(AgentJobKind stage)
    {
        if (_demo)
            return new ModelChoice("demo", "none");

        var chain = _options.For(stage.ToString());
        return new ModelChoice(chain.Primary!, chain.Fallback);
    }

    public string? Problem(string? reference, bool allowNone)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return allowNone ? null : "a model is required.";
        if (reference.Length > MaxReferenceLength)
            return $"model references are at most {MaxReferenceLength} characters.";
        if (!ModelReference.TryParse(reference, out var parsed))
            return $"'{reference}' is not a model reference. Write local:<model> or claude:<model>{(allowNone ? ", or none" : "")}.";
        if (parsed.Provider == LanguageModelProvider.None && !allowNone)
            return "a primary model is required.";
        return null;
    }
}
