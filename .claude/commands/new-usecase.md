# /new-usecase

Scaffold a new use case in Zephyrus.Application.

Use case name: $ARGUMENTS
(Format: {Verb}{Noun} — e.g. ApproveArtifact, InvokePrdAgent, AdvancePipeline)

## Instructions

Create all files for a new use case. Read ARCHITECTURE.md first.
Use cases live in Zephyrus.Application. They orchestrate Core interfaces only.
Never reference Infrastructure types directly.

---

## Files to Create

### 1. Use case — Zephyrus.Application/UseCases/{Name}UseCase.cs
```csharp
namespace Zephyrus.Application.UseCases;

public sealed class {Name}UseCase
{
    // Inject Core interfaces only — never Infrastructure types
    private readonly IFeatureRepository _features;
    // Add other Core interface dependencies as needed

    public {Name}UseCase(IFeatureRepository features /*, other Core interfaces */)
    {
        _features = features;
    }

    /// <summary>
    /// [Describe what this use case does in one sentence]
    /// </summary>
    public async Task<{Name}Response> ExecuteAsync({Name}Request request, CancellationToken ct = default)
    {
        // 1. Load domain entities
        // 2. Apply domain rules / validate
        // 3. Call Core interfaces (repository, agent runner, etc.)
        // 4. Persist state changes
        // 5. Return response

        throw new NotImplementedException();
    }
}
```

### 2. Request DTO — Zephyrus.Application/UseCases/{Name}Request.cs
```csharp
namespace Zephyrus.Application.UseCases;

public record {Name}Request(
    // Add request properties
    Guid FeatureId
);
```

### 3. Response DTO — Zephyrus.Application/UseCases/{Name}Response.cs
```csharp
namespace Zephyrus.Application.UseCases;

public record {Name}Response(
    bool Success,
    string? ErrorMessage
    // Add response properties
);
```

### 4. Unit test — Zephyrus.Tests/Application/{Name}UseCaseTests.cs
```csharp
namespace Zephyrus.Tests.Application;

public class {Name}UseCaseTests
{
    private readonly Mock<IFeatureRepository> _features = new();
    // Add other mocks as needed

    [Fact]
    public async Task ExecuteAsync_When{HappyPathCondition}_Should{ExpectedResult}()
    {
        // Arrange
        var useCase = new {Name}UseCase(_features.Object);
        var request = new {Name}Request(FeatureId: Guid.NewGuid());

        // Act
        var result = await useCase.ExecuteAsync(request);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_When{ErrorCondition}_Should{ExpectedBehaviour}()
    {
        // Arrange + Act + Assert
    }
}
```

---

## DI Registration

Add to `Zephyrus.Api/DependencyInjection.cs`:
```csharp
services.AddScoped<{Name}UseCase>();
```

## Controller Wiring

Add to the relevant controller in `Zephyrus.Api/Controllers/`:
```csharp
[HttpPost("{id}/{action}")]
public async Task<IActionResult> {Name}(Guid id, [{Name}Request] request)
{
    var result = await _{camelName}UseCase.ExecuteAsync(request with { FeatureId = id });
    return result.Success ? Ok(result) : BadRequest(result.ErrorMessage);
}
```

---

## Checklist Before Finishing

- [ ] Use case only references Zephyrus.Core interfaces
- [ ] Request and Response are records
- [ ] Unit test covers at minimum: happy path + one error path
- [ ] Use case registered in DI
- [ ] Controller action is ≤15 lines
- [ ] No business logic in the controller
