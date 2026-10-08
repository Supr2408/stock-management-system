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

    // GDI+ print resolution in DPI for physical accuracy
    // 1 inch = 25.4 mm => dots = mm * (Dpi / 25.4)
    public const float CompositionDpi = 300.0f;
    public static float MmToPixels(double mm) => (float)(mm * (CompositionDpi / 25.4));

    public A4SheetLayout CalculateLayout(LabelTemplate template, PrinterConfiguration config)
    {
        var paperW = config.PaperWidthMm > 0 ? config.PaperWidthMm : A4WidthMm;
        var paperH = config.PaperHeightMm > 0 ? config.PaperHeightMm : A4HeightMm;
        var mLeft = config.MarginLeftMm >= 0 ? config.MarginLeftMm : 10.0;
        var mRight = config.MarginRightMm >= 0 ? config.MarginRightMm : 10.0;
        var mTop = config.MarginTopMm >= 0 ? config.MarginTopMm : 10.0;
        var mBottom = config.MarginBottomMm >= 0 ? config.MarginBottomMm : 10.0;
        var gapX = config.HorizontalGapMm >= 0 ? config.HorizontalGapMm : 2.0;
        var gapY = config.VerticalGapMm >= 0 ? config.VerticalGapMm : 2.0;

        var usableW = Math.Max(0, paperW - mLeft - mRight);
        var usableH = Math.Max(0, paperH - mTop - mBottom);

        var labelW = Math.Max(1.0, template.WidthMm);
        var labelH = Math.Max(1.0, template.HeightMm);

        // Columns: floor((usableWidth + gapX) / (labelWidth + gapX))
        var cols = (int)Math.Floor((usableW + gapX) / (labelW + gapX));
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
        // For physical GDI+ vector printing / PDF saving via Windows Spooler
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
        var layout = CalculateLayout(template, config);
        if (layout.LabelsPerPage <= 0) return;

        int startIndex = pageIndex * layout.LabelsPerPage;
        int endIndex = Math.Min(labelsData.Count, startIndex + layout.LabelsPerPage);

        float dpi = g.DpiX > 0 ? g.DpiX : 300.0f;
        float mmToDots = dpi / 25.4f;

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

            float xPx = xMm * mmToDots;
            float yPx = yMm * mmToDots;
            float wPx = labelWMm * mmToDots;
            float hPx = labelHMm * mmToDots;

            RenderSingleLabel(g, template, labelsData[i], xPx, yPx, wPx, hPx, mmToDots);
        }
    }

    [SupportedOSPlatform("windows")]
    private void RenderSingleLabel(
        Graphics g,
        LabelTemplate template,
        IReadOnlyDictionary<string, string> values,
        float originX,
        float originY,
        float widthPx,
        float heightPx,
        float mmToDots)
    {
        var state = g.Save();
        try
        {
            // Clip to physical label boundaries so nothing spills outside
            g.SetClip(new RectangleF(originX, originY, widthPx, heightPx), CombineMode.Intersect);

            // Light border indicator for label boundary
            using var borderPen = new Pen(Color.FromArgb(230, 230, 230), 1f);
            g.DrawRectangle(borderPen, originX, originY, widthPx, heightPx);

            var sortedElements = template.Elements.OrderBy(e => e.ZIndex).ThenBy(e => e.Id).ToList();

            foreach (var el in sortedElements)
            {
                float elXPx = originX + (float)el.Xmm * mmToDots;
                float elYPx = originY + (float)el.Ymm * mmToDots;
                float elWPx = (float)el.WidthMm * mmToDots;
                float elHPx = (float)el.HeightMm * mmToDots;

                switch (el.ElementType)
                {
                    case TemplateElementType.Box:
                        using (var boxPen = new Pen(Color.Black, Math.Max(1f, el.BorderThicknessDots * (mmToDots / 8f))))
                        {
                            g.DrawRectangle(boxPen, elXPx, elYPx, elWPx, elHPx);
                        }
                        break;

                    case TemplateElementType.Text:
                        var rawText = ResolvePlaceholders(el.Content ?? string.Empty, values);
                        float emSize = Math.Max(7f, el.FontSize * (mmToDots / 25.4f));
                        using (var font = new Font("Arial", emSize, FontStyle.Bold))
                        {
                            var rect = new RectangleF(elXPx, elYPx, elWPx, elHPx);
                            g.DrawString(rawText, font, Brushes.Black, rect);
                        }
                        break;

                    case TemplateElementType.Barcode:
                        var barcodeVal = ResolvePlaceholders(el.Content ?? "{Barcode}", values);
                        if (string.IsNullOrWhiteSpace(barcodeVal)) barcodeVal = "143081026000001";

                        // Render barcode stripes + human readable
                        RenderVectorBarcode(g, barcodeVal, elXPx, elYPx, elWPx, elHPx, el.HumanReadable, mmToDots);
                        break;

                    case TemplateElementType.Logo:
                        RenderLogoElement(g, el, elXPx, elYPx, elWPx, elHPx);
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
    private void RenderVectorBarcode(
        Graphics g,
        string barcodeText,
        float x,
        float y,
        float width,
        float height,
        bool humanReadable,
        float mmToDots)
    {
        float textH = humanReadable ? Math.Max(12f, 3.5f * mmToDots) : 0f;
        float barH = Math.Max(10f, height - textH);

        // Deterministic pseudo-bars representing Code 128
        using (var brush = new SolidBrush(Color.Black))
        {
            int segments = Math.Max(20, barcodeText.Length * 5);
            float barW = width / segments;

            for (int s = 0; s < segments; s++)
            {
                // Alternating pattern derived from characters
                bool draw = (s % 2 == 0) || ((s + (barcodeText[s % barcodeText.Length])) % 3 == 0);
                if (draw)
                {
                    g.FillRectangle(brush, x + s * barW, y, Math.Max(1f, barW * 0.85f), barH);
                }
            }
        }

        if (humanReadable)
        {
            using var font = new Font("Courier New", Math.Max(7f, 2.8f * mmToDots), FontStyle.Bold);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(barcodeText, font, Brushes.Black, new RectangleF(x, y + barH, width, textH), sf);
        }
    }

    [SupportedOSPlatform("windows")]
    private void RenderLogoElement(
        Graphics g,
        LabelTemplateElement el,
        float x,
        float y,
        float w,
        float h)
    {
        if (el.Logo == null && string.IsNullOrWhiteSpace(el.Content))
        {
            using var font = new Font("Arial", 9f, FontStyle.Bold);
            g.DrawString("[LOGO]", font, Brushes.Gray, x, y);
            return;
        }

        var fileName = el.Logo?.FileName ?? el.Content;
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var cleanFileName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(barcodeRoot, "logos", cleanFileName);
        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(barcodeRoot, fileName.TrimStart('/', '\\'));
        }
        if (!File.Exists(fullPath)) return;

        try
        {
            using var original = Image.FromFile(fullPath);
            var rect = CalculateImagePlacement(original.Width, original.Height, x, y, w, h, el.FitMode);
            g.DrawImage(original, rect);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to render logo file {File} for laser compositor", fileName);
        }
    }

    private static RectangleF CalculateImagePlacement(
        int imgW,
        int imgH,
        float targetX,
        float targetY,
        float targetW,
        float targetH,
        LogoFitMode fitMode)
    {
        if (fitMode == LogoFitMode.Stretch || imgW <= 0 || imgH <= 0)
        {
            return new RectangleF(targetX, targetY, targetW, targetH);
        }

        double ratioW = targetW / imgW;
        double ratioH = targetH / imgH;

        if (fitMode == LogoFitMode.Contain)
        {
            double scale = Math.Min(ratioW, ratioH);
            float finalW = (float)(imgW * scale);
            float finalH = (float)(imgH * scale);
            float offsetX = targetX + (targetW - finalW) / 2f;
            float offsetY = targetY + (targetH - finalH) / 2f;
            return new RectangleF(offsetX, offsetY, finalW, finalH);
        }
        else // Cover
        {
            double scale = Math.Max(ratioW, ratioH);
            float finalW = (float)(imgW * scale);
            float finalH = (float)(imgH * scale);
            float offsetX = targetX + (targetW - finalW) / 2f;
            float offsetY = targetY + (targetH - finalH) / 2f;
            return new RectangleF(offsetX, offsetY, finalW, finalH);
        }
    }

    private static string ResolvePlaceholders(string templateText, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(templateText)) return string.Empty;
        var result = templateText;
        foreach (var (k, v) in values)
        {
            result = result.Replace($"{{{k}}}", v, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
