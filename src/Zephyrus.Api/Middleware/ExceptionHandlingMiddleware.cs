using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Zephyrus.Application.Exceptions;
using Zephyrus.Core.Exceptions;

namespace Zephyrus.Api.Middleware;

/// <summary>
/// Maps exceptions to problem details responses. Only exceptions written for
/// callers (the domain's and the application's own) show their message.
/// Anything else, including InvalidOperationException and ArgumentException
/// thrown by .NET, EF Core, Octokit or a model client, is logged in full and
/// answered with a generic 500 that carries the trace ID, so the details stay
/// in the logs.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    public const string UnexpectedErrorMessage =
        "An unexpected error occurred. Quote the trace ID when reporting it.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            UnauthorizedApprovalException ex => (HttpStatusCode.Forbidden, ex.Message),
            NotPermittedException ex => (HttpStatusCode.Forbidden, ex.Message),
            ArtifactNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            ResourceNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            InvalidTransitionException ex => (HttpStatusCode.Conflict, ex.Message),
            RequestRejectedException ex => (HttpStatusCode.BadRequest, ex.Message),
            GeneratedFileRejectedException ex => (HttpStatusCode.UnprocessableEntity, ex.Message),
            _ => (HttpStatusCode.InternalServerError, UnexpectedErrorMessage)
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception");
        else
            _logger.LogWarning(exception, "Handled exception: {StatusCode}", statusCode);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            status = (int)statusCode,
            title = statusCode.ToString(),
            detail = message,
            traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
