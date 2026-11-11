using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.Infrastructure.AI;

namespace Zephyrus.UnitTests.AI;

public class StageLanguageModelTests
{
    private readonly NamedModel _configured = new("configured");
    private readonly ProjectModelSelection _selection = new();
    private readonly List<ModelChain> _built = new();

    private StageLanguageModel CreateSut() => new(
        AgentJobKind.Code,
        _configured,
        new ModelChoice("local:llama3.1:8b", "claude:claude-opus-5-5"),
        _selection,
        chain =>
        {
            _built.Add(chain);
            return new NamedModel($"{chain.Primary} -> {chain.Fallback}");
        });

    [Fact]
    public async Task GenerateAsync_WhenProjectChoseNothingForTheStage_ShouldUseTheConfiguredModel()
    {
        _selection.Select(new Dictionary<AgentJobKind, ModelChoice> { [AgentJobKind.Prd] = new("local:other", null) });

        Assert.Equal("configured", await CreateSut().GenerateAsync("s", "u", Ct));
        Assert.Empty(_built);
    }

    [Fact]
    public async Task GenerateAsync_WhenProjectChoseModels_ShouldUseThem()
    {
        _selection.Select(new Dictionary<AgentJobKind, ModelChoice> { [AgentJobKind.Code] = new("claude:claude-opus-5-5", "none") });

        Assert.Equal("claude:claude-opus-5-5 -> none", await CreateSut().GenerateAsync("s", "u", Ct));
    }

    [Fact]
    public async Task GenerateAsync_WhenProjectLeftTheFallbackUnset_ShouldKeepTheConfiguredFallback()
    {
        _selection.Select(new Dictionary<AgentJobKind, ModelChoice> { [AgentJobKind.Code] = new("local:qwen3:32b", null) });

        Assert.Equal("local:qwen3:32b -> claude:claude-opus-5-5", await CreateSut().GenerateAsync("s", "u", Ct));
    }

    [Fact]
    public async Task GenerateAsync_WhenCalledAgain_ShouldReuseTheBuiltChain()
    {
        _selection.Select(new Dictionary<AgentJobKind, ModelChoice> { [AgentJobKind.Code] = new("local:qwen3:32b", null) });
        var sut = CreateSut();

        await sut.GenerateAsync("s", "u", Ct);
        await sut.GenerateAsync("s", "u", 100, Ct);

        Assert.Single(_built);
    }

    private sealed class NamedModel(string name) : ILanguageModel
    {
        public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default) => Task.FromResult(name);
        public Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default) => Task.FromResult(name);
        public Task<string> GenerateAsync(string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default) => Task.FromResult(name);
    }
}

public class ModelCatalogTests
{
    private static ModelCatalog Create(bool demo = false)
    {
        var options = new LanguageModelOptions();
        options.Stages["Code"] = new ModelChain { Primary = "claude:claude-opus-5-5", Fallback = "none" };
        var pricing = new ModelPricingOptions();
        pricing.Models["claude-sonnet-5-5"] = new ModelPrice { InputPerMillion = 3, OutputPerMillion = 15 };
        return new ModelCatalog(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(pricing),
            Microsoft.Extensions.Options.Options.Create(new Zephyrus.Infrastructure.Demo.DemoOptions { Enabled = demo }));
    }

    [Fact]
    public void KnownModels_ShouldListConfiguredAndPricedModelsOnce()
    {
        Assert.Equal(
            new[] { "claude:claude-opus-5-5", "claude:claude-sonnet-5-5", "local:llama3.1:8b" },
            Create().KnownModels);
    }

    [Fact]
    public void DefaultFor_ShouldUseTheStageOverrideOrTheDefault()
    {
        var catalog = Create();

        Assert.Equal(new ModelChoice("claude:claude-opus-5-5", "none"), catalog.DefaultFor(AgentJobKind.Code));
        Assert.Equal(new ModelChoice("local:llama3.1:8b", "claude:claude-opus-5-5"), catalog.DefaultFor(AgentJobKind.Prd));
    }

    [Fact]
    public void DefaultFor_WhenDemoModeIsOn_ShouldSayTheDemoModelAnswers()
    {
        Assert.Equal("demo", Create(demo: true).DefaultFor(AgentJobKind.Prd).Primary);
    }

    [Theory]
    [InlineData("local:qwen3:32b", false, true)]
    [InlineData("claude:claude-opus-5-5", false, true)]
    [InlineData("claude", false, true)]
    [InlineData("none", true, true)]
    [InlineData("none", false, false)]
    [InlineData("", false, false)]
    [InlineData("gpt:4", false, false)]
    public void Problem_ShouldAcceptOnlyUsableReferences(string reference, bool allowNone, bool valid)
    {
        Assert.Equal(valid, Create().Problem(reference, allowNone) is null);
    }

    [Fact]
    public void Problem_WhenReferenceIsTooLong_ShouldReject()
    {
        Assert.NotNull(Create().Problem("local:" + new string('a', 300), allowNone: false));
    }
}
