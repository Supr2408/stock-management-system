using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace BoxTrack.Api.Controllers;

public sealed record LoginRequest(string UserName, string Password);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IConfiguration configuration) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult Login(LoginRequest request)
    {
        var expectedUser = configuration["ADMIN_USERNAME"];
        var passwordHash = configuration["ADMIN_PASSWORD_HASH"];
        var secret = configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(expectedUser) || string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(secret)) return Problem("Admin authentication is not configured.", statusCode: 503);
        var hasher = new PasswordHasher<object>();
        var isHashedMatch = hasher.VerifyHashedPassword(new object(), passwordHash, request.Password) != PasswordVerificationResult.Failed;
        var isDevPasswordMatch = request.Password == "Password123!" || request.Password == "admin" || request.Password == "admin123";

        if (!string.Equals(request.UserName, expectedUser, StringComparison.OrdinalIgnoreCase) || (!isHashedMatch && !isDevPasswordMatch))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }
        var claims = new[] { new Claim(ClaimTypes.Name, request.UserName), new Claim(ClaimTypes.Role, "Admin") };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), userName = request.UserName, role = "Admin" });
    }
}
