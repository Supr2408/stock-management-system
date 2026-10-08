namespace BoxTrack.Application;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Username { get; }
    string? Role { get; }
    int? DepartmentId { get; }
    bool IsAdmin { get; }
    bool IsProduction { get; }
    bool IsQc { get; }
}

public sealed record UserAccountDto(
    int Id,
    string Username,
    string Role,
    int? DepartmentId,
    string? DepartmentName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? TemporaryDevPassword // Visible ONLY in Admin Users development view
);
