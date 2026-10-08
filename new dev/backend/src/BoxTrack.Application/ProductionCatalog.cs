namespace BoxTrack.Application;

public sealed record PendingProductionLabelRow(long Id, int ItemId, int ItemCode, string ItemName, string DepartmentName, string BarcodeValue, DateOnly ManufactureDate, int SerialNumber);
public sealed record AddToStockInput(string BatchNumber, string FromBarcode, string ToBarcode);
public sealed record AddToStockResult(long StockReceiptId, string BatchNumber, int ItemId, int Quantity, string FromBarcode, string ToBarcode);

public interface IProductionCatalog
{
    Task<Page<PendingProductionLabelRow>> ListPendingAsync(int? departmentId, int? manufactureYear, int? manufactureMonth, string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<AddToStockResult> AddToStockAsync(AddToStockInput input, string? userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListBatchesAsync(CancellationToken cancellationToken);
}
