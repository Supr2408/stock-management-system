using BoxTrack.Application;
using BoxTrack.Domain;
using BoxTrack.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Application.Tests;

public sealed class PrinterCatalogTests
{
    private sealed class FakeDiscoveryService(IReadOnlyList<AvailablePrinterDto> printers) : IPrinterDiscoveryService
    {
        public Task<IReadOnlyList<AvailablePrinterDto>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(printers);
    }

    private sealed class FakePrintService : IPrintService
    {
        public PrinterCategory? LastPrintedCategory { get; private set; }
        public string? LastRequestedBy { get; private set; }

        public Task<PrintJobDto> PrintDocumentAsync(PrinterCategory category, string documentName, byte[] content, string? requestedBy, int copies = 1, CancellationToken cancellationToken = default)
        {
            LastPrintedCategory = category;
            LastRequestedBy = requestedBy;
            return Task.FromResult(new PrintJobDto(1, category.ToString(), "TestPrinter", documentName, null, copies, "Completed", requestedBy, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null));
        }

        public Task<PrintJobDto> PrintReportDocumentAsync(PrintReportDocumentRequest request, string? requestedBy, CancellationToken cancellationToken = default)
        {
            LastPrintedCategory = PrinterCategory.RegularDocument;
            LastRequestedBy = requestedBy;
            return Task.FromResult(new PrintJobDto(1, PrinterCategory.RegularDocument.ToString(), "TestPrinter", request.Title, null, 1, "Completed", requestedBy, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null));
        }

        public Task<PrintJobDto?> PrintBarcodeLabelsAsync(IReadOnlyList<GeneratedBarcodePrintItem> labels, string? requestedBy, int? templateId = null, CancellationToken cancellationToken = default)
        {
            LastPrintedCategory = PrinterCategory.Barcode;
            LastRequestedBy = requestedBy;
            return Task.FromResult<PrintJobDto?>(new PrintJobDto(1, PrinterCategory.Barcode.ToString(), "TestPrinter", "Barcode_Batch", null, labels.Count, "Completed", requestedBy, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null));
        }

        public Task<PrintJobDto> TestPrintAsync(PrinterCategory category, string? requestedBy, CancellationToken cancellationToken = default)
        {
            LastPrintedCategory = category;
            LastRequestedBy = requestedBy;
            return Task.FromResult(new PrintJobDto(1, category.ToString(), "TestPrinter", "TestDoc", null, 1, "Completed", requestedBy, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null));
        }
    }

