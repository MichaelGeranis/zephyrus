namespace Zephyrus.Api.Middleware;

/// <summary>
/// Adds defensive response headers. The API only serves JSON, so it never
/// needs to be framed, sniffed as another content type, or leak a referrer.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cache-Control"] = "no-store";

        return _next(context);
    }
}
