using Zephyrus.Infrastructure.AI;

namespace Zephyrus.UnitTests.AI;

public class LanguageModelOptionsTests
{
    [Theory]
    [InlineData("local:llama3.1:8b", LanguageModelProvider.Local, "llama3.1:8b")]
    [InlineData("claude:claude-opus-5-5", LanguageModelProvider.Claude, "claude-opus-5-5")]
    [InlineData("Claude", LanguageModelProvider.Claude, null)]
    [InlineData("none", LanguageModelProvider.None, null)]
    [InlineData("", LanguageModelProvider.None, null)]
    public void Parse_WhenGivenAReference_ShouldSplitProviderAndModel(string text, LanguageModelProvider provider, string? model)
    {
        Assert.Equal(new ModelReference(provider, model), ModelReference.Parse(text));
    }

    [Fact]
    public void Parse_WhenProviderIsUnknown_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => ModelReference.Parse("openai:gpt-x"));
    }

    [Fact]
    public void For_WhenStageHasNoOverride_ShouldUseTheDefault()
    {
        var options = new LanguageModelOptions();

        Assert.Same(options.Default, options.For("Prd"));
    }

    [Fact]
    public void For_WhenStageOverridesOnlyThePrimary_ShouldInheritTheDefaultFallback()
    {
        var options = new LanguageModelOptions();
        options.Stages["Code"] = new ModelChain { Primary = "claude:claude-sonnet-5-5" };

        var chain = options.For("code");

        Assert.Equal("claude:claude-sonnet-5-5", chain.Primary);
        Assert.Equal("claude:claude-opus-5-5", chain.Fallback);
    }

    [Fact]
    public void For_WhenStageSetsFallbackToNone_ShouldTurnTheFallbackOff()
    {
        var options = new LanguageModelOptions();
        options.Stages["Code"] = new ModelChain { Primary = "claude:claude-opus-5-5", Fallback = "none" };

        Assert.Equal(LanguageModelProvider.None, ModelReference.Parse(options.For("Code").Fallback).Provider);
    }

    [Fact]
    public void Validate_WhenStageNameIsMisspelt_ShouldThrow()
    {
        var options = new LanguageModelOptions();
        options.Stages["Coding"] = new ModelChain { Primary = "local:llama3.1:8b" };

        var error = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("Coding", error.Message);
    }

    [Fact]
    public void Validate_WhenDefaultHasNoPrimary_ShouldThrow()
    {
        var options = new LanguageModelOptions { Default = new ModelChain { Primary = "none" } };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
