using System.Diagnostics;

namespace Zephyrus.Infrastructure.Telemetry;

/// <summary>
/// The trace source for spans Zephyrus creates itself: agent jobs and
/// language model calls. HTTP, outgoing HTTP and database spans come from the
/// framework instrumentation registered in <c>Program.cs</c>.
/// </summary>
public static class ZephyrusTelemetry
{
    public const string SourceName = "Zephyrus";

    public static readonly ActivitySource Source = new(SourceName);

    /// <summary>
    /// Starts a span for one language model call, tagged with the OpenTelemetry
    /// generative-AI semantic conventions so tracing backends recognise it.
    /// </summary>
    public static Activity? StartModelCall(string system, string model)
    {
        var activity = Source.StartActivity($"chat {model}", ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "chat");
        activity?.SetTag("gen_ai.system", system);
        activity?.SetTag("gen_ai.request.model", model);
        return activity;
    }

    public static void RecordModelResponse(Activity? activity, string? model, int inputTokens, int outputTokens)
    {
        if (activity is null)
            return;

        activity.SetTag("gen_ai.response.model", model);
        activity.SetTag("gen_ai.usage.input_tokens", inputTokens);
        activity.SetTag("gen_ai.usage.output_tokens", outputTokens);
    }

    public static void RecordError(Activity? activity, Exception error)
    {
        activity?.SetStatus(ActivityStatusCode.Error, error.Message);
        activity?.SetTag("error.type", error.GetType().FullName);
    }
}
