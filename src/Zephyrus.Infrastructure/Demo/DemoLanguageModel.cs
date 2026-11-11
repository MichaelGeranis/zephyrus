using System.Text.Json;
using System.Text.RegularExpressions;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI;
using Zephyrus.Infrastructure.Telemetry;

namespace Zephyrus.Infrastructure.Demo;

/// <summary>
/// Stands in for the Claude API in demo mode, so the whole pipeline can be
/// tried without an API key. Each agent gets a canned response in the format
/// its parser expects, titled after the feature being worked on.
/// </summary>
/// <remarks>
/// The agent is recognised from its system prompt, so this follows the prompts
/// in <c>/prompts</c>. The output is illustrative, not generated.
/// </remarks>
public sealed partial class DemoLanguageModel : ILanguageModel
{
    private const int MaxTitleLength = 70;

    /// <summary>Recorded as the model on demo agent runs.</summary>
    public const string ModelName = "demo";

    private readonly LanguageModelUsageTracker? _usage;

    public DemoLanguageModel(LanguageModelUsageTracker? usage = null)
    {
        _usage = usage;
    }

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => Task.FromResult(Respond(systemPrompt, userMessage));

    public Task<string> GenerateAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
        => Task.FromResult(Respond(systemPrompt, userMessage));

    public Task<string> GenerateAsync(
        string systemPrompt, IReadOnlyList<ConversationMessage> messages, int maxTokens, CancellationToken ct = default)
        => Task.FromResult(Respond(systemPrompt, string.Join("\n\n", messages.Select(m => m.Content))));

    private string Respond(string systemPrompt, string userMessage)
    {
        // A span like a real model call's, so the demo's traces show the whole chain.
        using var activity = ZephyrusTelemetry.StartModelCall("demo", ModelName);
        var answer = Answer(systemPrompt, userMessage);

        // Rough token counts (about four characters per token) so the usage
        // columns show something plausible; demo runs cost nothing.
        var inputTokens = (systemPrompt.Length + userMessage.Length) / 4;
        _usage?.Record(ModelName, inputTokens, answer.Length / 4, free: true);
        ZephyrusTelemetry.RecordModelResponse(activity, ModelName, inputTokens, answer.Length / 4);
        return answer;
    }

    private static string Answer(string systemPrompt, string userMessage)
    {
        var title = FeatureTitle(userMessage);

        if (systemPrompt.Contains("You are the PRD Agent"))
            return Prd(title);
        if (systemPrompt.Contains("You are the Architect Agent"))
            return Adr(title);
        if (systemPrompt.Contains("You are the Task Agent"))
            return Tasks(title);
        if (systemPrompt.Contains("You are the Code Agent"))
            return Code(title);
        if (systemPrompt.Contains("You are the QA Agent"))
            return Qa(title);
        if (systemPrompt.Contains("You are the DevOps Agent"))
            return DevOps();

        return $"# {title}\n\nDemo output.";
    }

    /// <summary>
    /// The PRD agent receives the feature prompt; later agents receive the PRD,
    /// whose heading carries the title forward.
    /// </summary>
    internal static string FeatureTitle(string userMessage)
    {
        var prompt = FeaturePromptPattern().Match(userMessage);
        if (prompt.Success)
            return Shorten(prompt.Groups[1].Value);

        var prdHeading = PrdHeadingPattern().Match(userMessage);
        if (prdHeading.Success)
            return Shorten(prdHeading.Groups[1].Value);

        return "Demo feature";
    }

    private static string Shorten(string text)
    {
        var line = text.Trim().TrimEnd('.');
        if (line.Length == 0)
            return "Demo feature";

        line = char.ToUpperInvariant(line[0]) + line[1..];
        return line.Length <= MaxTitleLength ? line : line[..MaxTitleLength].TrimEnd() + "…";
    }

    private static string Prd(string title) => $"""
        # PRD: {title}

        > Demo mode: this document was produced by the built-in demo model, not by Claude.

        ## Problem Statement
        Teams asked for "{title}". Today the workflow is manual, slow and leaves no record of
        who decided what.

        ## Target Users
        - Product managers who define the scope
        - Engineers who build and maintain it

        ## Goals
        - Deliver the core capability behind "{title}"
        - Keep every change reviewable and auditable

        ## Non-Goals
        - Mobile-specific layouts
        - Migrating historical data

        ## User Stories

        ### Story 1: Core flow
        - **Given** a signed-in team member
        - **When** they use the new capability
        - **Then** the result is saved and visible to the rest of the team

        ### Story 2: Audit
        - **Given** a change made through the new capability
        - **When** a reviewer opens its history
        - **Then** they see who made it and when

        ## Acceptance Criteria
        - [ ] The core flow works end to end
        - [ ] Every change is recorded with its author and time
        - [ ] Errors are shown with a clear next step

        ## Open Questions
        - Which roles may use the capability on day one?
        """;

