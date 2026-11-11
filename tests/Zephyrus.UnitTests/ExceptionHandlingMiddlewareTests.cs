using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Zephyrus.Api.Middleware;
using Zephyrus.Application.Exceptions;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;

namespace Zephyrus.UnitTests;

public class ExceptionHandlingMiddlewareTests
{
    private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<ExceptionHandlingMiddleware>.Instance);

    private static HttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int StatusCode, JsonElement Body)> InvokeAsync(
        ExceptionHandlingMiddleware middleware, HttpContext context)
    {
        await middleware.InvokeAsync(context);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, body.RootElement);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_ShouldPassThrough()
    {
        var wasCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.True(wasCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenArtifactNotFoundException_ShouldReturn404()
    {
        var middleware = CreateMiddleware(_ =>
            throw new ArtifactNotFoundException(Guid.NewGuid()));
        var context = CreateContext();

        var (statusCode, _) = await InvokeAsync(middleware, context);

        Assert.Equal((int)HttpStatusCode.NotFound, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenArtifactNotFoundException_ShouldReturnProblemJson()
    {
        var artifactId = Guid.NewGuid();
        var middleware = CreateMiddleware(_ =>
            throw new ArtifactNotFoundException(artifactId));
        var context = CreateContext();

        var (_, body) = await InvokeAsync(middleware, context);

        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Contains(artifactId.ToString(), body.GetProperty("detail").GetString() ?? "");
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidTransitionException_ShouldReturn409()
    {
        var middleware = CreateMiddleware(_ =>
            throw new InvalidTransitionException(FeatureStatus.Ideation, FeatureStatus.Deployed));
        var context = CreateContext();

        var (statusCode, _) = await InvokeAsync(middleware, context);

        Assert.Equal((int)HttpStatusCode.Conflict, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidTransitionException_ShouldReturnProblemJson()
    {
        var middleware = CreateMiddleware(_ =>
            throw new InvalidTransitionException(FeatureStatus.Ideation, FeatureStatus.Deployed));
        var context = CreateContext();

        var (_, body) = await InvokeAsync(middleware, context);

        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Contains("Ideation", body.GetProperty("detail").GetString() ?? "");
    }

    [Fact]
    public async Task InvokeAsync_WhenResourceNotFoundException_ShouldReturn404WithItsMessage()
    {
        var middleware = CreateMiddleware(_ =>
            throw new ResourceNotFoundException("Feature 'abc' not found."));
        var context = CreateContext();

        var (statusCode, body) = await InvokeAsync(middleware, context);

        Assert.Equal((int)HttpStatusCode.NotFound, statusCode);
        Assert.Equal("Feature 'abc' not found.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestRejectedException_ShouldReturn400WithItsMessage()
    {
        var middleware = CreateMiddleware(_ =>
            throw new RequestRejectedException("Feature must be in Ideation status."));
        var context = CreateContext();

        var (statusCode, body) = await InvokeAsync(middleware, context);

        Assert.Equal((int)HttpStatusCode.BadRequest, statusCode);
        Assert.Equal("Feature must be in Ideation status.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenGeneratedFileRejectedException_ShouldReturn422NamingThePaths()
    {
        var middleware = CreateMiddleware(_ =>
            throw new GeneratedFileRejectedException("Code", new[] { "'.github/workflows/x.yml' ('.github/' is protected)" }));
        var context = CreateContext();

        var (statusCode, body) = await InvokeAsync(middleware, context);

        Assert.Equal((int)HttpStatusCode.UnprocessableEntity, statusCode);
        Assert.Contains(".github/workflows/x.yml", body.GetProperty("detail").GetString());
    }

    // .NET, EF Core, Octokit and the model clients throw these with internal
    // details (SQL, URLs, response bodies); they must not reach the caller.
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(HttpRequestException))]
    public async Task InvokeAsync_WhenFrameworkException_ShouldReturnGeneric500(Type exceptionType)
    {
        var middleware = CreateMiddleware(_ =>
            throw (Exception)Activator.CreateInstance(exceptionType, "Npgsql: relation \"secret_table\" not found")!);
        var context = CreateContext();

        var (statusCode, body) = await InvokeAsync(middleware, context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, statusCode);
        Assert.Equal(ExceptionHandlingMiddleware.UnexpectedErrorMessage, body.GetProperty("detail").GetString());
        Assert.DoesNotContain("secret_table", body.GetRawText());
    }

    [Fact]
    public async Task InvokeAsync_WhenUnexpectedException_ShouldReturnTraceIdToQuote()
    {
        var middleware = CreateMiddleware(_ => throw new Exception("boom"));
        var context = CreateContext();
        context.TraceIdentifier = "trace-123";

        var (_, body) = await InvokeAsync(middleware, context);

        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_ShouldReturn500()
    {
        var middleware = CreateMiddleware(_ =>
            throw new Exception("Something unexpected happened."));
        var context = CreateContext();

        var (statusCode, _) = await InvokeAsync(middleware, context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, statusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_ShouldReturnGenericMessage()
    {
        var middleware = CreateMiddleware(_ =>
            throw new Exception("Internal details that should not leak."));
        var context = CreateContext();

        var (_, body) = await InvokeAsync(middleware, context);

        var detail = body.GetProperty("detail").GetString() ?? "";
        Assert.DoesNotContain("Internal details", detail);
        Assert.Contains("unexpected", detail);
    }

    [Fact]
    public async Task InvokeAsync_WhenException_ShouldSetContentTypeToApplicationProblemJson()
    {
        var middleware = CreateMiddleware(_ =>
            throw new ArtifactNotFoundException(Guid.NewGuid()));
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Equal("application/problem+json", context.Response.ContentType);
    }
}
