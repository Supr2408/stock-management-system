namespace BoxTrack.Application;

public sealed record LabelLogoRow(int Id, string Name, string FileName, string RelativePath, string ContentType);
public sealed record LabelLogoUpload(string Name, string FileName, string ContentType, Stream Content);
public sealed record LabelGenerationInput(int ItemId, DateOnly ManufactureDate, int Quantity, string LogoMode, int? LabelLogoId);
public sealed record GeneratedLabelRow(long Id, int ItemId, int ItemCode, string ItemName, DateOnly ManufactureDate, int SerialNumber, string BarcodeValue, string LogoMode, int? LabelLogoId, string? LogoName, string FileName, string RelativePath);
public sealed record LabelGenerationResult(int Generated, string FromBarcode, string ToBarcode, IReadOnlyList<GeneratedLabelRow> Labels);

public interface ILabelCatalog
{
    Task<IReadOnlyList<LabelLogoRow>> ListLogosAsync(CancellationToken cancellationToken);
    Task<LabelLogoRow> UploadLogoAsync(LabelLogoUpload upload, string? userId, CancellationToken cancellationToken);
    Task<LabelGenerationResult> GenerateAsync(LabelGenerationInput input, string? userId, CancellationToken cancellationToken);
    Task<Page<GeneratedLabelRow>> ListGeneratedAsync(int page, int pageSize, CancellationToken cancellationToken);
}
