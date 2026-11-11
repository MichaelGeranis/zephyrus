using System.Net;
using System.Text;
using System.Text.Json;

namespace Zephyrus.UnitTests.GitHub;

/// <summary>
/// Plays the GitHub REST API in memory: each route answers with canned JSON,
/// and every request is recorded so tests can check what Octokit sent. An
/// unrouted request gets 404, as GitHub answers for a missing ref or file.
/// </summary>
internal sealed class FakeGitHub : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, object Body)> _routes = new();

    public List<RecordedRequest> Requests { get; } = new();

    /// <summary>Answers <paramref name="method"/> on <paramref name="path"/> (with query, if any).</summary>
    public FakeGitHub On(HttpMethod method, string path, object body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[$"{method} {path}"] = (status, body);
        return this;
    }

    public RecordedRequest Single(HttpMethod method) => Requests.Single(r => r.Method == method);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Requests.Add(new RecordedRequest(
            request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString(), body));

        var (status, json) = _routes.TryGetValue($"{request.Method} {request.RequestUri.PathAndQuery}", out var route)
            ? route
            : (HttpStatusCode.NotFound, (object)new { message = "Not Found" });

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(json), Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };
    }

    internal sealed record RecordedRequest(HttpMethod Method, string Path, string? Authorization, string? Body)
    {
        public JsonElement Json => JsonDocument.Parse(Body!).RootElement;
    }

    // Minimal response shapes, with the fields Octokit reads.
    public static object Reference(string name, string sha) =>
        new { @ref = name, url = "", node_id = "", @object = new { sha, type = "commit", url = "" } };

    public static object File(string path, string sha, string content) => new
    {
        type = "file",
        name = Path.GetFileName(path),
        path,
        sha,
        encoding = "base64",
        content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
        size = content.Length,
    };

    public static object Issue(int number, string title, string? body) =>
        new { number, title, body, state = "open", id = number, node_id = "", url = "", html_url = "" };

    public static object PullRequest(int number) =>
        new { number, id = number, node_id = "", state = "open", title = "", url = "", html_url = "" };

    public static object ContentChange(string path) =>
        new { content = new { path, type = "file" }, commit = new { sha = "c0ffee" } };
}
