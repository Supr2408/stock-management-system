using BoxTrack.Application;
using BoxTrack.Domain;
using BoxTrack.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BoxTrack.Application.Tests;

public class LaserA4LayoutTests
{
    private readonly LaserA4LabelCompositor _compositor = new(
        barcodeRoot: Path.GetTempPath(),
        logger: NullLogger<LaserA4LabelCompositor>.Instance);

    [Fact]
    public void CalculateLayout_StandardA4And50x30Label_ReturnsExpectedGrid()
    {
        // 210 x 297 mm paper, 10 mm margins, 2 mm gap
        // Usable width = 210 - 20 = 190 mm
        // Cols = (190 + 2) / (50 + 2) = 192 / 52 = 3
        // Usable height = 297 - 20 = 277 mm
        // Rows = (277 + 2) / (30 + 2) = 279 / 32 = 8
        // Total per page = 3 * 8 = 24
        var template = new LabelTemplate
        {
            Name = "50x30 Label",
            WidthMm = 50,
            HeightMm = 30
        };

        var config = new PrinterConfiguration
        {
            Category = PrinterCategory.Barcode,
            PrinterName = "HP LaserJet",
            PaperSize = "A4",
            PaperWidthMm = 210,
            PaperHeightMm = 297,
            MarginLeftMm = 10,
            MarginRightMm = 10,
            MarginTopMm = 10,
            MarginBottomMm = 10,
            HorizontalGapMm = 2,
            VerticalGapMm = 2
        };

        var layout = _compositor.CalculateLayout(template, config);

        Assert.Equal(190, layout.UsableWidthMm);
        Assert.Equal(277, layout.UsableHeightMm);
        Assert.Equal(3, layout.Columns);
        Assert.Equal(8, layout.Rows);
        Assert.Equal(24, layout.LabelsPerPage);
    }

    [Fact]
    public void CalculateLayout_LabelCoordinates_MatchOffsetMath()
    {
        var template = new LabelTemplate
        {
            Name = "60x40 Label",
            WidthMm = 60,
            HeightMm = 40
        };

        var config = new PrinterConfiguration
        {
            Category = PrinterCategory.Barcode,
            PrinterName = "HP LaserJet",
            PaperSize = "A4",
            PaperWidthMm = 210,
            PaperHeightMm = 297,
            MarginLeftMm = 10,
            MarginRightMm = 10,
            MarginTopMm = 15,
            MarginBottomMm = 15,
            HorizontalGapMm = 3,
            VerticalGapMm = 4
        };

        var layout = _compositor.CalculateLayout(template, config);

        // Column 0, Row 0
        var (x0, y0) = (layout.MarginLeftMm + 0 * (template.WidthMm + layout.HorizontalGapMm),
                        layout.MarginTopMm + 0 * (template.HeightMm + layout.VerticalGapMm));
        Assert.Equal(10, x0);
        Assert.Equal(15, y0);

        // Column 1, Row 0: X = 10 + 1 * (60 + 3) = 73
        var (x1, y1) = (layout.MarginLeftMm + 1 * (template.WidthMm + layout.HorizontalGapMm),
                        layout.MarginTopMm + 0 * (template.HeightMm + layout.VerticalGapMm));
        Assert.Equal(73, x1);
        Assert.Equal(15, y1);

        // Column 0, Row 2: Y = 15 + 2 * (40 + 4) = 15 + 88 = 103
        var (x2, y2) = (layout.MarginLeftMm + 0 * (template.WidthMm + layout.HorizontalGapMm),
                        layout.MarginTopMm + 2 * (template.HeightMm + layout.VerticalGapMm));
        Assert.Equal(10, x2);
        Assert.Equal(103, y2);
    }

    [Fact]
    public void CalculatePages_MultiPageLabels_DistributesAcrossPagesCorrectly()
    {
        var template = new LabelTemplate
        {
            Name = "50x30 Label",
            WidthMm = 50,
            HeightMm = 30
        };

        var config = new PrinterConfiguration
        {
            Category = PrinterCategory.Barcode,
            PrinterName = "HP LaserJet",
            PaperSize = "A4",
            PaperWidthMm = 210,
            PaperHeightMm = 297,
            MarginLeftMm = 10,
            MarginRightMm = 10,
            MarginTopMm = 10,
            MarginBottomMm = 10,
            HorizontalGapMm = 2,
            VerticalGapMm = 2
        };

        var layout = _compositor.CalculateLayout(template, config);
        Assert.Equal(24, layout.LabelsPerPage);

        // 50 labels on 24 labels/page -> Page 0: 24, Page 1: 24, Page 2: 2 -> 3 pages total
        int totalLabels = 50;
        int pageCount = (int)Math.Ceiling((double)totalLabels / layout.LabelsPerPage);
        Assert.Equal(3, pageCount);

        // Slot for label index 25 -> Page 1, index within page = 1 -> col 1, row 0
        int labelIndex = 25;
        int pageIndex = labelIndex / layout.LabelsPerPage;
        int slotIndexInPage = labelIndex % layout.LabelsPerPage;
        int col = slotIndexInPage % layout.Columns;
        int row = slotIndexInPage / layout.Columns;

        Assert.Equal(1, pageIndex);
        Assert.Equal(1, slotIndexInPage);
        Assert.Equal(1, col);
        Assert.Equal(0, row);
    }

    [Fact]
    public void CalculateLayout_ZeroOrOversizedMargins_ClampsGracefully()
    {
        var template = new LabelTemplate
        {
            Name = "50x50 Label",
            WidthMm = 50,
            HeightMm = 50
        };

        var config = new PrinterConfiguration
        {
            Category = PrinterCategory.Barcode,
            PrinterName = "HP LaserJet",
            PaperSize = "Custom",
            PaperWidthMm = 100,
            PaperHeightMm = 100,
            MarginLeftMm = 60,
            MarginRightMm = 60, // Usable width <= 0
            MarginTopMm = 0,
            MarginBottomMm = 0,
            HorizontalGapMm = 0,
            VerticalGapMm = 0
        };

        var layout = _compositor.CalculateLayout(template, config);

        // Columns and rows should clamp to 0 if usable space is 0
        Assert.Equal(0, layout.Columns);
        Assert.Equal(0, layout.LabelsPerPage);
    }
}
