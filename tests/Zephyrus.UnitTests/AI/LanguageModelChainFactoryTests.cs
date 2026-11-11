using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zephyrus.Infrastructure.AI;

namespace Zephyrus.UnitTests.AI;

public class LanguageModelChainFactoryTests
{
    private static LanguageModelChainFactory Factory(LanguageModelOptions choice)
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var http = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();

        return new LanguageModelChainFactory(
            http,
            Options.Create(new ClaudeLanguageModelOptions()),
            Options.Create(new LocalModelOptions()),
            Options.Create(choice),
            LanguageModelTestSupport.Tracker(),
            new LanguageModelRouting(),
            NullLoggerFactory.Instance);
    }

    [Fact]
    public void ForDefault_WhenFallbackIsConfigured_ShouldBuildAChain()
    {
        Assert.IsType<FallbackLanguageModel>(Factory(new LanguageModelOptions()).ForDefault());
    }

    [Fact]
    public void ForStage_WhenStageTurnsTheFallbackOff_ShouldReturnTheSingleModel()
    {
        var choice = new LanguageModelOptions();
        choice.Stages["Code"] = new ModelChain { Primary = "claude:claude-opus-5-5", Fallback = "none" };
        var factory = Factory(choice);

        Assert.True(factory.HasStageOverride("Code"));
        Assert.IsType<ClaudeLanguageModel>(factory.ForStage("Code"));
        Assert.False(factory.HasStageOverride("Prd"));
    }

    [Fact]
    public void ForStage_WhenFallbackEqualsPrimary_ShouldNotChainAModelToItself()
    {
        var choice = new LanguageModelOptions();
        choice.Stages["Prd"] = new ModelChain { Primary = "claude:claude-opus-5-5" };

        Assert.IsType<ClaudeLanguageModel>(Factory(choice).ForStage("Prd"));
    }
}
