using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public static class SeedData
{
    public static string NormalizeDepartmentUsername(string deptName)
    {
        // Deterministic normalization: lowercase alphanumeric only
        var clean = new string(deptName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(clean) ? $"dept{Math.Abs(deptName.GetHashCode()):x}" : clean;
    }

    public static async Task InitializeAsync(BoxTrackDbContext db)
    {
        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(
                new Department { Name = "AFC", CreatedBy = "seed" },
                new Department { Name = "AFR", CreatedBy = "seed" },
                new Department { Name = "PLASCON", CreatedBy = "seed" });
            await db.SaveChangesAsync();
        }

        // 1. Seed Admin account if missing
        if (!await db.UserAccounts.AnyAsync(u => u.Username.ToLower() == "admin"))
        {
            db.UserAccounts.Add(new UserAccount
            {
                Username = "admin",
                Role = "Admin",
                TemporaryDevPassword = "Password123!",
                CreatedBy = "seed"
            });
        }

        // 2. Seed QC account if missing
        if (!await db.UserAccounts.AnyAsync(u => u.Username.ToLower() == "qc"))
        {
            db.UserAccounts.Add(new UserAccount
            {
                Username = "qc",
                Role = "QC",
                TemporaryDevPassword = "123",
                CreatedBy = "seed"
            });
        }

        // 3. Seed Production account for EVERY existing active department
        var allDepartments = await db.Departments.Where(d => d.IsActive).ToListAsync();
        foreach (var dept in allDepartments)
        {
            var username = NormalizeDepartmentUsername(dept.Name);
            var exists = await db.UserAccounts.AnyAsync(u => u.Username.ToLower() == username.ToLower());
            if (!exists)
            {
                db.UserAccounts.Add(new UserAccount
                {
                    Username = username,
                    Role = "Production",
                    DepartmentId = dept.Id,
                    TemporaryDevPassword = "123",
                    CreatedBy = "seed"
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
