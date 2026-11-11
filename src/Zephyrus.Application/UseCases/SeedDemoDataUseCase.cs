using Zephyrus.Application.Managers;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Fills an empty demo install with a project and two features, so the first
/// screen shows the pipeline instead of a blank page. One feature waits at
/// <c>Ideation</c>; the other already has a PRD waiting for approval.
/// </summary>
/// <remarks>
/// Runs only in demo mode, and only when there are no projects yet, so it never
/// touches data someone created.
/// </remarks>
public sealed class SeedDemoDataUseCase
{
    public const string ProjectName = "Acme Billing (demo)";
    public const string RepositorySlug = "acme/billing";
    public const string FeatureWithPrd = "Let customers download their invoices as PDF";
    public const string FeatureInIdeation = "Send a weekly usage summary email to account owners";

    private const string Constitution = """
        project:
          name: acme-billing
          description: "Invoicing and payments for small businesses"

        stack:
          frontend: "Next.js + TypeScript"
          backend: "ASP.NET Core + C#"
          database: "PostgreSQL"
          orm: "Entity Framework Core"

        conventions:
          testing: "xUnit (backend), Jest (frontend)"
          branch_strategy: "feature branches -> main"
          commit_style: "conventional commits"

        deployment:
          target: "Railway"
          trigger: "merge to main"

        architecture:
          api_style: "REST + JSON"
          patterns:
            - "Repository pattern for data access"
            - "Stateless agents - all state in DB and GitHub"
        """;

    private readonly IProjectRepository _projectRepository;
    private readonly ProjectManager _projectManager;
    private readonly FeatureManager _featureManager;
    private readonly InvokePrdAgentUseCase _invokePrdAgent;

    public SeedDemoDataUseCase(
        IProjectRepository projectRepository,
        ProjectManager projectManager,
        FeatureManager featureManager,
        InvokePrdAgentUseCase invokePrdAgent)
    {
        _projectRepository = projectRepository;
        _projectManager = projectManager;
        _featureManager = featureManager;
        _invokePrdAgent = invokePrdAgent;
    }

    /// <returns><c>true</c> when demo data was created; <c>false</c> when projects already existed.</returns>
    public async Task<bool> ExecuteAsync(CancellationToken ct = default)
    {
        var existing = await _projectRepository.GetAllAsync(ct);
        if (existing.Count > 0)
            return false;

        // The demo code host ignores the token; a placeholder keeps the
        // project valid for the real code path.
        var project = await _projectManager.CreateAsync(
            ProjectName,
            "Sample project created by demo mode. Nothing here reaches GitHub or Claude.",
            Constitution,
            RepositorySlug,
            "demo-token-not-used",
            ct);

        var withPrd = await _featureManager.CreateAsync(project.Id, FeatureWithPrd, ct);
        await _featureManager.CreateAsync(project.Id, FeatureInIdeation, ct);

        await _invokePrdAgent.ExecuteAsync(withPrd.Id, ct: ct);
        return true;
    }
}
