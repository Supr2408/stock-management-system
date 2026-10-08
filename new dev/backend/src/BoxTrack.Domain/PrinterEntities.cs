namespace BoxTrack.Domain;

public enum PrinterCategory
{
    RegularDocument = 1,
    Barcode = 2
}

public enum BarcodePrinterMode
{
    Laser = 1,
    Tsc = 2
}

public enum PrintJobStatus
{
    Queued = 1,
    Printing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum TemplateElementType
{
    Logo = 1,
    Barcode = 2,
    Text = 3,
    Box = 4
}

public enum LogoFitMode
{
    Contain = 1,
    Cover = 2,
    Stretch = 3
}

public sealed class PrinterConfiguration : AuditedEntity
{
    public PrinterCategory Category { get; set; }
    public required string PrinterName { get; set; }
    public BarcodePrinterMode Mode { get; set; } = BarcodePrinterMode.Laser;
    public string Model { get; set; } = "TSC TTP-247";
    public int Dpi { get; set; } = 203;
    public int? ActiveTemplateId { get; set; }
    public LabelTemplate? ActiveTemplate { get; set; }

    // Laser A4 sheet layout configuration
    public string PaperSize { get; set; } = "A4";
    public double PaperWidthMm { get; set; } = 210.0;
    public double PaperHeightMm { get; set; } = 297.0;
    public double MarginLeftMm { get; set; } = 10.0;
    public double MarginRightMm { get; set; } = 10.0;
    public double MarginTopMm { get; set; } = 10.0;
    public double MarginBottomMm { get; set; } = 10.0;
    public double HorizontalGapMm { get; set; } = 2.0;
    public double VerticalGapMm { get; set; } = 2.0;
}

public sealed class LabelTemplate : AuditedEntity
{
    public required string Name { get; set; }
    public BarcodePrinterMode PrinterType { get; set; } = BarcodePrinterMode.Tsc;
    public double WidthMm { get; set; } = 100.0;
    public double HeightMm { get; set; } = 50.0;
    public double GapMm { get; set; } = 2.0;
    public int Orientation { get; set; } = 0; // 0 or 1
    public bool IsDefault { get; set; } = false;
    public List<LabelTemplateElement> Elements { get; set; } = [];
}

public sealed class LabelTemplateElement
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public LabelTemplate? Template { get; set; }
    public TemplateElementType ElementType { get; set; }
    public double Xmm { get; set; }
    public double Ymm { get; set; }
    public double WidthMm { get; set; }
    public double HeightMm { get; set; }
    public int Rotation { get; set; } = 0; // 0, 90, 180, 270
    public int ZIndex { get; set; } = 0;
    public string? Content { get; set; }
    public string? FontName { get; set; }
    public int FontSize { get; set; } = 12;
    public string? BarcodeType { get; set; } = "128"; // 128, 39, QR
    public bool HumanReadable { get; set; } = true;
    public LogoFitMode FitMode { get; set; } = LogoFitMode.Contain;
    public int? LogoId { get; set; }
    public LabelLogo? Logo { get; set; }
    public int BorderThicknessDots { get; set; } = 2;
}

public sealed class PrintJob
{
    public long Id { get; set; }
    public PrinterCategory Category { get; set; }
    public required string PrinterName { get; set; }
    public required string DocumentName { get; set; }
    public string? DocumentReference { get; set; }
    public int Copies { get; set; } = 1;
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Queued;
    public string? RequestedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}
