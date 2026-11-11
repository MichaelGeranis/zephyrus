using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Infrastructure.AI;

/// <summary>
/// The model one stage's agent talks to. Agents are created before the job
/// says which project it is for, so the project's choice is looked up on each
/// call: its chain if it chose one, the configured chain otherwise.
/// </summary>
public sealed class StageLanguageModel : ILanguageModel
{
    private readonly AgentJobKind _stage;
    private readonly ILanguageModel _configured;
    private readonly ModelChoice _configuredChoice;
    private readonly IProjectModelSelection _selection;
    private readonly Func<ModelChain, ILanguageModel> _build;

    private (ModelChoice Choice, ILanguageModel Model)? _built;

    public StageLanguageModel(
        AgentJobKind stage,
        ILanguageModel configured,
        ModelChoice configuredChoice,
        IProjectModelSelection selection,
        Func<ModelChain, ILanguageModel> build)
    {
        _stage = stage;
        _configured = configured;
        _configuredChoice = configuredChoice;
        _selection = selection;
        _build = build;
    }

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => Current().GenerateAsync(systemPrompt, userMessage, ct);

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => Current().GenerateAsync(systemPrompt, userMessage, maxTokens, ct);

    public Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default)
        => Current().GenerateAsync(systemPrompt, messages, maxTokens, ct);

    private ILanguageModel Current()
    {
        var choice = _selection.For(_stage);
        if (choice is null)
            return _configured;

        if (_built is { } built && built.Choice == choice)
            return built.Model;

        // A fallback left unset keeps the configured one.
        var model = _build(new ModelChain { Primary = choice.Primary, Fallback = choice.Fallback ?? _configuredChoice.Fallback });
        _built = (choice, model);
        return model;
    }
}
