using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.Extensions.Logging;

namespace BoxTrack.Infrastructure;

public sealed class TscTsplCommandGenerator(
    IImageProcessor imageProcessor,
    string barcodeRoot,
    ILogger<TscTsplCommandGenerator> logger) : ITscCommandGenerator
{
    public const int Dpi = 203;

    public static int MmToDots(double mm) => (int)Math.Round(mm * (Dpi / 25.4), MidpointRounding.AwayFromZero);

    public async Task<TscCommandResult> GenerateLabelAsync(
        LabelTemplate template,
        IReadOnlyDictionary<string, string> dynamicValues,
        int copies = 1,
        CancellationToken cancellationToken = default)
    {
        var builder = new TscCommandBuilder();
        builder.SetSize(template.WidthMm, template.HeightMm);
        builder.SetGap(template.GapMm);
        builder.SetDirection(template.Orientation);
        builder.Clear();

        var sortedElements = template.Elements.OrderBy(e => e.ZIndex).ThenBy(e => e.Id).ToList();

        foreach (var element in sortedElements)
        {
            var xDots = MmToDots(element.Xmm);
            var yDots = MmToDots(element.Ymm);
            var wDots = MmToDots(element.WidthMm);
            var hDots = MmToDots(element.HeightMm);

            switch (element.ElementType)
            {
                case TemplateElementType.Box:
                    builder.AddBox(xDots, yDots, xDots + wDots, yDots + hDots, Math.Max(1, element.BorderThicknessDots));
                    break;

                case TemplateElementType.Text:
                    var textContent = ResolvePlaceholders(element.Content ?? string.Empty, dynamicValues);
                    var (xMul, yMul) = FontMultiplication(element.FontSize);
                    var font = string.IsNullOrWhiteSpace(element.FontName) ? "3" : element.FontName; // Built-in TSPL font
                    builder.AddText(xDots, yDots, font, element.Rotation, xMul, yMul, textContent);
                    break;

                case TemplateElementType.Barcode:
                    var barcodeVal = ResolvePlaceholders(element.Content ?? "101010101010", dynamicValues);
                    var barcodeType = NormalizeBarcodeType(element.BarcodeType);
                    builder.AddBarcode(
                        xDots,
                        yDots,
                        barcodeType,
                        hDots,
                        element.HumanReadable,
                        element.Rotation,
                        narrowDots: 2,
                        wideDots: 4,
                        barcodeVal);
                    break;

                case TemplateElementType.Logo:
                    string? logoFileName = element.Logo?.FileName;
                    if (string.IsNullOrWhiteSpace(logoFileName) && !string.IsNullOrWhiteSpace(element.Content))
                    {
                        var ext = Path.GetExtension(element.Content).ToLowerInvariant();
                        if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".svg")
                        {
                            logoFileName = element.Content;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(logoFileName) && dynamicValues.TryGetValue("LogoFileName", out var lfn) && !string.IsNullOrWhiteSpace(lfn))
                    {
                        logoFileName = lfn;
                    }

                    if (!string.IsNullOrWhiteSpace(logoFileName))
                    {
                        var cleanLogoName = Path.GetFileName(logoFileName);
                        var logoPath = Path.Combine(barcodeRoot, "logos", cleanLogoName);
                        if (!File.Exists(logoPath))
                        {
                            logoPath = Path.Combine(barcodeRoot, logoFileName.TrimStart('/', '\\'));
                        }
                        if (File.Exists(logoPath))
                        {
                            try
                            {
                                await using var stream = File.OpenRead(logoPath);
                                var processed = await imageProcessor.ProcessLogoForThermalPrintAsync(
                                    stream,
                                    element.WidthMm,
                                    element.HeightMm,
                                    element.FitMode,
                                    Dpi,
                                    cancellationToken);

                                builder.AddBitmap(
                                    xDots,
                                    yDots,
                                    processed.WidthBytes,
                                    processed.HeightDots,
                                    mode: 0,
                                    processed.BitmapData);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to process logo {LogoFile} for TSC printing.", logoFileName);
                            }
                        }
                    }
                    else
                    {
                        var logoMode = dynamicValues.TryGetValue("LogoMode", out var lm) ? lm : element.Content;
                        if (!string.Equals(logoMode, "WithoutLogo", StringComparison.OrdinalIgnoreCase))
                        {
                            var text = string.Equals(logoMode, "WithALUFO", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(element.Content, "WithALUFO", StringComparison.OrdinalIgnoreCase)
                                ? "ALUFO"
                                : "NAGREEKA";
                            builder.AddText(xDots, yDots, "3", 0, 1, 1, text);
                        }
                    }
                    break;
            }
        }

        builder.Print(Math.Max(1, copies));

        return new TscCommandResult(builder.BuildPayload(), builder.BuildCommandText());
    }

    public async Task<TscCommandResult> GenerateTestPrintAsync(
        LabelTemplate template,
        string requestedBy = "Admin",
        CancellationToken cancellationToken = default)
    {
        var sampleData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Barcode"] = "143081026000001",
            ["ItemName"] = "450 ML PREMIUM TEST",
            ["ItemCode"] = "143",
            ["MfgDate"] = DateTime.UtcNow.ToString("dd/MM/yyyy"),
            ["BatchNo"] = "BT-SAMPLE-01",
            ["SerialNo"] = "000001",
            ["RequestedBy"] = requestedBy
        };

        return await GenerateLabelAsync(template, sampleData, 1, cancellationToken);
    }

    private static string ResolvePlaceholders(string templateText, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(templateText)) return string.Empty;

        var result = templateText;
        foreach (var (key, val) in values)
        {
            result = result.Replace($"{{{key}}}", val, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    private static (int xMul, int yMul) FontMultiplication(int fontSize)
    {
        if (fontSize <= 10) return (1, 1);
        if (fontSize <= 16) return (1, 2);
        if (fontSize <= 24) return (2, 2);
        if (fontSize <= 36) return (2, 3);
        return (3, 3);
    }

    private static string NormalizeBarcodeType(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "128";
        var lower = rawType.Trim().ToLowerInvariant();
        if (lower.Contains("39")) return "39";
        if (lower.Contains("93")) return "93";
        if (lower.Contains("ean") || lower.Contains("13")) return "EAN13";
        return "128";
    }
}
