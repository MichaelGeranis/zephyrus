using Zephyrus.Api.Authentication;

namespace Zephyrus.Api.Middleware;

/// <summary>
/// Answers 429 to a client that has presented too many wrong team tokens,
/// before its token is checked at all, so it cannot keep guessing. Requests
/// without a bearer token, such as GitHub webhooks, pass through.
/// </summary>
public sealed class FailedSignInThrottleMiddleware
{
    private readonly RequestDelegate _next;
    private readonly FailedSignInLimiter _limiter;
    private readonly ILogger<FailedSignInThrottleMiddleware> _logger;

    public FailedSignInThrottleMiddleware(
        RequestDelegate next,
        FailedSignInLimiter limiter,
        ILogger<FailedSignInThrottleMiddleware> logger)
    {
        _next = next;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var client = FailedSignInLimiter.ClientKey(context);
        if (context.Request.Headers.Authorization.Count > 0 && _limiter.IsBlocked(client, out var retryAfter))
        {
            _logger.LogWarning("Refused a request from {Client}: too many failed sign-ins", client);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                status = StatusCodes.Status429TooManyRequests,
                title = "TooManyRequests",
                detail = "Too many failed sign-ins. Try again later."
            });
            return;
        }

        await _next(context);
    }
}
