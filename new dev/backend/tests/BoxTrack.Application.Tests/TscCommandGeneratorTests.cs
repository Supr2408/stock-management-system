using System.Text;
using BoxTrack.Application;
using BoxTrack.Domain;
using BoxTrack.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace BoxTrack.Application.Tests;

public sealed class TscCommandGeneratorTests
{
    private sealed class FakeImageProcessor : IImageProcessor
    {
        public Task<ProcessedLogoGraphic> ProcessLogoForThermalPrintAsync(
            Stream imageStream,
            double targetWidthMm,
            double targetHeightMm,
            LogoFitMode fitMode,
            int dpi = 203,
            CancellationToken cancellationToken = default)
        {
            // Simple 8x8 dummy bitmap (1 byte per row, 8 rows = 8 bytes)
            var bytes = new byte[8];
            return Task.FromResult(new ProcessedLogoGraphic(8, 8, 1, bytes, "image/x-tsc-bitmap"));
        }

        public Task<(int Width, int Height)> GetImageDimensionsAsync(
            Stream imageStream,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((100, 100));
        }
    }

    [Fact]
    public void TscCommandBuilder_GeneratesStandardTsplPreamble()
    {
        var builder = new TscCommandBuilder();
        builder.SetSize(100, 50)
               .SetGap(2, 0)
               .SetDirection(0)
               .Clear()
               .AddBox(10, 10, 200, 100, 2)
               .Print(1, 1);

        var output = Encoding.ASCII.GetString(builder.BuildPayload());

        Assert.Contains("SIZE 100 mm, 50 mm\r\n", output);
        Assert.Contains("GAP 2 mm, 0 mm\r\n", output);
        Assert.Contains("DIRECTION 0\r\n", output);
        Assert.Contains("CLS\r\n", output);
        Assert.Contains("BOX 10,10,200,100,2\r\n", output);
        Assert.Contains("PRINT 1,1\r\n", output);
    }

    [Fact]
    public void MmToDots_ConvertsAt203DpiCorrectly()
    {
        // 1 mm at 203 dpi is approximately 8 dots
        // 25.4 mm is exactly 203 dots
        Assert.Equal(8, TscTsplCommandGenerator.MmToDots(1.0));
        Assert.Equal(203, TscTsplCommandGenerator.MmToDots(25.4));
        Assert.Equal(80, TscTsplCommandGenerator.MmToDots(10.0));
        // 100 * (203 / 25.4) = 799.21259... => rounds to 799
        Assert.Equal(799, TscTsplCommandGenerator.MmToDots(100.0));
    }

    [Fact]
    public async Task TscTsplCommandGenerator_ConvertsMillimetersToDotsAt203Dpi()
    {
        var generator = new TscTsplCommandGenerator(
            new FakeImageProcessor(),
            barcodeRoot: Path.GetTempPath(),
            NullLogger<TscTsplCommandGenerator>.Instance);

        var template = new LabelTemplate
        {
            Id = 1,
            Name = "TSC 100x50",
            WidthMm = 100,
            HeightMm = 50,
            GapMm = 2,
            Orientation = 0,
            Elements = new List<LabelTemplateElement>
            {
                new()
                {
                    ElementType = TemplateElementType.Barcode,
                    Xmm = 10, // 10mm * 8 = 80 dots
                    Ymm = 20, // 20mm * 8 = 160 dots
                    WidthMm = 50,
                    HeightMm = 15, // 15mm * 8 = 120 dots
                    Content = "BOX-123456",
                    BarcodeType = "128",
                    HumanReadable = true
                },
                new()
                {
                    ElementType = TemplateElementType.Text,
                    Xmm = 5,  // 5mm * 8 = 40 dots
                    Ymm = 5,  // 5mm * 8 = 40 dots
                    WidthMm = 60,
                    HeightMm = 10,
                    Content = "Item: {ItemName}",
                    FontSize = 14
                }
            }
        };

        var dynamicValues = new Dictionary<string, string>
        {
            ["ItemName"] = "Steel Pipe Grade A",
            ["Barcode"] = "BOX-123456"
        };

        var result = await generator.GenerateLabelAsync(template, dynamicValues, copies: 2);
        var tspl = Encoding.ASCII.GetString(result.Payload);

        // Verify SIZE and GAP
        Assert.Contains("SIZE 100 mm, 50 mm\r\n", tspl);
        Assert.Contains("GAP 2 mm, 0 mm\r\n", tspl);

        // Verify Barcode command: X=80, Y=160, Height=120
        Assert.Contains("BARCODE 80,160,\"128\",120,1,0,2,4,\"BOX-123456\"\r\n", tspl);

        // Verify placeholder replacement for ItemName with font multiplier (1,2)
        Assert.Contains("TEXT 40,40,\"3\",0,1,2,\"Item: Steel Pipe Grade A\"\r\n", tspl);

        // Verify 2 copies
        Assert.Contains("PRINT 2,1\r\n", tspl);
    }

    [Fact]
    public async Task TscTsplCommandGenerator_GeneratesTestPrintCommands()
    {
        var generator = new TscTsplCommandGenerator(
            new FakeImageProcessor(),
            barcodeRoot: Path.GetTempPath(),
            NullLogger<TscTsplCommandGenerator>.Instance);

        var template = new LabelTemplate
        {
            Id = 2,
            Name = "Test Label",
            WidthMm = 80,
            HeightMm = 40,
            GapMm = 3,
            Elements = new List<LabelTemplateElement>
            {
                new()
                {
                    ElementType = TemplateElementType.Text,
                    Xmm = 5,
                    Ymm = 5,
                    WidthMm = 50,
                    HeightMm = 10,
                    Content = "TEST: {ItemName}",
                    FontSize = 12
                }
            }
        };

        var result = await generator.GenerateTestPrintAsync(template, requestedBy: "Tester");
        var tspl = Encoding.ASCII.GetString(result.Payload);

        Assert.Contains("SIZE 80 mm, 40 mm\r\n", tspl);
        Assert.Contains("GAP 3 mm, 0 mm\r\n", tspl);
        Assert.Contains("CLS\r\n", tspl);
        Assert.Contains("TEST: 450 ML PREMIUM TEST", tspl);
        Assert.Contains("PRINT 1,1\r\n", tspl);
    }
}
