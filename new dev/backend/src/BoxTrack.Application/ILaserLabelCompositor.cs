using BoxTrack.Domain;

namespace BoxTrack.Application;

public sealed record A4SheetLayout(
    double PaperWidthMm,
    double PaperHeightMm,
    double MarginLeftMm,
    double MarginRightMm,
    double MarginTopMm,
    double MarginBottomMm,
    double HorizontalGapMm,
    double VerticalGapMm,
    int Columns,
    int Rows,
    int LabelsPerPage,
    double UsableWidthMm,
    double UsableHeightMm
);

public interface ILaserLabelCompositor
{
    A4SheetLayout CalculateLayout(
        LabelTemplate template,
        PrinterConfiguration config);

    Task<byte[]> GenerateA4PdfDocumentAsync(
        LabelTemplate template,
        IReadOnlyList<IReadOnlyDictionary<string, string>> labelsData,
        PrinterConfiguration config,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateTestPrintA4PdfAsync(
        LabelTemplate template,
        PrinterConfiguration config,
        string requestedBy = "Admin",
        CancellationToken cancellationToken = default);
}
