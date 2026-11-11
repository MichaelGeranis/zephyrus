using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Api.Controllers;

/// <summary>
/// Identity of the authenticated caller. The UI uses this to show who is
/// signed in and which approvals they are allowed to give.
/// </summary>
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get([FromServices] IUserContext userContext)
    {
        return Ok(new CurrentUserResponse(
            userContext.UserId!,
            User.Identity?.Name ?? userContext.UserId!,
            userContext.Roles.Select(role => role.ToString()).ToArray()));
    }
}

public record CurrentUserResponse(string Email, string DisplayName, IReadOnlyList<string> Roles);
