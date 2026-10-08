using BoxTrack.Domain;

namespace BoxTrack.Application;

public sealed record ProcessedLogoGraphic(
    int WidthDots,
    int HeightDots,
    int WidthBytes,
    byte[] BitmapData,
    string MimeType
);

public interface IImageProcessor
{
    Task<ProcessedLogoGraphic> ProcessLogoForThermalPrintAsync(
        Stream imageStream,
        double targetWidthMm,
        double targetHeightMm,
        LogoFitMode fitMode,
        int dpi = 203,
        CancellationToken cancellationToken = default);

    Task<(int Width, int Height)> GetImageDimensionsAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default);
}
