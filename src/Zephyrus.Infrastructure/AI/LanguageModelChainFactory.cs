using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// Builds the model chain (primary plus optional fallback) for the default and
/// for each pipeline stage, from <see cref="LanguageModelOptions"/>.
/// </summary>
public sealed class LanguageModelChainFactory
{
    public const string ClaudeHttpClient = "claude";
    public const string LocalHttpClient = "local";

    private readonly IHttpClientFactory _http;
    private readonly IOptions<ClaudeLanguageModelOptions> _claude;
    private readonly IOptions<LocalModelOptions> _local;
    private readonly LanguageModelOptions _choice;
    private readonly LanguageModelUsageTracker _usage;
    private readonly LanguageModelRouting _routing;
    private readonly ILoggerFactory _loggers;

    public LanguageModelChainFactory(
        IHttpClientFactory http,
        IOptions<ClaudeLanguageModelOptions> claude,
        IOptions<LocalModelOptions> local,
        IOptions<LanguageModelOptions> choice,
        LanguageModelUsageTracker usage,
        LanguageModelRouting routing,
        ILoggerFactory loggers)
    {
        _http = http;
        _claude = claude;
        _local = local;
        _choice = choice.Value;
        _usage = usage;
        _routing = routing;
        _loggers = loggers;
    }

    public bool HasStageOverride(string stage) => _choice.HasStageOverride(stage);

    public ILanguageModel ForDefault() => Build(_choice.Default);

    public ILanguageModel ForStage(string stage) => Build(_choice.For(stage));

    /// <summary>A chain a project chose for one of its stages.</summary>
    public ILanguageModel ForChain(ModelChain chain) => Build(chain);

    private ILanguageModel Build(ModelChain chain)
    {
        var primaryRef = ModelReference.Parse(chain.Primary);
        var fallbackRef = ModelReference.Parse(chain.Fallback);

        var primary = Create(primaryRef);
        if (fallbackRef.Provider == LanguageModelProvider.None || fallbackRef == primaryRef)
            return primary;

        return new FallbackLanguageModel(
            primary, Create(fallbackRef), _routing, _loggers.CreateLogger<FallbackLanguageModel>());
    }

    private ILanguageModel Create(ModelReference reference) => reference.Provider switch
    {
        LanguageModelProvider.Local => new OpenAiCompatibleLanguageModel(
            _http.CreateClient(LocalHttpClient), _local, _usage, reference.Model),
        LanguageModelProvider.Claude => new ClaudeLanguageModel(
            _http.CreateClient(ClaudeHttpClient), _claude, _usage,
            _loggers.CreateLogger<ClaudeLanguageModel>(), reference.Model),
        _ => throw new InvalidOperationException("A model reference is required.")
    };
}
