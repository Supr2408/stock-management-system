using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.Versioning;
using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoxTrack.Infrastructure;

public sealed class WindowsPrintService(
    BoxTrackDbContext db,
    ITscCommandGenerator tscCommandGenerator,
    IPrinterTransport printerTransport,
    LaserA4LabelCompositor laserCompositor,
    ILogger<WindowsPrintService> logger) : IPrintService
{
    public async Task<PrintJobDto> PrintDocumentAsync(PrinterCategory category, string documentName, byte[] content, string? requestedBy, int copies = 1, CancellationToken cancellationToken = default)
    {
        var config = await db.PrinterConfigurations.SingleOrDefaultAsync(c => c.Category == category && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"No printer has been configured for {category}. Please configure a printer first.");

        var job = new PrintJob
        {
            Category = category,
            PrinterName = config.PrinterName,
            DocumentName = documentName,
            Copies = Math.Max(1, copies),
            Status = PrintJobStatus.Queued,
            RequestedBy = requestedBy,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow
        };

        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        if (!OperatingSystem.IsWindows())
        {
            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(job);
        }

        try
        {
            job.Status = PrintJobStatus.Printing;
            await db.SaveChangesAsync(cancellationToken);

            ExecuteWindowsRawOrDocumentPrint(job, config.PrinterName, content);

            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to print document {DocumentName} on printer {PrinterName}", documentName, config.PrinterName);
            job.Status = PrintJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Printing failed on printer '{config.PrinterName}': {ex.Message}", ex);
        }

        return ToDto(job);
    }

    public async Task<PrintJobDto> PrintReportDocumentAsync(PrintReportDocumentRequest request, string? requestedBy, CancellationToken cancellationToken = default)
    {
        var config = await db.PrinterConfigurations.SingleOrDefaultAsync(c => c.Category == PrinterCategory.RegularDocument && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No printer configured for Regular Document Printing. Please select a regular document printer under More -> Printing.");

        var job = new PrintJob
        {
            Category = PrinterCategory.RegularDocument,
            PrinterName = config.PrinterName,
            DocumentName = request.Title,
            Copies = 1,
            Status = PrintJobStatus.Queued,
            RequestedBy = requestedBy,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow
        };

        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        if (!OperatingSystem.IsWindows())
        {
            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(job);
        }

        try
        {
            job.Status = PrintJobStatus.Printing;
            await db.SaveChangesAsync(cancellationToken);

            ExecuteWindowsDocumentPrint(request.Title, request.Subtitle, request.Address, request.Headers, request.Rows, config.PrinterName, job);

            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to print report document {Title} on regular printer {PrinterName}", request.Title, config.PrinterName);
            job.Status = PrintJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Printing report document failed on '{config.PrinterName}': {ex.Message}", ex);
        }

        return ToDto(job);
    }

    public async Task<PrintJobDto?> PrintBarcodeLabelsAsync(IReadOnlyList<GeneratedBarcodePrintItem> labels, string? requestedBy, CancellationToken cancellationToken = default)
    {
        if (labels.Count == 0) return null;

        var config = await db.PrinterConfigurations
            .Include(c => c.ActiveTemplate)
            .ThenInclude(t => t!.Elements)
            .ThenInclude(e => e.Logo)
            .SingleOrDefaultAsync(c => c.Category == PrinterCategory.Barcode && c.IsActive, cancellationToken);

        if (config == null || string.IsNullOrWhiteSpace(config.PrinterName))
        {
            logger.LogWarning("No barcode printer is configured. Skipping automatic barcode printing.");
            return null;
        }

        var docName = labels.Count == 1
            ? $"Barcode_Label_{labels[0].BarcodeValue}"
            : $"Barcode_Batch_{labels[0].BarcodeValue}_to_{labels[^1].BarcodeValue}";

        var job = new PrintJob
        {
            Category = PrinterCategory.Barcode,
            PrinterName = config.PrinterName,
            DocumentName = docName,
            Copies = labels.Count,
            Status = PrintJobStatus.Queued,
            RequestedBy = requestedBy,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow
        };

        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        if (!OperatingSystem.IsWindows())
        {
            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(job);
        }

        try
        {
            job.Status = PrintJobStatus.Printing;
            await db.SaveChangesAsync(cancellationToken);

            if (config.Mode == BarcodePrinterMode.Tsc && config.ActiveTemplate != null)
            {
                // Generate TSPL labels for each label item and send RAW
                foreach (var lbl in labels)
                {
                    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Barcode"] = lbl.BarcodeValue,
                        ["ItemName"] = lbl.ItemName,
                        ["Description"] = lbl.Description,
                        ["MfgDate"] = lbl.ManufactureDate.ToString("dd/MM/yyyy"),
                        ["SerialNo"] = lbl.SerialNumber.ToString("000000"),
                        ["LogoMode"] = lbl.LogoMode
                    };

                    var tscResult = await tscCommandGenerator.GenerateLabelAsync(config.ActiveTemplate, values, 1, cancellationToken);
                    await printerTransport.SendRawAsync(config.PrinterName, tscResult.Payload, job.DocumentName, cancellationToken);
                }
            }
            else
            {
                // Laser / standard Windows vector document print
                ExecuteWindowsBarcodeLabelsPrint(labels, config.PrinterName, job, config);
            }

            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to print barcode labels on printer {PrinterName}", config.PrinterName);
            job.Status = PrintJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Barcode printing failed on printer '{config.PrinterName}': {ex.Message}", ex);
        }

        return ToDto(job);
    }

    public async Task<PrintJobDto> TestPrintAsync(PrinterCategory category, string? requestedBy, CancellationToken cancellationToken = default)
    {
        var config = await db.PrinterConfigurations.SingleOrDefaultAsync(c => c.Category == category && c.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"No printer has been configured for {category}. Please configure a printer first under Printing settings.");

        var documentName = category == PrinterCategory.Barcode ? "Test_Barcode_Label" : "Test_Document";

        var job = new PrintJob
        {
            Category = category,
            PrinterName = config.PrinterName,
            DocumentName = documentName,
            Copies = 1,
            Status = PrintJobStatus.Queued,
            RequestedBy = requestedBy,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow
        };

        db.PrintJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        if (!OperatingSystem.IsWindows())
        {
            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(job);
        }

        try
        {
            job.Status = PrintJobStatus.Printing;
            await db.SaveChangesAsync(cancellationToken);

            ExecuteWindowsTestPrint(category, config.PrinterName, requestedBy, job);

            job.Status = PrintJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test print failed for category {Category} on printer {PrinterName}", category, config.PrinterName);
            job.Status = PrintJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Test print failed on printer '{config.PrinterName}': {ex.Message}", ex);
        }

        return ToDto(job);
    }

    [SupportedOSPlatform("windows")]
    private static void ExecuteWindowsDocumentPrint(
        string title,
        string? subtitle,
        IReadOnlyList<string>? address,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string printerName,
        PrintJob job)
    {
        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings.PrinterName = printerName;

        if (!printDoc.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{printerName}' is not valid or not accessible by the Windows print subsystem.");
        }

        printDoc.DocumentName = job.DocumentName;

        if (printerName.Contains("PDF", StringComparison.OrdinalIgnoreCase))
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BoxTrackPrints");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"{job.DocumentName}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.pdf");
            printDoc.PrinterSettings.PrintToFile = true;
            printDoc.PrinterSettings.PrintFileName = tempFile;
            job.DocumentReference = tempFile;
        }

        int rowIndex = 0;
        int pageNumber = 1;

        printDoc.PrintPage += (sender, e) =>
        {
            var g = e.Graphics ?? throw new InvalidOperationException("Could not obtain printer graphics.");
            float left = 40;
            float top = 40;
            float right = e.PageBounds.Width - 40;
            float bottom = e.PageBounds.Height - 50;

            using var fontTitle = new Font("Arial", 14, FontStyle.Bold);
            using var fontSub = new Font("Arial", 10, FontStyle.Regular);
            using var fontHead = new Font("Arial", 9, FontStyle.Bold);
            using var fontCell = new Font("Arial", 8.5f, FontStyle.Regular);
            using var fontFoot = new Font("Arial", 8, FontStyle.Italic);

            g.DrawString(title, fontTitle, Brushes.Black, left, top);
            top += 25;

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                g.DrawString(subtitle, fontSub, Brushes.DarkSlateGray, left, top);
                top += 18;
            }

            if (pageNumber == 1 && address != null && address.Count > 0)
            {
                foreach (var line in address)
                {
                    g.DrawString(line, fontSub, Brushes.Black, left, top);
                    top += 15;
                }
                top += 6;
            }

            int colCount = Math.Max(1, headers.Count);
            float tableWidth = right - left;
            float colWidth = tableWidth / colCount;

            g.FillRectangle(Brushes.LightGray, left, top, tableWidth, 22);
            g.DrawRectangle(Pens.Black, left, top, tableWidth, 22);
            for (int c = 0; c < headers.Count; c++)
            {
                float colX = left + c * colWidth;
                g.DrawString(headers[c], fontHead, Brushes.Black, new RectangleF(colX + 3, top + 4, colWidth - 6, 16));
                if (c > 0) g.DrawLine(Pens.Gray, colX, top, colX, top + 22);
            }
            top += 22;

            while (rowIndex < rows.Count && top + 20 < bottom)
            {
                var row = rows[rowIndex];
                g.DrawRectangle(Pens.LightGray, left, top, tableWidth, 18);
                for (int c = 0; c < Math.Min(colCount, row.Count); c++)
                {
                    float colX = left + c * colWidth;
                    g.DrawString(row[c] ?? "", fontCell, Brushes.Black, new RectangleF(colX + 3, top + 2, colWidth - 6, 15));
                    if (c > 0) g.DrawLine(Pens.LightGray, colX, top, colX, top + 18);
                }
                top += 18;
                rowIndex++;
            }

            g.DrawString($"Page {pageNumber} | Printed: {DateTimeOffset.Now:yyyy-MM-dd HH:mm} | BoxTrack", fontFoot, Brushes.Gray, left, bottom + 10);
            pageNumber++;
            e.HasMorePages = (rowIndex < rows.Count);
        };

        printDoc.Print();
    }

    [SupportedOSPlatform("windows")]
    private void ExecuteWindowsBarcodeLabelsPrint(
        IReadOnlyList<GeneratedBarcodePrintItem> labels,
        string printerName,
        PrintJob job,
        PrinterConfiguration config)
    {
        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings.PrinterName = printerName;

        if (!printDoc.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{printerName}' is not valid or not accessible.");
        }

        printDoc.DocumentName = job.DocumentName;

        if (printerName.Contains("PDF", StringComparison.OrdinalIgnoreCase))
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BoxTrackPrints");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"{job.DocumentName}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.pdf");
            printDoc.PrinterSettings.PrintToFile = true;
            printDoc.PrinterSettings.PrintFileName = tempFile;
            job.DocumentReference = tempFile;
        }

        // Prepare labels data dictionary
        var labelsData = labels.Select(lbl => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Barcode"] = lbl.BarcodeValue,
            ["ItemName"] = lbl.ItemName,
            ["Description"] = lbl.Description,
            ["MfgDate"] = lbl.ManufactureDate.ToString("dd/MM/yyyy"),
            ["SerialNo"] = lbl.SerialNumber.ToString("000000"),
            ["LogoMode"] = lbl.LogoMode
        }).ToList();

        // Use active template if configured, otherwise create a sensible default 50x30 or 100x50 template
        var template = config.ActiveTemplate ?? new LabelTemplate
        {
            Name = "Default Laser Barcode Label",
            WidthMm = 50.0,
            HeightMm = 30.0,
            Elements =
            [
                new() { ElementType = TemplateElementType.Text, Xmm = 2, Ymm = 2, WidthMm = 46, HeightMm = 5, FontSize = 10, Content = "{ItemName}" },
                new() { ElementType = TemplateElementType.Barcode, Xmm = 2, Ymm = 8, WidthMm = 46, HeightMm = 14, BarcodeType = "128", HumanReadable = true, Content = "{Barcode}" },
                new() { ElementType = TemplateElementType.Text, Xmm = 2, Ymm = 23, WidthMm = 46, HeightMm = 5, FontSize = 8, Content = "MFG: {MfgDate} Sr: {SerialNo}" }
            ]
        };

        var layout = laserCompositor.CalculateLayout(template, config);
        int labelsPerPage = Math.Max(1, layout.LabelsPerPage);
        int totalPages = (int)Math.Ceiling((double)labelsData.Count / labelsPerPage);
        int currentPageIndex = 0;

        printDoc.PrintPage += (sender, e) =>
        {
            if (currentPageIndex >= totalPages) return;
            var g = e.Graphics ?? throw new InvalidOperationException("Could not obtain graphics.");

            laserCompositor.RenderA4SheetPage(g, template, labelsData, currentPageIndex, config);

            currentPageIndex++;
            e.HasMorePages = (currentPageIndex < totalPages);
        };

        printDoc.Print();
    }

    [SupportedOSPlatform("windows")]
    private static void ExecuteWindowsTestPrint(PrinterCategory category, string printerName, string? requestedBy, PrintJob job)
    {
        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings.PrinterName = printerName;

        if (!printDoc.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{printerName}' is not valid or not accessible by the Windows print subsystem.");
        }

        printDoc.DocumentName = job.DocumentName;

        if (category == PrinterCategory.Barcode)
        {
            printDoc.PrintPage += (sender, e) =>
            {
                var g = e.Graphics ?? throw new InvalidOperationException("Could not obtain printer graphics context.");
                using var fontHeading = new Font("Arial", 11, FontStyle.Bold);
                using var fontBarcode = new Font("Courier New", 14, FontStyle.Bold);
                using var fontMeta = new Font("Arial", 8, FontStyle.Regular);

                g.DrawString("BOXTRACK BARCODE TEST", fontHeading, Brushes.Black, 25, 20);
                g.DrawString("|||| | ||||| |||| | ||||| ||||", fontBarcode, Brushes.Black, 25, 45);
                g.DrawString("*TEST-BOXTRACK-001*", fontMeta, Brushes.Black, 25, 75);
                g.DrawString($"Printer: {printerName}", fontMeta, Brushes.DarkGray, 25, 95);
                g.DrawString($"Printed: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}", fontMeta, Brushes.DarkGray, 25, 110);
            };
        }
        else
        {
            printDoc.PrintPage += (sender, e) =>
            {
                var g = e.Graphics ?? throw new InvalidOperationException("Could not obtain printer graphics context.");
                using var fontHeader = new Font("Arial", 16, FontStyle.Bold);
                using var fontSub = new Font("Arial", 12, FontStyle.Bold);
                using var fontBody = new Font("Arial", 10, FontStyle.Regular);
                using var fontFooter = new Font("Arial", 8, FontStyle.Italic);

                g.DrawString("BoxTrack Stock Management System", fontHeader, Brushes.Black, 40, 40);
                g.DrawString("Regular Document Test Page", fontSub, Brushes.Black, 40, 75);
                g.DrawLine(Pens.Black, 40, 100, 520, 100);

                g.DrawString($"Target Printer : {printerName}", fontBody, Brushes.Black, 40, 120);
                g.DrawString($"Requested By   : {requestedBy ?? "Admin"}", fontBody, Brushes.Black, 40, 145);
                g.DrawString($"Timestamp      : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}", fontBody, Brushes.Black, 40, 170);
                g.DrawString("Status         : Server PC printer communication successful.", fontBody, Brushes.Black, 40, 195);
                g.DrawString("Subsystem      : BoxTrack Phase 1 In-Process Print Subsystem", fontFooter, Brushes.Gray, 40, 240);
            };
        }

        if (printerName.Contains("PDF", StringComparison.OrdinalIgnoreCase))
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BoxTrackPrints");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"{job.DocumentName}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.pdf");
            printDoc.PrinterSettings.PrintToFile = true;
            printDoc.PrinterSettings.PrintFileName = tempFile;
            job.DocumentReference = tempFile;
        }

        printDoc.Print();
    }

    [SupportedOSPlatform("windows")]
    private static void ExecuteWindowsRawOrDocumentPrint(PrintJob job, string printerName, byte[] content)
    {
        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings.PrinterName = printerName;

        if (!printDoc.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{printerName}' is not valid or not accessible.");
        }

        printDoc.DocumentName = job.DocumentName;

        if (printerName.Contains("PDF", StringComparison.OrdinalIgnoreCase))
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "BoxTrackPrints");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"{job.DocumentName}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.pdf");
            printDoc.PrinterSettings.PrintToFile = true;
            printDoc.PrinterSettings.PrintFileName = tempFile;
            job.DocumentReference = tempFile;
        }

        printDoc.PrintPage += (sender, e) =>
        {
            var g = e.Graphics;
            if (g == null) return;
            using var font = new Font("Arial", 11, FontStyle.Regular);
            var text = System.Text.Encoding.UTF8.GetString(content);
            g.DrawString(text, font, Brushes.Black, 40, 40);
        };

        printDoc.Print();
    }

    private static PrintJobDto ToDto(PrintJob job) => new(
        job.Id,
        job.Category.ToString(),
        job.PrinterName,
        job.DocumentName,
        job.DocumentReference,
        job.Copies,
        job.Status.ToString(),
        job.RequestedBy,
        job.CreatedAt,
        job.StartedAt,
        job.CompletedAt,
        job.ErrorMessage
    );
}
