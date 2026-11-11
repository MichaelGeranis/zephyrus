using System.Net;
using System.Text;

namespace Zephyrus.UnitTests.AI;

/// <summary>Answers every request with a canned response and keeps the request body.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;

    public StubHttpHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _body = body;
        _status = status;
    }

    public HttpRequestMessage? LastRequest { get; private set; }
    public string LastRequestBody { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
    }
}
