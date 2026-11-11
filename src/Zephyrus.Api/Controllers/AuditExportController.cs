using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Zephyrus.Application.Managers;

namespace Zephyrus.Api.Controllers;

/// <summary>
/// Downloads the audit trail as a JSON file (format <c>zephyrus.audit/v1</c>).
/// </summary>
[ApiController]
[Route("api")]
public class AuditExportController : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly AuditExportManager _audit;

    public AuditExportController(AuditExportManager audit)
    {
        _audit = audit;
    }

    [HttpGet("features/{id:guid}/audit-export")]
    public async Task<IActionResult> ExportFeature(Guid id, CancellationToken ct) =>
        Download(await _audit.ExportFeatureAsync(id, ct), $"zephyrus-audit-feature-{id}.json");

    [HttpGet("projects/{id:guid}/audit-export")]
    public async Task<IActionResult> ExportProject(Guid id, CancellationToken ct) =>
        Download(await _audit.ExportProjectAsync(id, ct), $"zephyrus-audit-project-{id}.json");

    private FileContentResult Download(AuditExport export, string fileName) =>
        File(JsonSerializer.SerializeToUtf8Bytes(export, Json), "application/json", fileName);
}
