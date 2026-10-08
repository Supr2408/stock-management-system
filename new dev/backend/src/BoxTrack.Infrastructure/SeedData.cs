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

        // 4. Seed standard label templates if missing
        if (!await db.LabelTemplates.AnyAsync(t => t.WidthMm == 50 && t.HeightMm == 30))
        {
            var a4_32Template = new LabelTemplate
            {
                Name = "A4 32-Up Grid (50x30mm)",
                PrinterType = BarcodePrinterMode.Laser,
                WidthMm = 50.0,
                HeightMm = 30.0,
                GapMm = 2.0,
                Orientation = 0,
                IsDefault = false,
                CreatedBy = "seed",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Elements =
                [
                    new() { ElementType = TemplateElementType.Logo, Xmm = 2, Ymm = 2, WidthMm = 20, HeightMm = 7, FitMode = LogoFitMode.Contain },
                    new() { ElementType = TemplateElementType.Text, Xmm = 23, Ymm = 2, WidthMm = 25, HeightMm = 6, FontSize = 8, Content = "{ItemName}" },
                    new() { ElementType = TemplateElementType.Barcode, Xmm = 2, Ymm = 10, WidthMm = 46, HeightMm = 12, BarcodeType = "128", HumanReadable = true, Content = "{Barcode}" },
                    new() { ElementType = TemplateElementType.Text, Xmm = 2, Ymm = 23, WidthMm = 46, HeightMm = 5, FontSize = 7, Content = "MFG: {MfgDate} | {Barcode}" }
                ]
            };
            db.LabelTemplates.Add(a4_32Template);
        }

        if (!await db.LabelTemplates.AnyAsync(t => t.WidthMm == 100 && t.HeightMm == 50))
        {
            var tscTemplate = new LabelTemplate
            {
                Name = "Standard TSC 100x50 Shipping",
                PrinterType = BarcodePrinterMode.Tsc,
                WidthMm = 100.0,
                HeightMm = 50.0,
                GapMm = 3.0,
                Orientation = 0,
                IsDefault = true,
                CreatedBy = "seed",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Elements =
                [
                    new() { ElementType = TemplateElementType.Logo, Xmm = 5, Ymm = 3, WidthMm = 90, HeightMm = 12, FitMode = LogoFitMode.Contain },
                    new() { ElementType = TemplateElementType.Text, Xmm = 5, Ymm = 16, WidthMm = 90, HeightMm = 6, FontSize = 12, Content = "{ItemName}" },
                    new() { ElementType = TemplateElementType.Barcode, Xmm = 5, Ymm = 23, WidthMm = 90, HeightMm = 18, BarcodeType = "128", HumanReadable = true, Content = "{Barcode}" },
                    new() { ElementType = TemplateElementType.Text, Xmm = 5, Ymm = 43, WidthMm = 90, HeightMm = 5, FontSize = 8, Content = "MFG: {MfgDate}  Sr: {SerialNo}" }
                ]
            };
            db.LabelTemplates.Add(tscTemplate);
        }

        await db.SaveChangesAsync();
    }
}
