using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public static class SeedData
{
    public static async Task InitializeAsync(BoxTrackDbContext db)
    {
        if (await db.Departments.AnyAsync()) return;
        db.Departments.AddRange(new Department { Name = "AFC", CreatedBy = "seed" }, new Department { Name = "AFR", CreatedBy = "seed" }, new Department { Name = "PLASCON", CreatedBy = "seed" });
        await db.SaveChangesAsync();
    }
}