    private static BoxTrackDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BoxTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new BoxTrackDbContext(options);
    }

    [Fact]
    public async Task GetAvailablePrintersAsync_ReturnsDiscoveredPrinters()
    {
        using var db = CreateInMemoryDbContext();
        var discovery = new FakeDiscoveryService(new[]
        {
            new AvailablePrinterDto("HP LaserJet M1005", "HP LaserJet M1005", "Ready", true),
            new AvailablePrinterDto("Zebra ZD220", "Zebra ZD220", "Ready", false)
        });
        var printService = new FakePrintService();
        var catalog = new PrinterCatalogService(db, discovery, printService);

        var printers = await catalog.GetAvailablePrintersAsync();

        Assert.Equal(2, printers.Count);
        Assert.Equal("HP LaserJet M1005", printers[0].Name);
        Assert.True(printers[0].IsDefault);
        Assert.Equal("Zebra ZD220", printers[1].Name);
    }

    [Fact]
    public async Task SetConfigurationAsync_WithInvalidPrinter_ThrowsException()
    {
        using var db = CreateInMemoryDbContext();
        var discovery = new FakeDiscoveryService(new[]
        {
            new AvailablePrinterDto("HP LaserJet M1005", "HP LaserJet M1005", "Ready", true)
        });
        var printService = new FakePrintService();
        var catalog = new PrinterCatalogService(db, discovery, printService);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.SetConfigurationAsync(PrinterCategory.RegularDocument, "NonExistentPrinter", "admin"));

        Assert.Contains("not installed or available", ex.Message);
    }

    [Fact]
    public async Task SetConfigurationAsync_WithEmptyName_ThrowsException()
    {
        using var db = CreateInMemoryDbContext();
        var discovery = new FakeDiscoveryService([]);
        var printService = new FakePrintService();
        var catalog = new PrinterCatalogService(db, discovery, printService);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.SetConfigurationAsync(PrinterCategory.RegularDocument, "   ", "admin"));

        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public async Task SetConfigurationAsync_CreatesConfigurationAndAuditLog()
    {
        using var db = CreateInMemoryDbContext();
        var discovery = new FakeDiscoveryService(new[]
        {
            new AvailablePrinterDto("HP LaserJet M1005", "HP LaserJet M1005", "Ready", true),
            new AvailablePrinterDto("Zebra ZD220", "Zebra ZD220", "Ready", false)
        });
        var printService = new FakePrintService();
        var catalog = new PrinterCatalogService(db, discovery, printService);

        var regularConfig = await catalog.SetConfigurationAsync(PrinterCategory.RegularDocument, "HP LaserJet M1005", "admin");
        var barcodeConfig = await catalog.SetConfigurationAsync(PrinterCategory.Barcode, "Zebra ZD220", "admin");

        Assert.Equal("HP LaserJet M1005", regularConfig.PrinterName);
        Assert.Equal("RegularDocument", regularConfig.Category);
        Assert.Equal("Zebra ZD220", barcodeConfig.PrinterName);
        Assert.Equal("Barcode", barcodeConfig.Category);

        var summary = await catalog.GetConfigurationAsync();
        Assert.NotNull(summary.RegularDocumentPrinter);
        Assert.Equal("HP LaserJet M1005", summary.RegularDocumentPrinter.PrinterName);
        Assert.NotNull(summary.BarcodePrinter);
        Assert.Equal("Zebra ZD220", summary.BarcodePrinter.PrinterName);

        var auditLogs = await db.AuditLogs.Where(a => a.EntityName == "PrinterConfiguration").ToListAsync();
        Assert.Equal(2, auditLogs.Count);
        Assert.All(auditLogs, a => Assert.Equal("Created", a.Action));
    }

    [Fact]
    public async Task SetConfigurationAsync_UpdatesExistingConfigurationAndCreatesUpdateAuditLog()
    {
        using var db = CreateInMemoryDbContext();
        var discovery = new FakeDiscoveryService(new[]
        {
            new AvailablePrinterDto("HP LaserJet M1005", "HP LaserJet M1005", "Ready", true),
            new AvailablePrinterDto("Canon LBP2900", "Canon LBP2900", "Ready", false)
        });
        var printService = new FakePrintService();
        var catalog = new PrinterCatalogService(db, discovery, printService);

        await catalog.SetConfigurationAsync(PrinterCategory.RegularDocument, "HP LaserJet M1005", "admin1");
        var updated = await catalog.SetConfigurationAsync(PrinterCategory.RegularDocument, "Canon LBP2900", "admin2");

        Assert.Equal("Canon LBP2900", updated.PrinterName);

        var configs = await db.PrinterConfigurations.Where(c => c.Category == PrinterCategory.RegularDocument).ToListAsync();
        Assert.Single(configs);
        Assert.Equal("Canon LBP2900", configs[0].PrinterName);

        var auditLogs = await db.AuditLogs.Where(a => a.EntityName == "PrinterConfiguration").OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, auditLogs.Count);
        Assert.Equal("Created", auditLogs[0].Action);
        Assert.Equal("Updated", auditLogs[1].Action);
        Assert.Contains("HP LaserJet M1005", auditLogs[1].BeforeJson!);
        Assert.Contains("Canon LBP2900", auditLogs[1].AfterJson!);
    }

    [Fact]
    public async Task TestPrintAsync_DelegatesToPrintServiceWithCorrectCategory()
    {
        using var db = CreateInMemoryDbContext();
        var discovery = new FakeDiscoveryService([]);
        var printService = new FakePrintService();
        var catalog = new PrinterCatalogService(db, discovery, printService);

        var job = await catalog.TestPrintAsync(PrinterCategory.Barcode, "admin_user");

        Assert.Equal(PrinterCategory.Barcode, printService.LastPrintedCategory);
        Assert.Equal("admin_user", printService.LastRequestedBy);
        Assert.Equal("Completed", job.Status);
    }

    [Fact]
    public async Task PrintReportDocumentAsync_DelegatesToPrintServiceWithRegularDocumentCategory()
    {
        using var db = CreateInMemoryDbContext();
        var discovery = new FakeDiscoveryService([]);
        var printService = new FakePrintService();
        var catalog = new PrinterCatalogService(db, discovery, printService);

        var request = new PrintReportDocumentRequest(
            Title: "Stock Report",
            Subtitle: "All departments",
            Address: null,
            Headers: new[] { "Code", "Name", "Qty" },
            Rows: new[] { new[] { "101", "Foil", "50" } }
        );

        var job = await catalog.PrintReportDocumentAsync(request, "admin_user");

        Assert.Equal(PrinterCategory.RegularDocument, printService.LastPrintedCategory);
        Assert.Equal("admin_user", printService.LastRequestedBy);
        Assert.Equal("Stock Report", job.DocumentName);
        Assert.Equal("Completed", job.Status);
    }
}
