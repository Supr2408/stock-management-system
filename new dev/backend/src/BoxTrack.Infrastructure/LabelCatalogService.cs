using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class LabelCatalogService(BoxTrackDbContext db, string barcodeRoot, IPrintService printService) : ILabelCatalog
{
    private static readonly HashSet<string> AllowedLogoTypes = new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/svg+xml", "image/webp" };

    public async Task<IReadOnlyList<LabelLogoRow>> ListLogosAsync(CancellationToken cancellationToken) =>
        await db.LabelLogos.AsNoTracking().Where(logo => logo.IsActive).OrderBy(logo => logo.Name).Select(logo => new LabelLogoRow(logo.Id, logo.Name, logo.FileName, logo.RelativePath, logo.ContentType)).ToListAsync(cancellationToken);

    public async Task<LabelLogoRow> UploadLogoAsync(LabelLogoUpload upload, string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(upload.Name)) throw new InvalidOperationException("Logo name is required.");
        if (string.IsNullOrWhiteSpace(upload.FileName)) throw new InvalidOperationException("Choose a logo file.");
        if (!AllowedLogoTypes.Contains(upload.ContentType)) throw new InvalidOperationException("Only PNG, JPG, SVG, and WEBP logo files are supported.");
        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".svg" and not ".webp") throw new InvalidOperationException("Only PNG, JPG, SVG, and WEBP logo files are supported.");

        var logosDirectory = Path.Combine(barcodeRoot, "logos");
        Directory.CreateDirectory(logosDirectory);
        var safeName = SafeFileName(upload.Name);
        var fileName = $"{safeName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{extension}";
        var fullPath = Path.Combine(logosDirectory, fileName);
        await using (var target = File.Create(fullPath))
        {
            await upload.Content.CopyToAsync(target, cancellationToken);
        }

        var entity = new LabelLogo { Name = upload.Name.Trim(), FileName = fileName, RelativePath = Path.Combine("barcode", "logos", fileName).Replace('\\', '/'), ContentType = upload.ContentType, CreatedBy = userId };
        db.LabelLogos.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new LabelLogoRow(entity.Id, entity.Name, entity.FileName, entity.RelativePath, entity.ContentType);
    }

    public async Task<LabelGenerationResult> GenerateAsync(LabelGenerationInput input, string? userId, CancellationToken cancellationToken)
    {
        if (input.Quantity is < 1 or > 500) throw new InvalidOperationException("Quantity must be between 1 and 500 labels.");
        if (input.ManufactureDate == default) throw new InvalidOperationException("Manufacturing date is required.");
        var item = await db.Items.Include(value => value.Department).SingleOrDefaultAsync(value => value.Id == input.ItemId && value.IsActive, cancellationToken) ?? throw new InvalidOperationException("Select a valid item from Item Master.");
        if (item.Code is < 101 or > 999) throw new InvalidOperationException("Item code must be a 3 digit value between 101 and 999.");

        var logoMode = NormalizeLogoMode(input.LogoMode);
        LabelLogo? logo = null;
        if (logoMode == "Custom")
            logo = await db.LabelLogos.SingleOrDefaultAsync(value => value.Id == input.LabelLogoId && value.IsActive, cancellationToken) ?? throw new InvalidOperationException("Select a valid uploaded logo.");

        Directory.CreateDirectory(barcodeRoot);
        var labelsDirectory = Path.Combine(barcodeRoot, "labels");
        Directory.CreateDirectory(labelsDirectory);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var nextSerial = (await db.BarcodeLabels.MaxAsync(label => (int?)label.SerialNumber, cancellationToken) ?? 0) + 1;
        var labels = new List<BarcodeLabel>();
        for (var offset = 0; offset < input.Quantity; offset++)
        {
            var serial = nextSerial + offset;
            var barcode = $"{item.Code:000}{input.ManufactureDate:ddMMyy}{serial:000000}";
            if (await db.BarcodeLabels.AnyAsync(label => label.BarcodeValue == barcode, cancellationToken)) throw new InvalidOperationException($"Barcode {barcode} already exists.");
            var fileName = $"{barcode}.svg";
            var label = new BarcodeLabel { ItemId = item.Id, ItemCode = item.Code, ManufactureDate = input.ManufactureDate, SerialNumber = serial, BarcodeValue = barcode, LogoMode = logoMode, LabelLogoId = logo?.Id, FileName = fileName, RelativePath = Path.Combine("barcode", "labels", fileName).Replace('\\', '/'), CreatedBy = userId };
            labels.Add(label);
        }

        db.BarcodeLabels.AddRange(labels);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var label in labels)
        {
            var logoDataUri = logo is null ? null : await LogoDataUriAsync(logo, cancellationToken);
            var svg = BarcodeSvgRenderer.RenderLabel(label.BarcodeValue, item.Name, item.Description ?? item.Department?.Name ?? string.Empty, label.LogoMode, logoDataUri);
            await File.WriteAllTextAsync(Path.Combine(labelsDirectory, label.FileName), svg, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        string? printedTo = null;
        string? printStatus = null;
        try
        {
            var printItems = labels.Select(l => new GeneratedBarcodePrintItem(
                l.BarcodeValue,
                item.Name,
                item.Description ?? item.Department?.Name ?? string.Empty,
                input.ManufactureDate,
                l.SerialNumber,
                l.LogoMode
            )).ToList();

            var printJob = await printService.PrintBarcodeLabelsAsync(printItems, userId, cancellationToken);
            if (printJob != null)
            {
                printedTo = printJob.PrinterName;
                printStatus = printJob.Status;
            }
        }
        catch (Exception)
        {
            printStatus = "Failed";
        }

        var rows = labels.Select(label => ToRow(label, item.Name, logo?.Name)).ToList();
        return new LabelGenerationResult(rows.Count, rows.First().BarcodeValue, rows.Last().BarcodeValue, rows, printedTo, printStatus);
    }

    public async Task<Page<GeneratedLabelRow>> ListGeneratedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.BarcodeLabels.AsNoTracking().Include(label => label.Item).Include(label => label.LabelLogo).OrderByDescending(label => label.Id);
        var total = await query.CountAsync(cancellationToken);
        var labels = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(label => new GeneratedLabelRow(label.Id, label.ItemId, label.ItemCode, label.Item!.Name, label.ManufactureDate, label.SerialNumber, label.BarcodeValue, label.LogoMode, label.LabelLogoId, label.LabelLogo == null ? null : label.LabelLogo.Name, label.FileName, label.RelativePath)).ToListAsync(cancellationToken);
        return new Page<GeneratedLabelRow>(labels, total, page, pageSize);
    }

    private async Task<string> LogoDataUriAsync(LabelLogo logo, CancellationToken cancellationToken)
    {
        var path = Path.Combine(barcodeRoot, "logos", logo.FileName);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return $"data:{logo.ContentType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static GeneratedLabelRow ToRow(BarcodeLabel label, string itemName, string? logoName) => new(label.Id, label.ItemId, label.ItemCode, itemName, label.ManufactureDate, label.SerialNumber, label.BarcodeValue, label.LogoMode, label.LabelLogoId, logoName, label.FileName, label.RelativePath);
    private static string NormalizeLogoMode(string? value) => value switch { "WithALUFO" => "WithALUFO", "WithoutLogo" => "WithoutLogo", "Custom" => "Custom", _ => "Nagreeka" };
    private static string SafeFileName(string value) => string.Concat(value.Trim().Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-').ToLowerInvariant() is { Length: > 0 } result ? result : "logo";
}
