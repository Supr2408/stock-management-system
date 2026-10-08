using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BoxTrack.Domain;
using BoxTrack.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BoxTrack.Api.Controllers;

public sealed record LoginRequest(string UserName, string Password);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IConfiguration configuration, BoxTrackDbContext db) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var secret = configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Problem("JWT authentication secret is not configured.", statusCode: 503);
        }

        var normalizedInputUser = request.UserName.Trim();
        var userAccount = await db.UserAccounts
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedInputUser.ToLower() && u.IsActive, cancellationToken);

        string role;
        string userName;
        int? departmentId = null;
        string? departmentName = null;

        if (userAccount != null)
        {
            // Verify password: check temporary dev password or password hash or dev shortcuts
            bool isPasswordValid = false;
            if (!string.IsNullOrWhiteSpace(userAccount.TemporaryDevPassword) && request.Password == userAccount.TemporaryDevPassword)
            {
                isPasswordValid = true;
            }
            else if (!string.IsNullOrWhiteSpace(userAccount.PasswordHash))
            {
                var hasher = new PasswordHasher<object>();
                isPasswordValid = hasher.VerifyHashedPassword(new object(), userAccount.PasswordHash, request.Password) != PasswordVerificationResult.Failed;
            }
            else if (request.Password == "123" || request.Password == "Password123!" || request.Password == "admin")
            {
                // Fallback dev shortcut during transition
                isPasswordValid = true;
            }

            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            role = userAccount.Role;
            userName = userAccount.Username;
            departmentId = userAccount.DepartmentId;
            departmentName = userAccount.Department?.Name;
        }
        else
        {
            // Check legacy environment admin configuration if user table was not yet seeded
            var expectedUser = configuration["ADMIN_USERNAME"] ?? "admin";
            var passwordHash = configuration["ADMIN_PASSWORD_HASH"];
            var hasher = new PasswordHasher<object>();
            var isHashedMatch = !string.IsNullOrWhiteSpace(passwordHash) && hasher.VerifyHashedPassword(new object(), passwordHash, request.Password) != PasswordVerificationResult.Failed;
            var isDevPasswordMatch = request.Password == "Password123!" || request.Password == "admin" || request.Password == "123";

            if (!string.Equals(normalizedInputUser, expectedUser, StringComparison.OrdinalIgnoreCase) || (!isHashedMatch && !isDevPasswordMatch))
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            role = "Admin";
            userName = expectedUser;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, role)
        };

        if (departmentId.HasValue)
        {
            claims.Add(new Claim("DepartmentId", departmentId.Value.ToString()));
            if (!string.IsNullOrWhiteSpace(departmentName))
            {
                claims.Add(new Claim("DepartmentName", departmentName));
            }
        }

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            userName,
            role,
            departmentId,
            departmentName
        });
    }
}

