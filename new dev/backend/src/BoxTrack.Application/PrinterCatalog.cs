using BoxTrack.Domain;

namespace BoxTrack.Application;

public sealed record AvailablePrinterDto(string Name, string DisplayName, string Status, bool IsDefault);

public sealed record PrinterConfigurationDto(
    int Id,
    string Category,
    string PrinterName,
    bool IsActive,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    int Mode = 1,
    string Model = "TSC TTP-247",
    int Dpi = 203,
    int? ActiveTemplateId = null,
    string? ActiveTemplateName = null
);

public sealed record PrinterConfigurationsSummaryDto(
    PrinterConfigurationDto? RegularDocumentPrinter,
    PrinterConfigurationDto? BarcodePrinter,
    PrinterConfigurationDto? TscPrinter = null
);

public sealed record UpdatePrinterConfigurationRequest(string PrinterName);

public sealed record PrintJobDto(
    long Id,
    string Category,
    string PrinterName,
    string DocumentName,
    string? DocumentReference,
    int Copies,
    string Status,
    string? RequestedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage
);

public sealed record TestPrintRequest(string Category);

public sealed record GeneratedBarcodePrintItem(
    string BarcodeValue,
    string ItemName,
    string Description,
    DateOnly ManufactureDate,
    int SerialNumber,
    string LogoMode
);

public sealed record PrintReportDocumentRequest(
    string Title,
    string? Subtitle,
    IReadOnlyList<string>? Address,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows
);

public interface IPrinterDiscoveryService
{
    Task<IReadOnlyList<AvailablePrinterDto>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default);
}

public interface IPrintService
{
    Task<PrintJobDto> PrintDocumentAsync(PrinterCategory category, string documentName, byte[] content, string? requestedBy, int copies = 1, CancellationToken cancellationToken = default);
    Task<PrintJobDto> PrintReportDocumentAsync(PrintReportDocumentRequest request, string? requestedBy, CancellationToken cancellationToken = default);
    Task<PrintJobDto?> PrintBarcodeLabelsAsync(IReadOnlyList<GeneratedBarcodePrintItem> labels, string? requestedBy, CancellationToken cancellationToken = default);
    Task<PrintJobDto> TestPrintAsync(PrinterCategory category, string? requestedBy, CancellationToken cancellationToken = default);
}

public interface IPrinterCatalog
{
    Task<IReadOnlyList<AvailablePrinterDto>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default);
    Task<PrinterConfigurationsSummaryDto> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task<PrinterConfigurationDto> SetConfigurationAsync(PrinterCategory category, string printerName, string? userId, CancellationToken cancellationToken = default);
    Task<PrinterConfigurationDto> SetBarcodeConfigurationAsync(UpdateBarcodePrinterConfigRequest request, string? userId, CancellationToken cancellationToken = default);
    Task<PrintJobDto> PrintReportDocumentAsync(PrintReportDocumentRequest request, string? userId, CancellationToken cancellationToken = default);
    Task<PrintJobDto> TestPrintAsync(PrinterCategory category, string? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrintJobDto>> ListRecentJobsAsync(int limit = 20, CancellationToken cancellationToken = default);
}
