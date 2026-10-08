using System.Security.Claims;
using BoxTrack.Application;
using Microsoft.AspNetCore.Http;

namespace BoxTrack.Infrastructure;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Username;

    public string? Username => User?.Identity?.Name ?? User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? Role => User?.FindFirst(ClaimTypes.Role)?.Value;

    public int? DepartmentId
    {
        get
        {
            var claim = User?.FindFirst("DepartmentId")?.Value;
            if (int.TryParse(claim, out var deptId))
            {
                return deptId;
            }
            return null;
        }
    }

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool IsProduction => string.Equals(Role, "Production", StringComparison.OrdinalIgnoreCase);

    public bool IsQc => string.Equals(Role, "QC", StringComparison.OrdinalIgnoreCase);
}