    private static string Adr(string title) => $$"""
        # ADR: {{title}}

        > Demo mode: this document was produced by the built-in demo model, not by Claude.

        ## Summary
        Implement "{{title}}" as a new use case in the Application layer, exposed through one
        REST endpoint and one page in the web UI.

        ## New Components
        | Component | Layer | Purpose |
        |-----------|-------|---------|
        | `ChangeRecord` entity | Core | Stores what changed, who changed it and when |
        | `RecordChangeUseCase` | Application | Validates input and records the change |
        | `ChangesController` | Api | Maps HTTP to the use case |
        | Changes page | Web | Lists and creates changes |

        ## Database Changes
        - New table `change_records` (id, feature_id, author, created_at, payload)

        ## API Contracts
        ### `POST /api/changes`
        - **Request:** `{ "featureId": "uuid", "payload": "string" }`
        - **Response:** `{ "id": "uuid", "createdAt": "timestamp" }`
        - **Status codes:** 201, 400, 401

        ## External Dependencies
        - None

        ## Sequence Diagram
        1. The user submits the form in the web UI.
        2. The API authenticates the caller and calls `RecordChangeUseCase`.
        3. The use case validates the input and stores the record.
        4. The API returns the new record.

        ## Estimated Complexity
        | Area | Estimate | Notes |
        |------|----------|-------|
        | Backend | M | New entity, use case and endpoint |
        | Frontend | S | One page |
        | Database | S | One table |

        ## Risks & Open Questions
        - Payload size limits need confirming with the PM/EM.
        """;

    private static string Tasks(string title) => JsonSerializer.Serialize(new
    {
        tasks = new object[]
        {
            new
            {
                title = $"Add change_records table for {title}",
                body = "## Description\nCreate the EF Core migration for `change_records`.\n\n## Acceptance Criteria\n- [ ] Migration creates the table\n- [ ] Migration is reversible",
                agent_type = "DB",
            },
            new
            {
                title = $"Implement RecordChangeUseCase for {title}",
                body = "## Description\nAdd the use case and endpoint described in the ADR.\n\n## Acceptance Criteria\n- [ ] Input is validated\n- [ ] The author is taken from the signed-in user",
                agent_type = "BE",
            },
            new
            {
                title = $"Add the changes page for {title}",
                body = "## Description\nCreate the page that lists and creates changes.\n\n## Acceptance Criteria\n- [ ] The page lists existing changes\n- [ ] The form shows validation errors",
                agent_type = "FE",
            },
        },
    });

    private static string Code(string title) => JsonSerializer.Serialize(new
    {
        files = new[]
        {
            new
            {
                path = "src/Changes/RecordChangeUseCase.cs",
                content = $$"""
                    namespace Changes;

                    // Demo mode: generated by the built-in demo model for "{{title}}".
                    public sealed class RecordChangeUseCase
                    {
                        public ChangeRecord Execute(Guid featureId, string author, string payload)
                        {
                            if (string.IsNullOrWhiteSpace(payload))
                                throw new ArgumentException("Payload is required.", nameof(payload));

                            return new ChangeRecord(Guid.NewGuid(), featureId, author, DateTime.UtcNow, payload);
                        }
                    }

                    public sealed record ChangeRecord(Guid Id, Guid FeatureId, string Author, DateTime CreatedAt, string Payload);
                    """,
            },
        },
    });

    private static string Qa(string title) => JsonSerializer.Serialize(new
    {
        test_files = new[]
        {
            new
            {
                path = "tests/Changes/RecordChangeUseCaseTests.cs",
                content = """
                    using Changes;
                    using Xunit;

                    public class RecordChangeUseCaseTests
                    {
                        [Fact]
                        public void Execute_WhenPayloadIsEmpty_ShouldThrow()
                        {
                            var useCase = new RecordChangeUseCase();
                            Assert.Throws<ArgumentException>(() => useCase.Execute(Guid.NewGuid(), "pm@example.com", ""));
                        }

                        [Fact]
                        public void Execute_WhenPayloadIsValid_ShouldRecordAuthor()
                        {
                            var record = new RecordChangeUseCase().Execute(Guid.NewGuid(), "pm@example.com", "data");
                            Assert.Equal("pm@example.com", record.Author);
                        }
                    }
                    """,
            },
        },
        report = $"# QA Report: {title}\n\n> Demo mode: produced by the built-in demo model.\n\n## Summary\nBoth tests pass.\n\n## Results\n| Test | Result |\n|------|--------|\n| Execute_WhenPayloadIsEmpty_ShouldThrow | Passed |\n| Execute_WhenPayloadIsValid_ShouldRecordAuthor | Passed |\n\n## Coverage\n92% of `RecordChangeUseCase`.",
    });

    private static string DevOps() => JsonSerializer.Serialize(new
    {
        workflow_yaml = "name: Deploy\n\non:\n  push:\n    branches: [main]\n\njobs:\n  build:\n    runs-on: ubuntu-latest\n    steps:\n      - uses: actions/checkout@v4\n      - uses: actions/setup-dotnet@v4\n        with:\n          dotnet-version: '8.0.x'\n      - run: dotnet test\n",
    });

    [GeneratedRegex(@"## Feature Prompt\s*\n\s*(.+)")]
    private static partial Regex FeaturePromptPattern();

    [GeneratedRegex(@"^# PRD: (.+)$", RegexOptions.Multiline)]
    private static partial Regex PrdHeadingPattern();
}
