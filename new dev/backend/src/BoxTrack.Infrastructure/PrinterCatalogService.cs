using System.Text.Json;
using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class PrinterCatalogService(
    BoxTrackDbContext db,
    IPrinterDiscoveryService discoveryService,
    IPrintService printService
) : IPrinterCatalog
{
    public async Task<IReadOnlyList<AvailablePrinterDto>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
    {
        return await discoveryService.GetAvailablePrintersAsync(cancellationToken);
    }

    public async Task<PrinterConfigurationsSummaryDto> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var configs = await db.PrinterConfigurations
            .AsNoTracking()
            .Include(c => c.ActiveTemplate)
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        var regular = configs.FirstOrDefault(c => c.Category == PrinterCategory.RegularDocument);
        var barcode = configs.FirstOrDefault(c => c.Category == PrinterCategory.Barcode);

        if (barcode != null && barcode.ActiveTemplate == null)
        {
            var defaultTpl = await db.LabelTemplates
                .AsNoTracking()
                .Where(t => t.IsActive && t.IsDefault)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (defaultTpl != null)
            {
                barcode.ActiveTemplate = defaultTpl;
                barcode.ActiveTemplateId = defaultTpl.Id;
            }
        }

        return new PrinterConfigurationsSummaryDto(
            RegularDocumentPrinter: regular == null ? null : ToDto(regular),
            BarcodePrinter: barcode == null ? null : ToDto(barcode)
        );
    }

    public async Task<PrinterConfigurationDto> SetConfigurationAsync(
        PrinterCategory category,
        string printerName,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new InvalidOperationException("Printer name cannot be empty.");
        }

        var availablePrinters = await discoveryService.GetAvailablePrintersAsync(cancellationToken);
        var matching = availablePrinters.FirstOrDefault(p =>
            string.Equals(p.Name, printerName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.DisplayName, printerName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (matching == null)
        {
            throw new InvalidOperationException($"Printer '{printerName}' is not installed or available on the server PC.");
        }

        var targetPrinterName = matching.Name;
        var existing = await db.PrinterConfigurations
            .SingleOrDefaultAsync(c => c.Category == category && c.IsActive, cancellationToken);

        if (existing != null)
        {
            var beforeJson = JsonSerializer.Serialize(new
            {
                existing.Id,
                Category = existing.Category.ToString(),
                existing.PrinterName,
                existing.IsActive
            });

            existing.PrinterName = targetPrinterName;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.CreatedBy = userId;

            var afterJson = JsonSerializer.Serialize(new
            {
                existing.Id,
                Category = existing.Category.ToString(),
                existing.PrinterName,
                existing.IsActive
            });

            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                EntityName = "PrinterConfiguration",
                Action = "Updated",
                BeforeJson = beforeJson,
                AfterJson = afterJson
            });

            await db.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }
        else
        {
            var newConfig = new PrinterConfiguration
            {
                Category = category,
                PrinterName = targetPrinterName,
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            db.PrinterConfigurations.Add(newConfig);

            var afterJson = JsonSerializer.Serialize(new
            {
                Category = newConfig.Category.ToString(),
                newConfig.PrinterName,
                newConfig.IsActive
            });

            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                EntityName = "PrinterConfiguration",
                Action = "Created",
                AfterJson = afterJson
            });

            await db.SaveChangesAsync(cancellationToken);
            return ToDto(newConfig);
        }
    }

    public async Task<PrinterConfigurationDto> SetBarcodeConfigurationAsync(
        UpdateBarcodePrinterConfigRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            throw new InvalidOperationException("Printer name cannot be empty.");
        }

        var availablePrinters = await discoveryService.GetAvailablePrintersAsync(cancellationToken);
        var matching = availablePrinters.FirstOrDefault(p =>
            string.Equals(p.Name, request.PrinterName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.DisplayName, request.PrinterName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (matching == null)
        {
            throw new InvalidOperationException($"Printer '{request.PrinterName}' is not installed or available on the server PC.");
        }

        var existing = await db.PrinterConfigurations
            .Include(c => c.ActiveTemplate)
            .SingleOrDefaultAsync(c => c.Category == PrinterCategory.Barcode && c.IsActive, cancellationToken);

        var mode = request.Mode == 2 ? BarcodePrinterMode.Tsc : BarcodePrinterMode.Laser;

        int? activeTemplateId = request.ActiveTemplateId;
        if (!activeTemplateId.HasValue)
        {
            var defaultTpl = await db.LabelTemplates
                .Where(t => t.IsActive && t.IsDefault)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (defaultTpl != null) activeTemplateId = defaultTpl.Id;
        }

        if (existing != null)
        {
            existing.PrinterName = matching.Name;
            existing.Mode = mode;
            existing.Model = string.IsNullOrWhiteSpace(request.Model) ? "TSC TTP-247" : request.Model.Trim();
            existing.Dpi = request.Dpi > 0 ? request.Dpi : 203;
            existing.ActiveTemplateId = activeTemplateId;
            existing.PaperSize = string.IsNullOrWhiteSpace(request.PaperSize) ? "A4" : request.PaperSize.Trim();
            existing.PaperWidthMm = request.PaperWidthMm > 0 ? request.PaperWidthMm : 210.0;
            existing.PaperHeightMm = request.PaperHeightMm > 0 ? request.PaperHeightMm : 297.0;
            existing.MarginLeftMm = request.MarginLeftMm >= 0 ? request.MarginLeftMm : 10.0;
            existing.MarginRightMm = request.MarginRightMm >= 0 ? request.MarginRightMm : 10.0;
            existing.MarginTopMm = request.MarginTopMm >= 0 ? request.MarginTopMm : 10.0;
            existing.MarginBottomMm = request.MarginBottomMm >= 0 ? request.MarginBottomMm : 10.0;
            existing.HorizontalGapMm = request.HorizontalGapMm >= 0 ? request.HorizontalGapMm : 2.0;
            existing.VerticalGapMm = request.VerticalGapMm >= 0 ? request.VerticalGapMm : 2.0;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.CreatedBy = userId;

            await db.SaveChangesAsync(cancellationToken);
            if (existing.ActiveTemplateId.HasValue)
            {
                await db.Entry(existing).Reference(e => e.ActiveTemplate).LoadAsync(cancellationToken);
            }
            return ToDto(existing);
        }
        else
        {
            var newConfig = new PrinterConfiguration
            {
                Category = PrinterCategory.Barcode,
                PrinterName = matching.Name,
                Mode = mode,
                Model = string.IsNullOrWhiteSpace(request.Model) ? "TSC TTP-247" : request.Model.Trim(),
                Dpi = request.Dpi > 0 ? request.Dpi : 203,
                ActiveTemplateId = activeTemplateId,
                PaperSize = string.IsNullOrWhiteSpace(request.PaperSize) ? "A4" : request.PaperSize.Trim(),
                PaperWidthMm = request.PaperWidthMm > 0 ? request.PaperWidthMm : 210.0,
                PaperHeightMm = request.PaperHeightMm > 0 ? request.PaperHeightMm : 297.0,
                MarginLeftMm = request.MarginLeftMm >= 0 ? request.MarginLeftMm : 10.0,
                MarginRightMm = request.MarginRightMm >= 0 ? request.MarginRightMm : 10.0,
                MarginTopMm = request.MarginTopMm >= 0 ? request.MarginTopMm : 10.0,
                MarginBottomMm = request.MarginBottomMm >= 0 ? request.MarginBottomMm : 10.0,
                HorizontalGapMm = request.HorizontalGapMm >= 0 ? request.HorizontalGapMm : 2.0,
                VerticalGapMm = request.VerticalGapMm >= 0 ? request.VerticalGapMm : 2.0,
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            db.PrinterConfigurations.Add(newConfig);
            await db.SaveChangesAsync(cancellationToken);
            if (newConfig.ActiveTemplateId.HasValue)
            {
                await db.Entry(newConfig).Reference(e => e.ActiveTemplate).LoadAsync(cancellationToken);
            }
            return ToDto(newConfig);
        }
    }

    public async Task<PrintJobDto> PrintReportDocumentAsync(PrintReportDocumentRequest request, string? userId, CancellationToken cancellationToken = default)
    {
        return await printService.PrintReportDocumentAsync(request, userId, cancellationToken);
    }

    public async Task<PrintJobDto> TestPrintAsync(PrinterCategory category, string? userId, CancellationToken cancellationToken = default)
    {
        return await printService.TestPrintAsync(category, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<PrintJobDto>> ListRecentJobsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        var clampedLimit = Math.Clamp(limit, 1, 100);
        var jobs = await db.PrintJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(clampedLimit)
            .ToListAsync(cancellationToken);

        return jobs.Select(j => new PrintJobDto(
            j.Id,
            j.Category.ToString(),
            j.PrinterName,
            j.DocumentName,
            j.DocumentReference,
            j.Copies,
            j.Status.ToString(),
            j.RequestedBy,
            j.CreatedAt,
            j.StartedAt,
            j.CompletedAt,
            j.ErrorMessage
        )).ToList();
    }

    private static PrinterConfigurationDto ToDto(PrinterConfiguration entity) => new(
        entity.Id,
        entity.Category.ToString(),
        entity.PrinterName,
        entity.IsActive,
        entity.UpdatedAt,
        entity.CreatedBy,
        (int)entity.Mode,
        entity.Model,
        entity.Dpi,
        entity.ActiveTemplateId,
        entity.ActiveTemplate?.Name,
        entity.PaperSize,
        entity.PaperWidthMm,
        entity.PaperHeightMm,
        entity.MarginLeftMm,
        entity.MarginRightMm,
        entity.MarginTopMm,
        entity.MarginBottomMm,
        entity.HorizontalGapMm,
        entity.VerticalGapMm
    );
}
