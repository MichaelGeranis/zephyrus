# /new-endpoint

Add a new API endpoint to Zephyrus.Api.

Endpoint description: $ARGUMENTS
(e.g. "POST /features/{id}/artifacts/{artifactId}/approve" or "GET /projects/{id}/pipeline")

## Instructions

Controllers in Zephyrus.Api are thin adapters only.
Every action delegates immediately to a use case.
Read ARCHITECTURE.md before proceeding.
Controller actions must be ≤15 lines. No exceptions.

---

## Files to Create or Update

### 1. Identify or create the controller
Controllers are grouped by resource:
```
Zephyrus.Api/Controllers/
  FeaturesController.cs
  ProjectsController.cs
  ArtifactsController.cs
  PipelineController.cs
```

### 2. Add the action method
```csharp
/// <summary>
/// [One line description of what this endpoint does]
/// </summary>
/// <response code="200">[Success description]</response>
/// <response code="400">[Error description]</response>
/// <response code="404">Resource not found</response>
[HttpPost("{id}/[action]")]
[ProducesResponseType(typeof({Name}Response), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> {ActionName}(
    Guid id,
    [FromBody] {Name}Request request,
    CancellationToken ct)
{
    var result = await _{camelCaseUseCaseName}.ExecuteAsync(
        request with { FeatureId = id }, ct);

    return result.Success
        ? Ok(result)
        : BadRequest(new { result.ErrorMessage });
}
```

### 3. Request/Response DTOs (if not already created by /new-usecase)
```csharp
// Zephyrus.Api/Models/{Name}Request.cs
// These are HTTP-layer DTOs — separate from Application-layer request records
// Map them explicitly rather than reusing Application records directly

public record {Name}Request(
    // HTTP request body properties only
);
```

### 4. Mapping (if HTTP DTO differs from Application request)
```csharp
// Map in the controller action, not in a separate mapper
var useCaseRequest = new {UseCaseName}Request(
    FeatureId: id,
    // map other fields
);
```

---

## Route Conventions

| Pattern | Usage |
|---------|-------|
| `GET /projects` | List all projects |
| `GET /projects/{id}` | Get single project |
| `POST /projects` | Create project |
| `GET /features/{id}/pipeline` | Get pipeline state for feature |
| `POST /features/{id}/artifacts/{artifactId}/approve` | Approve an artifact |
| `POST /features/{id}/pipeline/start` | Start pipeline for feature |

## Response Conventions

| Situation | HTTP Status |
|-----------|------------|
| Success with data | 200 OK |
| Resource created | 201 Created + Location header |
| Validation error | 400 Bad Request |
| Not found | 404 Not Found |
| Invalid state transition | 409 Conflict |
| Server/agent error | 500 (let global handler catch) |

---

## Checklist Before Finishing

- [ ] Action method is ≤15 lines
- [ ] No business logic in controller
- [ ] Delegates entirely to a use case
- [ ] ProducesResponseType attributes added
- [ ] XML doc comment on action
- [ ] Route follows naming conventions
- [ ] CancellationToken passed through to use case
