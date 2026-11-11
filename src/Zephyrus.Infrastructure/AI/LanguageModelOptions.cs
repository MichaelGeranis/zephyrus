namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// Which model answers first and which takes over when it fails, as a default
/// and per pipeline stage. Models are written <c>provider:model</c>, for example
/// <c>local:llama3.1:8b</c> or <c>claude:claude-opus-5-5</c>.
/// </summary>
public sealed class LanguageModelOptions
{
    public const string SectionName = "LanguageModel";

    /// <summary>Stage names, matching the agents.</summary>
    public static readonly string[] StageNames = { "Prd", "Architect", "Task", "Code", "Qa", "DevOps" };

    public ModelChain Default { get; set; } = new()
    {
        Primary = "local:llama3.1:8b",
        Fallback = "claude:claude-opus-5-5"
    };

    /// <summary>
    /// Overrides by stage name. A field left out falls back to <see cref="Default"/>;
    /// a <c>Fallback</c> of <c>none</c> turns the fallback off for that stage.
    /// </summary>
    public Dictionary<string, ModelChain> Stages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasStageOverride(string stage) => Stages.ContainsKey(stage);

    public ModelChain For(string stage)
    {
        if (!Stages.TryGetValue(stage, out var chain))
            return Default;

        return new ModelChain
        {
            Primary = string.IsNullOrWhiteSpace(chain.Primary) ? Default.Primary : chain.Primary,
            Fallback = chain.Fallback ?? Default.Fallback
        };
    }

    /// <summary>Throws at startup on a misspelt stage or model reference, rather than on the first agent run.</summary>
    public void Validate()
    {
        foreach (var stage in Stages.Keys)
        {
            if (!StageNames.Contains(stage, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"LanguageModel:Stages has unknown stage '{stage}'. Use one of: {string.Join(", ", StageNames)}.");
        }

        foreach (var (name, chain) in new[] { ("Default", Default) }.Concat(Stages.Keys.Select(k => (k, For(k)))))
        {
            if (ModelReference.Parse(chain.Primary).Provider == LanguageModelProvider.None)
                throw new InvalidOperationException($"LanguageModel {name}: a primary model is required.");
            ModelReference.Parse(chain.Fallback);
        }
    }
}

public sealed class ModelChain
{
    public string? Primary { get; set; }
    public string? Fallback { get; set; }
}

public enum LanguageModelProvider
{
    None,
    Local,
    Claude
}

/// <summary>A parsed <c>provider:model</c> reference. A bare provider uses that provider's configured model.</summary>
public readonly record struct ModelReference(LanguageModelProvider Provider, string? Model)
{
    public static bool TryParse(string? value, out ModelReference reference)
    {
        try
        {
            reference = Parse(value);
            return true;
        }
        catch (InvalidOperationException)
        {
            reference = default;
            return false;
        }
    }

    public static ModelReference Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            return new ModelReference(LanguageModelProvider.None, null);

        var text = value.Trim();
        var colon = text.IndexOf(':');
        var providerName = colon < 0 ? text : text[..colon];
        var model = colon < 0 ? null : text[(colon + 1)..].Trim();

        if (!Enum.TryParse<LanguageModelProvider>(providerName, ignoreCase: true, out var provider)
            || provider == LanguageModelProvider.None)
        {
            throw new InvalidOperationException(
                $"'{value}' is not a model reference. Write local:<model> or claude:<model>, or none.");
        }

        return new ModelReference(provider, string.IsNullOrEmpty(model) ? null : model);
    }
}

/// <summary>
/// Dollar prices per million tokens, keyed by model ID. Used to estimate the
/// cost of each agent run. Local models are free and need no entry.
/// </summary>
public sealed class ModelPricingOptions
{
    public const string SectionName = "ModelPricing";

    public Dictionary<string, ModelPrice> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ModelPrice
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
}
