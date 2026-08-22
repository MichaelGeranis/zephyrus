using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.Jobs;
using Zephyrus.Infrastructure.Persistence;
using Zephyrus.IntegrationTests.Fakes;

namespace Zephyrus.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory that replaces PostgreSQL with SQLite
/// and external services (GitHub, Claude) with in-memory fakes.
/// </summary>
public sealed class ZephyrusApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Token for a member holding every role — the default client uses it.</summary>
    public const string AllRolesToken = "test-all-roles";

    /// <summary>Token for a member holding only PM/EM.</summary>
    public const string PmOnlyToken = "test-pm";

    /// <summary>Token for a member holding only QA.</summary>
    public const string QaOnlyToken = "test-qa";

    private readonly SqliteConnection _connection;

    public FakeCodeHost FakeCodeHost { get; } = new();
    public FakeLanguageModel FakeLanguageModel { get; } = new();

    public ZephyrusApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // A known team roster so the real authentication handler runs in tests.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Members:0:Email"] = "pm@test.com",
                ["Team:Members:0:DisplayName"] = "Test Approver",
                ["Team:Members:0:Token"] = AllRolesToken,
                ["Team:Members:0:Roles:0"] = "PmEm",
                ["Team:Members:0:Roles:1"] = "TechLead",
                ["Team:Members:0:Roles:2"] = "Qa",

                ["Team:Members:1:Email"] = "pm-only@test.com",
                ["Team:Members:1:DisplayName"] = "PM Only",
                ["Team:Members:1:Token"] = PmOnlyToken,
                ["Team:Members:1:Roles:0"] = "PmEm",

                ["Team:Members:2:Email"] = "qa-only@test.com",
                ["Team:Members:2:DisplayName"] = "QA Only",
                ["Team:Members:2:Token"] = QaOnlyToken,
                ["Team:Members:2:Roles:0"] = "Qa",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ZephyrusDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            // Remove the real ICodeHostFactory, ILanguageModel, and IPromptLoader
            RemoveService<ICodeHostFactory>(services);
            RemoveService<ILanguageModel>(services);
            RemoveService<IPromptLoader>(services);

            // Remove the background job queue and its worker — tests run agent
            // jobs inline so the cascade completes before the assertion.
            RemoveService<IJobQueue>(services);
            RemoveImplementation<AgentJobWorker>(services);
            services.AddScoped<IJobQueue, InlineJobQueue>();

            // Add SQLite in-memory database
            services.AddDbContext<ZephyrusDbContext>(options =>
                options.UseSqlite(_connection));

            // Add fakes
            services.AddSingleton<ICodeHostFactory>(new FakeCodeHostFactory(FakeCodeHost));
            services.AddSingleton<ILanguageModel>(FakeLanguageModel);
            services.AddSingleton<IPromptLoader>(new FakePromptLoader());

            // Ensure schema is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ZephyrusDbContext>();
            db.Database.EnsureCreated();
        });
    }

    /// <summary>
    /// Every client is authenticated as the all-roles member by default, so
    /// tests exercising the pipeline are not also testing authorisation.
    /// </summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AllRolesToken);
    }

    /// <summary>
    /// Client authenticated with <paramref name="token"/>, or anonymous when null.
    /// </summary>
    public HttpClient CreateClientWithToken(string? token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors)
            services.Remove(d);
    }

    private static void RemoveImplementation<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ImplementationType == typeof(T)).ToList();
        foreach (var d in descriptors)
            services.Remove(d);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
