using BoxTrack.Domain;

namespace BoxTrack.Application;

public sealed record TscCommandResult(
    byte[] Payload,
    string CommandText
);

public interface ITscCommandGenerator
{
    Task<TscCommandResult> GenerateLabelAsync(
        LabelTemplate template,
        IReadOnlyDictionary<string, string> dynamicValues,
        int copies = 1,
        CancellationToken cancellationToken = default);

    Task<TscCommandResult> GenerateTestPrintAsync(
        LabelTemplate template,
        string requestedBy = "Admin",
        CancellationToken cancellationToken = default);
}

public interface IPrinterTransport
{
    Task SendRawAsync(string printerName, byte[] data, string documentName, CancellationToken cancellationToken = default);
    Task<string> GetPrinterStatusAsync(string printerName, CancellationToken cancellationToken = default);
}

public interface ITscLabelPrinter
{
    Task<PrintJobDto> PrintTemplateAsync(
        int templateId,
        string printerName,
        IReadOnlyDictionary<string, string> dynamicValues,
        int copies = 1,
        string? requestedBy = null,
        CancellationToken cancellationToken = default);

    Task<PrintJobDto> TestPrintTemplateAsync(
        int templateId,
        string printerName,
        string? requestedBy = null,
        CancellationToken cancellationToken = default);
}
