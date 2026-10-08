namespace BoxTrack.Domain;

public abstract class AuditedEntity
{
    public int Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Department : AuditedEntity
{
    public required string Name { get; set; }
    public List<Item> Items { get; set; } = [];
}

public sealed class Item : AuditedEntity
{
    public int Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int PackagingPerBox { get; set; }
    public decimal GrossWeightKg { get; set; }
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public List<BarcodeLabel> BarcodeLabels { get; set; } = [];
    public List<StockReceipt> StockReceipts { get; set; } = [];
}

public sealed class Customer : AuditedEntity
{
    public int? LegacyId { get; set; }
    public required string Name { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public List<DispatchRecord> Dispatches { get; set; } = [];
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UserId { get; set; }
    public required string EntityName { get; set; }
    public required string Action { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}

public sealed class LabelLogo : AuditedEntity
{
    public required string Name { get; set; }
    public required string FileName { get; set; }
    public required string RelativePath { get; set; }
    public required string ContentType { get; set; }
    public List<BarcodeLabel> BarcodeLabels { get; set; } = [];
}

public sealed class BarcodeLabel
{
    public long Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public int ItemCode { get; set; }
    public DateOnly ManufactureDate { get; set; }
    public int SerialNumber { get; set; }
    public required string BarcodeValue { get; set; }
    public string LogoMode { get; set; } = "Nagreeka";
    public int? LabelLogoId { get; set; }
    public LabelLogo? LabelLogo { get; set; }
    public required string FileName { get; set; }
    public required string RelativePath { get; set; }
    public string ProductionStatus { get; set; } = "PendingProduction";
    public StockReceipt? StockReceipt { get; set; }
    public long? DispatchRecordId { get; set; }
    public DispatchRecord? DispatchRecord { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
}

public sealed class StockReceipt
{
    public long Id { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public required string BatchNumber { get; set; }
    public required string FromBarcode { get; set; }
    public required string ToBarcode { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public List<BarcodeLabel> BarcodeLabels { get; set; } = [];
}

public sealed class DispatchRecord
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public required string SalesOrderNumber { get; set; }
    public required string InvoiceNumber { get; set; }
    public DateOnly DispatchDate { get; set; }
    public required string SourceFileName { get; set; }
    public int LabelCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public List<BarcodeLabel> BarcodeLabels { get; set; } = [];
}

public sealed class UserAccount : AuditedEntity
{
    public required string Username { get; set; }
    public required string Role { get; set; } // "Admin", "Production", "QC"
    public string? PasswordHash { get; set; }
    public string? TemporaryDevPassword { get; set; } // DEVELOPMENT ONLY - MUST BE REMOVED BEFORE PRODUCTION
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
}

