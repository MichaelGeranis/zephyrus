using Microsoft.Extensions.Options;
using Zephyrus.Infrastructure.AI;

namespace Zephyrus.UnitTests.AI;

internal static class LanguageModelTestSupport
{
    public static LanguageModelUsageTracker Tracker(params (string Model, decimal In, decimal Out)[] prices)
    {
        var options = new ModelPricingOptions();
        foreach (var (model, input, output) in prices)
            options.Models[model] = new ModelPrice { InputPerMillion = input, OutputPerMillion = output };
        return new LanguageModelUsageTracker(Options.Create(options));
    }
}
