using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.Extensions.Logging;

namespace BoxTrack.Infrastructure;

public sealed class LaserA4LabelCompositor(
    string barcodeRoot,
    ILogger<LaserA4LabelCompositor> logger) : ILaserLabelCompositor
{
    // Standard A4 physical dimensions in mm
    public const double A4WidthMm = 210.0;
    public const double A4HeightMm = 297.0;

    // GDI+ PrintDocument uses hundredths of an inch (100 DPI) as display units by default
    // 1 inch = 25.4 mm. In GraphicsUnit.Display, 1 inch = 100 units.
    // Therefore, 1 mm = 100 / 25.4 units approx 3.93700787 units.
    public const float MmToUnits = 100.0f / 25.4f;

    public A4SheetLayout CalculateLayout(LabelTemplate template, PrinterConfiguration config)
    {
        var paperW = config.PaperWidthMm > 0 ? config.PaperWidthMm : A4WidthMm;
        var paperH = config.PaperHeightMm > 0 ? config.PaperHeightMm : A4HeightMm;
        var mLeft = config.MarginLeftMm > 0 ? config.MarginLeftMm : 5.0;
        var mRight = config.MarginRightMm > 0 ? config.MarginRightMm : 5.0;
        var mTop = config.MarginTopMm > 0 ? config.MarginTopMm : 10.0;
        var mBottom = config.MarginBottomMm > 0 ? config.MarginBottomMm : 10.0;
        var gapX = config.HorizontalGapMm >= 0 ? config.HorizontalGapMm : 0.0;
        var gapY = config.VerticalGapMm >= 0 ? config.VerticalGapMm : 2.0;

        var usableW = Math.Max(0, paperW - mLeft - mRight);
        var usableH = Math.Max(0, paperH - mTop - mBottom);

        if (usableW <= 0 || usableH <= 0)
        {
            return new A4SheetLayout(
                paperW, paperH, mLeft, mRight, mTop, mBottom,
                gapX, gapY, 0, 0, 0, usableW, usableH);
        }

        var labelW = Math.Max(1.0, template.WidthMm);
        var labelH = Math.Max(1.0, template.HeightMm);

        // Columns: floor((usableWidth + gapX) / (labelWidth + gapX))
        var cols = (int)Math.Floor((usableW + gapX) / (labelW + gapX));
        // If 2 columns can fit without gap, allow 2 columns (e.g. 2 x 100mm on 200mm usable width)
        if (cols < 2 && usableW >= labelW * 2)
        {
            cols = 2;
            gapX = Math.Max(0, (usableW - labelW * cols) / (cols - 1));
        }

        // Rows: floor((usableHeight + gapY) / (labelHeight + gapY))
        var rows = (int)Math.Floor((usableH + gapY) / (labelH + gapY));

        cols = Math.Max(0, cols);
        rows = Math.Max(0, rows);
        var perPage = cols * rows;

        return new A4SheetLayout(
            paperW, paperH, mLeft, mRight, mTop, mBottom,
            gapX, gapY, cols, rows, perPage, usableW, usableH);
    }

    public Task<byte[]> GenerateA4PdfDocumentAsync(
        LabelTemplate template,
        IReadOnlyList<IReadOnlyDictionary<string, string>> labelsData,
        PrinterConfiguration config,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Array.Empty<byte>());
    }

    public Task<byte[]> GenerateTestPrintA4PdfAsync(
        LabelTemplate template,
        PrinterConfiguration config,
        string requestedBy = "Admin",
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Array.Empty<byte>());
    }

    [SupportedOSPlatform("windows")]
    public void RenderA4SheetPage(
        Graphics g,
        LabelTemplate template,
        IReadOnlyList<IReadOnlyDictionary<string, string>> labelsData,
        int pageIndex,
        PrinterConfiguration config)
    {
        // Explicitly enforce GraphicsUnit.Display (1/100 inch) for physically accurate scaling
        g.PageUnit = GraphicsUnit.Display;

        var layout = CalculateLayout(template, config);
        if (layout.LabelsPerPage <= 0) return;

        int startIndex = pageIndex * layout.LabelsPerPage;
        int endIndex = Math.Min(labelsData.Count, startIndex + layout.LabelsPerPage);

        float startXMm = (float)layout.MarginLeftMm;
        float startYMm = (float)layout.MarginTopMm;
        float labelWMm = (float)template.WidthMm;
        float labelHMm = (float)template.HeightMm;
        float gapXMm = (float)layout.HorizontalGapMm;
        float gapYMm = (float)layout.VerticalGapMm;

        for (int i = startIndex; i < endIndex; i++)
        {
            int slot = i - startIndex;
            int col = slot % layout.Columns;
            int row = slot / layout.Columns;

            float xMm = startXMm + col * (labelWMm + gapXMm);
            float yMm = startYMm + row * (labelHMm + gapYMm);

            float xUnits = xMm * MmToUnits;
            float yUnits = yMm * MmToUnits;
            float wUnits = labelWMm * MmToUnits;
            float hUnits = labelHMm * MmToUnits;

            RenderSingleLabel(g, template, labelsData[i], xUnits, yUnits, wUnits, hUnits);
        }
    }

    [SupportedOSPlatform("windows")]
    private void RenderSingleLabel(
        Graphics g,
        LabelTemplate template,
        IReadOnlyDictionary<string, string> values,
        float originX,
        float originY,
        float widthUnits,
        float heightUnits)
    {
        var state = g.Save();
        try
        {
            // Clip to physical label boundaries
            g.SetClip(new RectangleF(originX, originY, widthUnits, heightUnits), CombineMode.Intersect);

            // Light dashed border indicator for peel/cut boundary
            using var borderPen = new Pen(Color.FromArgb(210, 210, 210), 1f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(borderPen, originX, originY, widthUnits, heightUnits);

            var sortedElements = template.Elements.OrderBy(e => e.ZIndex).ThenBy(e => e.Id).ToList();

            foreach (var el in sortedElements)
            {
                float elXUnits = originX + (float)el.Xmm * MmToUnits;
                float elYUnits = originY + (float)el.Ymm * MmToUnits;
                float elWUnits = (float)el.WidthMm * MmToUnits;
                float elHUnits = (float)el.HeightMm * MmToUnits;

                switch (el.ElementType)
                {
                    case TemplateElementType.Box:
                        // Border thickness: in TSPL 8 dots = 1 mm
                        float penWidth = Math.Max(1f, (float)el.BorderThicknessDots * (1f / 8f) * MmToUnits);
                        using (var boxPen = new Pen(Color.Black, penWidth))
                        {
                            g.DrawRectangle(boxPen, elXUnits, elYUnits, elWUnits, elHUnits);
                        }
                        break;

                    case TemplateElementType.Text:
                        var rawText = ResolvePlaceholders(el.Content ?? string.Empty, values);
                        // Font size in points (1 pt = 1/72 inch). GDI+ automatically maps points to Display units
                        float ptSize = Math.Max(6f, (float)el.FontSize);
                        using (var font = new Font("Arial", ptSize, FontStyle.Bold))
                        {
                            var rect = new RectangleF(elXUnits, elYUnits, elWUnits, elHUnits);
                            using var sf = new StringFormat
                            {
                                Alignment = StringAlignment.Near,
                                LineAlignment = StringAlignment.Center,
                                Trimming = StringTrimming.EllipsisCharacter
                            };
                            g.DrawString(rawText, font, Brushes.Black, rect, sf);
                        }
                        break;

                    case TemplateElementType.Barcode:
                        var barcodeVal = ResolvePlaceholders(el.Content ?? "{Barcode}", values);
                        if (string.IsNullOrWhiteSpace(barcodeVal)) barcodeVal = "143081026000001";

                        RenderVectorBarcode(g, barcodeVal, elXUnits, elYUnits, elWUnits, elHUnits, el.HumanReadable);
                        break;

                    case TemplateElementType.Logo:
                        RenderLogoElement(g, el, values, elXUnits, elYUnits, elWUnits, elHUnits);
                        break;
                }
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RenderVectorBarcode(
        Graphics g,
        string barcodeText,
        float x,
        float y,
        float width,
        float height,
        bool humanReadable)
    {
        // Height for human-readable digits (approx 3.5mm)
        float textH = humanReadable ? Math.Max(12f, 3.5f * MmToUnits) : 0f;
        float barH = Math.Max(12f, height - textH);

        // Genuine Code 128 rendering using BarcodeSvgRenderer patterns
        try
        {
            var codes = BarcodeSvgRenderer.EncodeCode128B(barcodeText);
            int totalModules = codes.Sum(c => BarcodeSvgRenderer.Code128Patterns[c].Sum(ch => ch - '0'));
            float moduleW = width / Math.Max(1, totalModules);
            float currentX = x;

            using var brush = new SolidBrush(Color.Black);
            foreach (var code in codes)
            {
                var pattern = BarcodeSvgRenderer.Code128Patterns[code];
                for (int i = 0; i < pattern.Length; i++)
                {
                    int barWidthModules = pattern[i] - '0';
                    float barWidthPx = barWidthModules * moduleW;
                    if (i % 2 == 0) // Even index is a bar
                    {
                        g.FillRectangle(brush, currentX, y, barWidthPx, barH);
                    }
                    currentX += barWidthPx;
                }
            }
        }
        catch
        {
            // Fallback bars if encoding encounters illegal characters
            using var brush = new SolidBrush(Color.Black);
            int segments = Math.Max(20, barcodeText.Length * 4);
            float barW = width / segments;
            for (int s = 0; s < segments; s++)
            {
                if (s % 2 == 0) g.FillRectangle(brush, x + s * barW, y, barW * 0.8f, barH);
            }
        }

        if (humanReadable)
        {
            float ptSize = Math.Max(7f, 2.8f * (72f / 25.4f));
            using var font = new Font("Courier New", ptSize, FontStyle.Bold);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(barcodeText, font, Brushes.Black, new RectangleF(x, y + barH, width, textH), sf);
        }
    }

    [SupportedOSPlatform("windows")]
    private void RenderLogoElement(
        Graphics g,
        LabelTemplateElement el,
        IReadOnlyDictionary<string, string> values,
        float x,
        float y,
        float w,
        float h)
    {
        // 1. Check if there is an image file to draw (from Template element logo, content, or values)
        string? logoFileName = el.Logo?.FileName;
        if (string.IsNullOrWhiteSpace(logoFileName) && !string.IsNullOrWhiteSpace(el.Content))
        {
            var ext = Path.GetExtension(el.Content).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".svg")
            {
                logoFileName = el.Content;
            }
        }
        if (string.IsNullOrWhiteSpace(logoFileName) && values.TryGetValue("LogoFileName", out var lfn) && !string.IsNullOrWhiteSpace(lfn))
        {
            logoFileName = lfn;
        }

        if (!string.IsNullOrWhiteSpace(logoFileName))
        {
            var cleanFileName = Path.GetFileName(logoFileName);
            var fullPath = Path.Combine(barcodeRoot, "logos", cleanFileName);
            if (!File.Exists(fullPath))
            {
                fullPath = Path.Combine(barcodeRoot, logoFileName.TrimStart('/', '\\'));
            }
            if (File.Exists(fullPath))
            {
                try
                {
                    using var original = Image.FromFile(fullPath);
                    var rect = CalculateImagePlacement(original.Width, original.Height, x, y, w, h, el.FitMode);
                    g.DrawImage(original, rect);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to render logo file {File} for laser compositor", logoFileName);
                }
            }
        }

        // 2. Check textual mode if no image file
        var logoMode = values.TryGetValue("LogoMode", out var lm) ? lm : el.Content;
        if (string.Equals(logoMode, "WithoutLogo", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Textual logo: "WithALUFO"
        if (string.Equals(logoMode, "WithALUFO", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(el.Content, "WithALUFO", StringComparison.OrdinalIgnoreCase))
        {
            float ptTitle = Math.Max(9f, h * 0.42f * (72f / 100f));
            float ptSub = Math.Max(5f, h * 0.20f * (72f / 100f));
            using var fontTitle = new Font("Arial", ptTitle, FontStyle.Bold);
            using var fontSub = new Font("Arial", ptSub, FontStyle.Regular);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("ALUFO", fontTitle, Brushes.Black, new RectangleF(x, y, w, h * 0.58f), sf);
            g.DrawString("NAGREEKA INDCON PRODUCTS (P) LTD", fontSub, Brushes.Black, new RectangleF(x, y + h * 0.58f, w, h * 0.42f), sf);
            return;
        }

        // Default textual logo: "Nagreeka"
        {
            float ptTitle = Math.Max(9f, h * 0.42f * (72f / 100f));
            float ptSub = Math.Max(5f, h * 0.20f * (72f / 100f));
            using var fontTitle = new Font("Georgia", ptTitle, FontStyle.Bold);
            using var fontSub = new Font("Arial", ptSub, FontStyle.Regular);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Nagreeka", fontTitle, Brushes.Black, new RectangleF(x, y, w, h * 0.58f), sf);
            g.DrawString("NAGREEKA INDCON PRODUCTS (P) LTD", fontSub, Brushes.Black, new RectangleF(x, y + h * 0.58f, w, h * 0.42f), sf);
        }
    }

    private static RectangleF CalculateImagePlacement(int imgW, int imgH, float targetX, float targetY, float targetW, float targetH, LogoFitMode fitMode)
    {
        if (imgW <= 0 || imgH <= 0 || targetW <= 0 || targetH <= 0)
        {
            return new RectangleF(targetX, targetY, targetW, targetH);
        }

        switch (fitMode)
        {
            case LogoFitMode.Stretch:
                return new RectangleF(targetX, targetY, targetW, targetH);

            case LogoFitMode.Cover:
                float scaleCover = Math.Max(targetW / imgW, targetH / imgH);
                float wCover = imgW * scaleCover;
                float hCover = imgH * scaleCover;
                return new RectangleF(targetX + (targetW - wCover) / 2f, targetY + (targetH - hCover) / 2f, wCover, hCover);

            case LogoFitMode.Contain:
            default:
                float scale = Math.Min(targetW / imgW, targetH / imgH);
                float w = imgW * scale;
                float h = imgH * scale;
                return new RectangleF(targetX + (targetW - w) / 2f, targetY + (targetH - h) / 2f, w, h);
        }
    }

    private static string ResolvePlaceholders(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var res = text;
        foreach (var kvp in values)
        {
            res = res.Replace($"{{{kvp.Key}}}", kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return res;
    }
}
