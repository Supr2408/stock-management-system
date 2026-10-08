using BoxTrack.Domain;

namespace BoxTrack.Application;

public sealed record LabelTemplateElementDto(
    int Id,
    int ElementType,
    double Xmm,
    double Ymm,
    double WidthMm,
    double HeightMm,
    int Rotation,
    int ZIndex,
    string? Content,
    string? FontName,
    int FontSize,
    string? BarcodeType,
    bool HumanReadable,
    int FitMode,
    int? LogoId,
    string? LogoName,
    int BorderThicknessDots
);

public sealed record LabelTemplateDto(
    int Id,
    string Name,
    int PrinterType,
    double WidthMm,
    double HeightMm,
    double GapMm,
    int Orientation,
    bool IsDefault,
    IReadOnlyList<LabelTemplateElementDto> Elements,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record SaveLabelTemplateElementRequest(
    int? Id,
    int ElementType,
    double Xmm,
    double Ymm,
    double WidthMm,
    double HeightMm,
    int Rotation,
    int ZIndex,
    string? Content,
    string? FontName,
    int FontSize,
    string? BarcodeType,
    bool HumanReadable,
    int FitMode,
    int? LogoId,
    int BorderThicknessDots
);

public sealed record SaveLabelTemplateRequest(
    string Name,
    int PrinterType,
    double WidthMm,
    double HeightMm,
    double GapMm,
    int Orientation,
    bool IsDefault,
    IReadOnlyList<SaveLabelTemplateElementRequest> Elements
);

public sealed record UpdateBarcodePrinterConfigRequest(
    string PrinterName,
    int Mode, // 1 = Laser, 2 = Tsc
    string Model,
    int Dpi,
    int? ActiveTemplateId,
    string PaperSize = "A4",
    double PaperWidthMm = 210.0,
    double PaperHeightMm = 297.0,
    double MarginLeftMm = 10.0,
    double MarginRightMm = 10.0,
    double MarginTopMm = 10.0,
    double MarginBottomMm = 10.0,
    double HorizontalGapMm = 2.0,
    double VerticalGapMm = 2.0
);

public sealed record PrintTemplateJobRequest(
    int TemplateId,
    int Copies,
    IReadOnlyDictionary<string, string>? DynamicValues
);

public interface ILabelTemplateCatalog
{
    Task<IReadOnlyList<LabelTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default);
    Task<LabelTemplateDto> GetTemplateByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<LabelTemplateDto> SaveTemplateAsync(int? id, SaveLabelTemplateRequest request, string? userId, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(int id, CancellationToken cancellationToken = default);
    Task<PrintJobDto> TestPrintTemplateAsync(int id, string? userId, CancellationToken cancellationToken = default);
    Task<PrintJobDto> PrintTemplateAsync(int id, PrintTemplateJobRequest request, string? userId, CancellationToken cancellationToken = default);
}
