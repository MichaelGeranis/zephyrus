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
    /// <summary>Webhook secret the tests sign their deliveries with.</summary>
    public const string WebhookSecret = "test-webhook-secret";

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

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:Webhook:Secret"] = WebhookSecret,
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
