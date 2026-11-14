using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Zephyrus.Api.Authentication;
using Zephyrus.Api.Hosting;
using Zephyrus.Api.Middleware;
using Zephyrus.Api.Webhooks;
using Zephyrus.Core.Interfaces;
using Zephyrus.Application;
using Zephyrus.Application.UseCases;
using Zephyrus.Infrastructure;
using Zephyrus.Infrastructure.Persistence;
using Zephyrus.Infrastructure.Telemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, builder.Configuration);
// Authentication — the approver identity on every approval comes from here,
// never from the request body.
builder.Services.Configure<TeamOptions>(builder.Configuration.GetSection(TeamOptions.SectionName));

// The Docker setup ships well-known demo tokens. Refuse them outside demo mode
// so a real deployment cannot start with credentials anyone can read.
var configuredTeam = builder.Configuration.GetSection(TeamOptions.SectionName).Get<TeamOptions>() ?? new TeamOptions();
if (configuredTeam.Members.FirstOrDefault(m => !m.HasValidTokenHash) is { } badHash)
    throw new InvalidOperationException(
        $"Team member '{badHash.Email}' has a TokenHash that is not 64 hexadecimal characters (the SHA-256 of the token).");
if (!builder.Configuration.GetValue<bool>("Demo:Enabled"))
{
    if (configuredTeam.Members.Any(m => m.Token.StartsWith(TeamOptions.DemoTokenPrefix, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException(
            $"Team tokens starting with '{TeamOptions.DemoTokenPrefix}' are only allowed when Demo:Enabled is true. Set real tokens.");
}
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
// Too many wrong tokens from one address and it is refused for a while.
builder.Services.Configure<FailedSignInOptions>(
    builder.Configuration.GetSection(FailedSignInOptions.SectionName));
builder.Services.AddSingleton<FailedSignInLimiter>();
builder.Services
    .AddAuthentication(TeamTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TeamTokenAuthenticationHandler>(
        TeamTokenAuthenticationHandler.SchemeName, null);
// Every endpoint requires an authenticated team member unless it opts out
// with [AllowAnonymous]. The only opt-out is the GitHub webhook, which is
// authenticated by its payload signature instead.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Inbound webhooks authenticate by payload signature, not by the scheme above.
builder.Services.Configure<GitHubWebhookOptions>(
    builder.Configuration.GetSection(GitHubWebhookOptions.SectionName));
builder.Services.AddScoped<GitHubWebhookHandler>();

// Tracing: off unless an OTLP endpoint is configured (the Docker setup points
// it at Jaeger). Spans cover incoming requests, agent jobs, language model
// calls, outgoing HTTP and database queries.
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            builder.Configuration["OTEL_SERVICE_NAME"] ?? "zephyrus-api"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(ZephyrusTelemetry.SourceName)
            .AddSource("Npgsql")
            .AddOtlpExporter());
}

builder.Services.AddControllers();
builder.Services.AddZephyrusHealthChecks();
var behindReverseProxy = ReverseProxyOptions.Configure(builder.Services, builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Zephyrus API", Version = "v1" });
});
// Browser origins allowed to call the API. Set Cors:AllowedOrigins to the
// deployed frontend's origin; the default only covers the local dev server.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null || allowedOrigins.Length == 0)
    allowedOrigins = new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Docker sets this so a fresh database is ready without running `dotnet ef`.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    await DatabaseInitializer.MigrateAsync(app.Services);

// Demo mode swaps Claude and GitHub for local stand-ins (see Infrastructure/Demo)
// and fills an empty install with sample data.
if (app.Configuration.GetValue<bool>("Demo:Enabled"))
{
    app.Logger.LogWarning(
        "Demo mode is on: no calls go to Claude or GitHub, and agent output is canned. Do not use it on a shared deployment.");

    await using var scope = app.Services.CreateAsyncScope();
    if (await scope.ServiceProvider.GetRequiredService<SeedDemoDataUseCase>().ExecuteAsync())
        app.Logger.LogInformation("Demo data created.");
}

// A token written in the configuration signs in as that member for anyone who can
// read it; a hash does not. Say so once, by member, never by token.
if (!app.Configuration.GetValue<bool>("Demo:Enabled"))
{
    foreach (var member in configuredTeam.Members.Where(m => !m.UsesTokenHash && !string.IsNullOrEmpty(m.Token)))
        app.Logger.LogWarning(
            "Team member {Member} is configured with a plaintext Token. Configure TokenHash (the SHA-256 of the token) instead; see docs/production.md.",
            member.Email);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zephyrus API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
}

// First, so everything after sees the client's address and scheme, not the proxy's.
if (behindReverseProxy)
    app.UseForwardedHeaders();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseMiddleware<FailedSignInThrottleMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapZephyrusHealthChecks();

app.Run();

// Make the implicit Program class accessible to integration tests
public partial class Program { }
