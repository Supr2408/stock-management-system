namespace BoxTrack.Application;

public sealed record DailyPrintReportRow(int ItemCode, string ItemName, string DepartmentName, DateOnly ManufactureDate, int TotalPrint, string StartingBarcode, string EndingBarcode);
public sealed record StockSummaryReportRow(int ItemCode, string ItemName, string DepartmentName, int TotalStockQuantity);
public sealed record StockReportRow(int ItemCode, string ItemName, string DepartmentName, int Quantity);
public sealed record ItemDetailReportRow(string BarcodeValue, int ItemCode, string ItemName, string DepartmentName, DateOnly ManufactureDate, int SerialNumber, string Status, string? BatchNumber, string? CustomerName, string? SalesOrderNumber, string? InvoiceNumber, DateOnly? DispatchDate);
public sealed record BatchReportRow(string BatchNumber, string BarcodeValue, string ItemName, string? SalesOrderNumber, DateOnly? DispatchDate, string? CustomerName);
public sealed record CustomerReportRow(string SalesOrderNumber, string InvoiceNumber, DateOnly DispatchDate, string BatchNumber, string ItemName, int Quantity);
public sealed record CustomerReportResult(DispatchCustomerRow Customer, IReadOnlyList<CustomerReportRow> Rows);
public sealed record InventorySummaryReportRow(string ItemName, string DepartmentName, string CustomerName, string BatchNumber, int TotalBoxQuantity);
public sealed record SalesOrderSummaryReportRow(string SalesOrderNumber, string ItemName, int Quantity);
public sealed record BoxDetailReportRow(string ItemName, string BatchNumber, string BarcodeValue, string StockStatus, string? SalesOrderNumber, string CustomerName);

public interface IReportCatalog
{
    Task<IReadOnlyList<DailyPrintReportRow>> DailyPrintAsync(DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockSummaryReportRow>> StockSummaryAsync(int? departmentId, int? itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockReportRow>> StockAsync(int? departmentId, CancellationToken cancellationToken);
    Task<ItemDetailReportRow?> ItemDetailAsync(string barcodeOrSerial, CancellationToken cancellationToken);
    Task<IReadOnlyList<BatchReportRow>> BatchAsync(string batchNumber, CancellationToken cancellationToken);
    Task<CustomerReportResult?> CustomerAsync(int customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventorySummaryReportRow>> BatchWiseAsync(string? batchNumber, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventorySummaryReportRow>> ProductWiseAsync(int? itemId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SalesOrderSummaryReportRow>> SalesOrderSummaryAsync(string salesOrderNumber, CancellationToken cancellationToken);
    Task<IReadOnlyList<BoxDetailReportRow>> BoxDetailsAsync(int? departmentId, int? itemId, string? fromBarcode, string? toBarcode, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListBatchesAsync(CancellationToken cancellationToken);
}
