using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoxTrack.Infrastructure;

public sealed class LabelTemplateCatalogService(
    BoxTrackDbContext db,
    ITscCommandGenerator tscCommandGenerator,
    IPrinterTransport printerTransport,
    ILogger<LabelTemplateCatalogService> logger) : ILabelTemplateCatalog
{
    public async Task<IReadOnlyList<LabelTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await db.LabelTemplates
            .AsNoTracking()
            .Include(t => t.Elements)
            .ThenInclude(e => e.Logo)
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(ToDto).ToList();
    }

    public async Task<LabelTemplateDto> GetTemplateByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await db.LabelTemplates
            .AsNoTracking()
            .Include(t => t.Elements)
            .ThenInclude(e => e.Logo)
            .SingleOrDefaultAsync(t => t.Id == id && t.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Label template #{id} not found.");

        return ToDto(template);
    }

    public async Task<LabelTemplateDto> SaveTemplateAsync(
        int? id,
        SaveLabelTemplateRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Template name is required.");
        if (request.WidthMm <= 0 || request.HeightMm <= 0)
            throw new InvalidOperationException("Label width and height must be positive physical dimensions.");

        // Boundary check each element
        foreach (var el in request.Elements)
        {
            if (el.Xmm < 0 || el.Ymm < 0)
                throw new InvalidOperationException("Element position cannot be negative.");
            if (el.Xmm + el.WidthMm > request.WidthMm + 1.0)
                throw new InvalidOperationException($"Element extends beyond label width ({el.Xmm + el.WidthMm:0.#}mm > {request.WidthMm:0.#}mm).");
            if (el.Ymm + el.HeightMm > request.HeightMm + 1.0)
                throw new InvalidOperationException($"Element extends beyond label height ({el.Ymm + el.HeightMm:0.#}mm > {request.HeightMm:0.#}mm).");
        }

        LabelTemplate entity;
        if (id.HasValue && id.Value > 0)
        {
            entity = await db.LabelTemplates
                .Include(t => t.Elements)
                .SingleOrDefaultAsync(t => t.Id == id.Value && t.IsActive, cancellationToken)
                ?? throw new InvalidOperationException($"Label template #{id.Value} not found.");

            entity.Name = request.Name.Trim();
            entity.PrinterType = request.PrinterType == 2 ? BarcodePrinterMode.Tsc : BarcodePrinterMode.Laser;
            entity.WidthMm = request.WidthMm;
            entity.HeightMm = request.HeightMm;
            entity.GapMm = request.GapMm;
            entity.Orientation = request.Orientation;
            entity.IsDefault = request.IsDefault;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            db.LabelTemplateElements.RemoveRange(entity.Elements);
            entity.Elements.Clear();
        }
        else
        {
            entity = new LabelTemplate
            {
                Name = request.Name.Trim(),
                PrinterType = request.PrinterType == 2 ? BarcodePrinterMode.Tsc : BarcodePrinterMode.Laser,
                WidthMm = request.WidthMm,
                HeightMm = request.HeightMm,
                GapMm = request.GapMm,
                Orientation = request.Orientation,
                IsDefault = request.IsDefault,
                CreatedBy = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.LabelTemplates.Add(entity);
        }

        // Add elements
        foreach (var el in request.Elements)
        {
            var content = el.Content;
            if (string.IsNullOrWhiteSpace(content) && el.LogoId.HasValue)
            {
                var logo = await db.LabelLogos.FindAsync(new object[] { el.LogoId.Value }, cancellationToken);
                if (logo != null) content = logo.FileName;
            }

            entity.Elements.Add(new LabelTemplateElement
            {
                ElementType = (TemplateElementType)el.ElementType,
                Xmm = el.Xmm,
                Ymm = el.Ymm,
                WidthMm = el.WidthMm,
                HeightMm = el.HeightMm,
                Rotation = el.Rotation,
                ZIndex = el.ZIndex,
                Content = content,
                FontName = el.FontName,
                FontSize = el.FontSize,
                BarcodeType = el.BarcodeType,
                HumanReadable = el.HumanReadable,
                FitMode = (LogoFitMode)el.FitMode,
                LogoId = el.LogoId,
                BorderThicknessDots = el.BorderThicknessDots
            });
        }

        if (request.IsDefault)
        {
            var otherDefaults = await db.LabelTemplates
                .Where(t => t.Id != entity.Id && t.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var od in otherDefaults) od.IsDefault = false;
        }

        await db.SaveChangesAsync(cancellationToken);

        return await GetTemplateByIdAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteTemplateAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await db.LabelTemplates
            .SingleOrDefaultAsync(t => t.Id == id && t.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Label template #{id} not found.");

        template.IsActive = false;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PrintJobDto> TestPrintTemplateAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        var template = await db.LabelTemplates
            .Include(t => t.Elements)
            .ThenInclude(e => e.Logo)
            .SingleOrDefaultAsync(t => t.Id == id && t.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Label template #{id} not found.");

        var barcodeConfig = await db.PrinterConfigurations
            .SingleOrDefaultAsync(c => c.Category == PrinterCategory.Barcode && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No Barcode Printer is currently configured. Please configure a barcode printer first.");

        var job = new PrintJob
        {
            Category = PrinterCategory.Barcode,
            PrinterName = barcodeConfig.PrinterName,
            DocumentName = $"Test_Template_{template.Name}",
            Copies = 1,
            Status = PrintJobStatus.Queued,
            RequestedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            job.Status = PrintJobStatus.Printing;
            await db.SaveChangesAsync(cancellationToken);

            var tscResult = await tscCommandGenerator.GenerateTestPrintAsync(template, userId ?? "Admin", cancellationToken);
            await printerTransport.SendRawAsync(barcodeConfig.PrinterName, tscResult.Payload, job.DocumentName, cancellationToken);

            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToJobDto(job);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test print of template #{TemplateId} failed on {PrinterName}", id, barcodeConfig.PrinterName);
            job.Status = PrintJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Test print failed on '{barcodeConfig.PrinterName}': {ex.Message}", ex);
        }
    }

    public async Task<PrintJobDto> PrintTemplateAsync(
        int id,
        PrintTemplateJobRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var template = await db.LabelTemplates
            .Include(t => t.Elements)
            .ThenInclude(e => e.Logo)
            .SingleOrDefaultAsync(t => t.Id == id && t.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Label template #{id} not found.");

        var barcodeConfig = await db.PrinterConfigurations
            .SingleOrDefaultAsync(c => c.Category == PrinterCategory.Barcode && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No Barcode Printer is configured.");

        var job = new PrintJob
        {
            Category = PrinterCategory.Barcode,
            PrinterName = barcodeConfig.PrinterName,
            DocumentName = $"Print_Template_{template.Name}",
            Copies = Math.Max(1, request.Copies),
            Status = PrintJobStatus.Queued,
            RequestedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            job.Status = PrintJobStatus.Printing;
            await db.SaveChangesAsync(cancellationToken);

            var dynamicValues = request.DynamicValues ?? new Dictionary<string, string>();
            var tscResult = await tscCommandGenerator.GenerateLabelAsync(template, dynamicValues, request.Copies, cancellationToken);
            await printerTransport.SendRawAsync(barcodeConfig.PrinterName, tscResult.Payload, job.DocumentName, cancellationToken);

            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToJobDto(job);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Print job for template #{TemplateId} failed on {PrinterName}", id, barcodeConfig.PrinterName);
            job.Status = PrintJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Print job failed on '{barcodeConfig.PrinterName}': {ex.Message}", ex);
        }
    }

    private static LabelTemplateDto ToDto(LabelTemplate t) => new(
        t.Id,
        t.Name,
        (int)t.PrinterType,
        t.WidthMm,
        t.HeightMm,
        t.GapMm,
        t.Orientation,
        t.IsDefault,
        t.Elements.Select(e => new LabelTemplateElementDto(
            e.Id,
            (int)e.ElementType,
            e.Xmm,
            e.Ymm,
            e.WidthMm,
            e.HeightMm,
            e.Rotation,
            e.ZIndex,
            e.Content ?? e.Logo?.FileName,
            e.FontName,
            e.FontSize,
            e.BarcodeType,
            e.HumanReadable,
            (int)e.FitMode,
            e.LogoId,
            e.Logo?.Name,
            e.BorderThicknessDots
        )).ToList(),
        t.CreatedAt,
        t.UpdatedAt
    );

    private static PrintJobDto ToJobDto(PrintJob j) => new(
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
    );
}
