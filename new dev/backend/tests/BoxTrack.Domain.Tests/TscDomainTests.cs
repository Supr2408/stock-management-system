using BoxTrack.Domain;

namespace BoxTrack.Domain.Tests;

public sealed class TscDomainTests
{
    [Fact]
    public void LabelTemplate_InitializesWithCorrectDefaults()
    {
        var template = new LabelTemplate
        {
            Name = "4x2 Standard Shipping",
            PrinterType = BarcodePrinterMode.Tsc,
            WidthMm = 101.6,
            HeightMm = 50.8,
            GapMm = 3.0,
            Orientation = 0,
            IsDefault = true,
            CreatedBy = "admin"
        };

        Assert.Equal("4x2 Standard Shipping", template.Name);
        Assert.Equal(BarcodePrinterMode.Tsc, template.PrinterType);
        Assert.Equal(101.6, template.WidthMm);
        Assert.Equal(50.8, template.HeightMm);
        Assert.Equal(3.0, template.GapMm);
        Assert.Equal(0, template.Orientation);
        Assert.True(template.IsDefault);
        Assert.Equal("admin", template.CreatedBy);
        Assert.NotNull(template.Elements);
        Assert.Empty(template.Elements);
    }

    [Fact]
    public void LabelTemplateElement_InitializesWithCorrectProperties()
    {
        var element = new LabelTemplateElement
        {
            ElementType = TemplateElementType.Barcode,
            Xmm = 10.5,
            Ymm = 20.0,
            WidthMm = 50.0,
            HeightMm = 15.0,
            Content = "{Barcode}",
            BarcodeType = "128",
            HumanReadable = true,
            Rotation = 0,
            ZIndex = 2
        };

        Assert.Equal(TemplateElementType.Barcode, element.ElementType);
        Assert.Equal(10.5, element.Xmm);
        Assert.Equal(20.0, element.Ymm);
        Assert.Equal(50.0, element.WidthMm);
        Assert.Equal(15.0, element.HeightMm);
        Assert.Equal("{Barcode}", element.Content);
        Assert.Equal("128", element.BarcodeType);
        Assert.True(element.HumanReadable);
    }

    [Fact]
    public void PrinterConfiguration_SupportsTscModeAndActiveTemplate()
    {
        var config = new PrinterConfiguration
        {
            Category = PrinterCategory.Barcode,
            PrinterName = "TSC TTP-247",
            Mode = BarcodePrinterMode.Tsc,
            Model = "TSC TTP-247",
            Dpi = 203,
            ActiveTemplateId = 42
        };

        Assert.Equal(BarcodePrinterMode.Tsc, config.Mode);
        Assert.Equal("TSC TTP-247", config.Model);
        Assert.Equal(203, config.Dpi);
        Assert.Equal(42, config.ActiveTemplateId);
    }
}
