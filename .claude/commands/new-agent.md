# /new-agent

Scaffold a new Zephyrus agent end-to-end.

Agent name: $ARGUMENTS

## Instructions

Create all files for a new agent following the Zephyrus agent contract exactly.
Read ARCHITECTURE.md before proceeding. Do not deviate from the patterns below.

---

## Files to Create

### 1. Input record — Zephyrus.Core/Agents/{Name}AgentInput.cs
```csharp
namespace Zephyrus.Core.Agents;

public record {Name}AgentInput(
    // Add properties relevant to this agent
    // Always include:
    string ConstitutionYaml,
    string FeaturePrompt
    // Add agent-specific inputs below
);
```

### 2. Output record — Zephyrus.Core/Agents/{Name}AgentOutput.cs
```csharp
namespace Zephyrus.Core.Agents;

public record {Name}AgentOutput(
    // Add properties representing the artifact produced
    // Always include:
    bool Success,
    string? ErrorMessage
    // Add agent-specific outputs below
);
```

### 3. Agent implementation — Zephyrus.Infrastructure/AI/Agents/{Name}Agent.cs
```csharp
namespace Zephyrus.Infrastructure.AI.Agents;

public sealed class {Name}Agent : IAgent<{Name}AgentInput, {Name}AgentOutput>
{
    private readonly ILanguageModel _llm;
    private readonly ICodeHost _codeHost;
    private readonly IPromptLoader _promptLoader;

    public {Name}Agent(ILanguageModel llm, ICodeHost codeHost, IPromptLoader promptLoader)
    {
        _llm = llm;
        _codeHost = codeHost;
        _promptLoader = promptLoader;
    }

    public async Task<{Name}AgentOutput> RunAsync({Name}AgentInput input, CancellationToken ct = default)
    {
        var systemPrompt = await _promptLoader.LoadAsync("{agentname}", ct);

        var userMessage = BuildUserMessage(input);

        var response = await _llm.CompleteAsync(systemPrompt, userMessage, ct);

        var artifact = ParseOutput(response);

        await _codeHost.CommitFileAsync(
            path: $"docs/{agentname}-{{featureSlug}}.md",
            content: artifact,
            commitMessage: $"[Zephyrus] Add {agentname} artifact",
            ct: ct
        );

        return new {Name}AgentOutput(Success: true, ErrorMessage: null /*, parsed fields */);
    }

    private static string BuildUserMessage({Name}AgentInput input) =>
        $"""
        Constitution:
        {input.ConstitutionYaml}

        Feature Prompt:
        {input.FeaturePrompt}
        """;

    private static string ParseOutput(string response)
    {
        // Parse Claude response into artifact
        return response;
    }
}
```

### 4. System prompt — /prompts/{agentname}.md
```markdown
# {Name} Agent System Prompt

You are a [role description].

## Your Task
[Describe what the agent must produce]

## Output Format
[Describe the exact output structure]

## Rules
- Always read the Project Constitution before acting
- Stay within the scope defined in the Feature Prompt
- Do not over-engineer or add features not requested
- Output must be valid Markdown / JSON (specify which)

## Input Variables
- {constitution}: The project configuration
- {feature_prompt}: The original idea or feature request
```

### 5. Unit test — Zephyrus.Tests/Infrastructure/AI/{Name}AgentTests.cs
```csharp
namespace Zephyrus.Tests.Infrastructure.AI;

public class {Name}AgentTests
{
    private readonly Mock<ILanguageModel> _llm = new();
    private readonly Mock<ICodeHost> _codeHost = new();
    private readonly Mock<IPromptLoader> _promptLoader = new();

    [Fact]
    public async Task RunAsync_WhenValidInput_ShouldReturnSuccessOutput()
    {
        // Arrange
        _promptLoader.Setup(x => x.LoadAsync("{agentname}", default))
            .ReturnsAsync("system prompt");

        _llm.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync("mock agent response");

        var agent = new {Name}Agent(_llm.Object, _codeHost.Object, _promptLoader.Object);
        var input = new {Name}AgentInput(ConstitutionYaml: "name: test", FeaturePrompt: "test feature");

        // Act
        var result = await agent.RunAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        _codeHost.Verify(x => x.CommitFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenLlmFails_ShouldReturnFailureOutput()
    {
        // Arrange
        _promptLoader.Setup(x => x.LoadAsync("{agentname}", default))
            .ReturnsAsync("system prompt");

        _llm.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        var agent = new {Name}Agent(_llm.Object, _codeHost.Object, _promptLoader.Object);
        var input = new {Name}AgentInput(ConstitutionYaml: "name: test", FeaturePrompt: "test feature");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => agent.RunAsync(input));
    }
}
```

---

## DI Registration

Add to `Zephyrus.Api/DependencyInjection.cs`:
```csharp
services.AddScoped<IAgent<{Name}AgentInput, {Name}AgentOutput>, {Name}Agent>();
```

---

## Checklist Before Finishing

- [ ] Input and Output records are in Zephyrus.Core (not Infrastructure)
- [ ] Agent implementation is in Zephyrus.Infrastructure
- [ ] System prompt file exists at /prompts/{agentname}.md
- [ ] Unit test covers success and failure paths
- [ ] Agent registered in DI container
- [ ] No Octokit or HttpClient used directly — only ICodeHost and ILanguageModel
