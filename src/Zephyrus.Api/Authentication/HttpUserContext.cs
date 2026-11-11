using System.Security.Claims;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Api.Authentication;

/// <summary>
/// Reads the current caller from the authenticated principal. Nothing here
/// comes from request content, so a caller cannot claim to be someone else.
/// </summary>
public sealed class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpUserContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId =>
        IsAuthenticated ? Principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value : null;

    public IReadOnlyCollection<TeamRole> Roles
    {
        get
        {
            if (!IsAuthenticated)
                return Array.Empty<TeamRole>();

            return Principal!.FindAll(ClaimTypes.Role)
                .Select(claim => Enum.TryParse<TeamRole>(claim.Value, ignoreCase: true, out var role)
                    ? role
                    : (TeamRole?)null)
                .Where(role => role.HasValue)
                .Select(role => role!.Value)
                .Distinct()
                .ToArray();
        }
    }
}
