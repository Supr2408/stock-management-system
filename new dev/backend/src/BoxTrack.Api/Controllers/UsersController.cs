using BoxTrack.Application;
using BoxTrack.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/users")]
public sealed class UsersController(BoxTrackDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserAccountDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await db.UserAccounts
            .AsNoTracking()
            .Include(u => u.Department)
            .OrderBy(u => u.Role == "Admin" ? 0 : u.Role == "QC" ? 1 : 2)
            .ThenBy(u => u.Username)
            .Select(u => new UserAccountDto(
                u.Id,
                u.Username,
                u.Role,
                u.DepartmentId,
                u.Department != null ? u.Department.Name : null,
                u.IsActive,
                u.CreatedAt,
                u.TemporaryDevPassword
            ))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }
}
