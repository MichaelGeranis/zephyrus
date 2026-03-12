using System.Net;
using System.Text.Json;
using Zephyrus.Core.Exceptions;

namespace Zephyrus.Api.Middleware;

/// <summary>
/// Global exception handling middleware that maps domain and application
/// exceptions to appropriate HTTP status codes and problem details responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
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
            ArtifactNotFoundException ex => (HttpStatusCode.NotFound, ex.Message),
            InvalidTransitionException ex => (HttpStatusCode.Conflict, ex.Message),
            InvalidOperationException ex when ex.Message.Contains("not found") =>
                (HttpStatusCode.NotFound, ex.Message),
            InvalidOperationException ex => (HttpStatusCode.BadRequest, ex.Message),
            ArgumentException ex => (HttpStatusCode.BadRequest, ex.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
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
            detail = message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
