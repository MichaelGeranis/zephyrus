using Microsoft.AspNetCore.Authentication;
using Zephyrus.Api.Authentication;
using Zephyrus.Api.Middleware;
using Zephyrus.Api.Webhooks;
using Zephyrus.Core.Interfaces;
using Zephyrus.Application;
using Zephyrus.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, builder.Configuration);
// Authentication — the approver identity on every approval comes from here,
// never from the request body.
builder.Services.Configure<TeamOptions>(builder.Configuration.GetSection(TeamOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services
    .AddAuthentication(TeamTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TeamTokenAuthenticationHandler>(
        TeamTokenAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

// Inbound webhooks authenticate by payload signature, not by the scheme above.
builder.Services.Configure<GitHubWebhookOptions>(
    builder.Configuration.GetSection(GitHubWebhookOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Zephyrus API", Version = "v1" });
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3004")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zephyrus API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make the implicit Program class accessible to integration tests
public partial class Program { }
