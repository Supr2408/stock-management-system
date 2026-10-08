using BoxTrack.Application;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class ReportCatalogService(BoxTrackDbContext db) : IReportCatalog
{
    public async Task<IReadOnlyList<DailyPrintReportRow>> DailyPrintAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var labels = await db.BarcodeLabels.AsNoTracking().Include(label => label.Item).ThenInclude(item => item!.Department)
            .Where(label => label.ManufactureDate == date && label.ProductionStatus != "Dispatched").ToListAsync(cancellationToken);
        return labels.GroupBy(label => new { label.ItemCode, ItemName = label.Item!.Name, DepartmentName = label.Item.Department!.Name, label.ManufactureDate })
            .OrderBy(group => group.Key.DepartmentName).ThenBy(group => group.Key.ItemCode)
            .Select(group => new DailyPrintReportRow(group.Key.ItemCode, group.Key.ItemName, group.Key.DepartmentName, group.Key.ManufactureDate, group.Count(), group.Min(label => label.BarcodeValue) ?? string.Empty, group.Max(label => label.BarcodeValue) ?? string.Empty)).ToList();
    }

    public async Task<IReadOnlyList<StockSummaryReportRow>> StockSummaryAsync(int? departmentId, int? itemId, CancellationToken cancellationToken)
    {
        var query = db.BarcodeLabels.AsNoTracking().Include(label => label.Item).ThenInclude(item => item!.Department)
            .Where(label => label.ProductionStatus == "AddedToStock");
        if (departmentId.HasValue) query = query.Where(label => label.Item!.DepartmentId == departmentId.Value);
        if (itemId.HasValue) query = query.Where(label => label.ItemId == itemId.Value);
        var labels = await query.ToListAsync(cancellationToken);
        return labels.GroupBy(label => new { label.ItemCode, ItemName = label.Item!.Name, DepartmentName = label.Item.Department!.Name })
            .OrderBy(group => group.Key.DepartmentName).ThenBy(group => group.Key.ItemName)
            .Select(group => new StockSummaryReportRow(group.Key.ItemCode, group.Key.ItemName, group.Key.DepartmentName, group.Count())).ToList();
    }

    public async Task<IReadOnlyList<StockReportRow>> StockAsync(int? departmentId, CancellationToken cancellationToken)
    {
        var rows = await StockSummaryAsync(departmentId, null, cancellationToken);
        return rows.Select(row => new StockReportRow(row.ItemCode, row.ItemName, row.DepartmentName, row.TotalStockQuantity)).ToList();
    }

    public async Task<ItemDetailReportRow?> ItemDetailAsync(string barcodeOrSerial, CancellationToken cancellationToken)
    {
        var query = barcodeOrSerial.Trim();
        if (string.IsNullOrWhiteSpace(query)) return null;
        var label = await db.BarcodeLabels.AsNoTracking().Include(value => value.Item).ThenInclude(item => item!.Department).Include(value => value.StockReceipt).Include(value => value.DispatchRecord).ThenInclude(record => record!.Customer)
            .Where(value => value.BarcodeValue == query || value.BarcodeValue.EndsWith(query)).OrderByDescending(value => value.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return label is null ? null : new ItemDetailReportRow(label.BarcodeValue, label.ItemCode, label.Item!.Name, label.Item.Department!.Name, label.ManufactureDate, label.SerialNumber, label.ProductionStatus, label.StockReceipt?.BatchNumber, label.DispatchRecord?.Customer?.Name, label.DispatchRecord?.SalesOrderNumber, label.DispatchRecord?.InvoiceNumber, label.DispatchRecord?.DispatchDate);
    }

    public async Task<IReadOnlyList<BatchReportRow>> BatchAsync(string batchNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(batchNumber)) return [];
        var labels = await db.BarcodeLabels.AsNoTracking().Include(label => label.Item).Include(label => label.StockReceipt).Include(label => label.DispatchRecord).ThenInclude(record => record!.Customer)
            .Where(label => label.StockReceipt != null && EF.Functions.ILike(label.StockReceipt.BatchNumber, $"%{batchNumber.Trim()}%"))
            .OrderBy(label => label.BarcodeValue).ToListAsync(cancellationToken);
        return labels.Select(label => new BatchReportRow(label.StockReceipt!.BatchNumber, label.BarcodeValue, label.Item!.Name, label.DispatchRecord?.SalesOrderNumber, label.DispatchRecord?.DispatchDate, label.DispatchRecord?.Customer?.Name)).ToList();
    }

    public async Task<CustomerReportResult?> CustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(value => value.Id == customerId, cancellationToken);
        if (customer is null) return null;
        var labels = await db.BarcodeLabels.AsNoTracking().Include(label => label.Item).Include(label => label.StockReceipt).Include(label => label.DispatchRecord)
            .Where(label => label.DispatchRecord != null && label.DispatchRecord.CustomerId == customerId).ToListAsync(cancellationToken);
        var rows = labels.GroupBy(label => new { label.DispatchRecord!.SalesOrderNumber, label.DispatchRecord.InvoiceNumber, label.DispatchRecord.DispatchDate, BatchNumber = label.StockReceipt?.BatchNumber ?? "-", ItemName = label.Item!.Name })
            .OrderByDescending(group => group.Key.DispatchDate).ThenBy(group => group.Key.SalesOrderNumber)
            .Select(group => new CustomerReportRow(group.Key.SalesOrderNumber, group.Key.InvoiceNumber, group.Key.DispatchDate, group.Key.BatchNumber, group.Key.ItemName, group.Count())).ToList();
        return new CustomerReportResult(new DispatchCustomerRow(customer.Name, customer.Address1, customer.Address2, customer.City, customer.Pincode, customer.State, customer.Country), rows);
    }

    public async Task<IReadOnlyList<InventorySummaryReportRow>> BatchWiseAsync(string? batchNumber, CancellationToken cancellationToken)
    {
        var query = db.BarcodeLabels.AsNoTracking().Include(label => label.Item).ThenInclude(item => item!.Department).Include(label => label.StockReceipt).Include(label => label.DispatchRecord).ThenInclude(record => record!.Customer).Where(label => label.StockReceipt != null);
        if (!string.IsNullOrWhiteSpace(batchNumber)) query = query.Where(label => label.StockReceipt!.BatchNumber == batchNumber.Trim());
        var labels = await query.ToListAsync(cancellationToken);
        return labels.GroupBy(label => new { label.Item!.Name, Department = label.Item.Department!.Name, Customer = label.DispatchRecord?.Customer?.Name ?? "Not dispatched", Batch = label.StockReceipt!.BatchNumber }).OrderBy(group => group.Key.Batch).ThenBy(group => group.Key.Customer).Select(group => new InventorySummaryReportRow(group.Key.Name, group.Key.Department, group.Key.Customer, group.Key.Batch, group.Count())).ToList();
    }

    public async Task<IReadOnlyList<InventorySummaryReportRow>> ProductWiseAsync(int? itemId, CancellationToken cancellationToken)
    {
        var query = db.BarcodeLabels.AsNoTracking().Include(label => label.Item).ThenInclude(item => item!.Department).Include(label => label.StockReceipt).Include(label => label.DispatchRecord).ThenInclude(record => record!.Customer).Where(label => label.StockReceipt != null);
        if (itemId.HasValue) query = query.Where(label => label.ItemId == itemId.Value);
        var labels = await query.ToListAsync(cancellationToken);
        return labels.GroupBy(label => new { label.Item!.Name, Department = label.Item.Department!.Name, Customer = label.DispatchRecord?.Customer?.Name ?? "Not dispatched", Batch = label.StockReceipt!.BatchNumber }).OrderBy(group => group.Key.Name).ThenBy(group => group.Key.Batch).ThenBy(group => group.Key.Customer).Select(group => new InventorySummaryReportRow(group.Key.Name, group.Key.Department, group.Key.Customer, group.Key.Batch, group.Count())).ToList();
    }

    public async Task<IReadOnlyList<SalesOrderSummaryReportRow>> SalesOrderSummaryAsync(string salesOrderNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(salesOrderNumber)) return [];
        var labels = await db.BarcodeLabels.AsNoTracking().Include(label => label.Item).Include(label => label.DispatchRecord).Where(label => label.DispatchRecord != null && EF.Functions.ILike(label.DispatchRecord.SalesOrderNumber, $"%{salesOrderNumber.Trim()}%")).ToListAsync(cancellationToken);
        return labels.GroupBy(label => new { label.DispatchRecord!.SalesOrderNumber, ItemName = label.Item!.Name }).OrderBy(group => group.Key.ItemName).Select(group => new SalesOrderSummaryReportRow(group.Key.SalesOrderNumber, group.Key.ItemName, group.Count())).ToList();
    }

    public async Task<IReadOnlyList<BoxDetailReportRow>> BoxDetailsAsync(int? departmentId, int? itemId, string? fromBarcode, string? toBarcode, CancellationToken cancellationToken)
    {
        var query = db.BarcodeLabels.AsNoTracking().Include(label => label.Item).ThenInclude(item => item!.Department).Include(label => label.StockReceipt).Include(label => label.DispatchRecord).ThenInclude(record => record!.Customer).AsQueryable();
        if (departmentId.HasValue) query = query.Where(label => label.Item!.DepartmentId == departmentId.Value);
        if (itemId.HasValue) query = query.Where(label => label.ItemId == itemId.Value);
        if (!string.IsNullOrWhiteSpace(fromBarcode)) query = query.Where(label => string.Compare(label.BarcodeValue, fromBarcode.Trim()) >= 0);
        if (!string.IsNullOrWhiteSpace(toBarcode)) query = query.Where(label => string.Compare(label.BarcodeValue, toBarcode.Trim()) <= 0);
        return (await query.OrderBy(label => label.BarcodeValue).ToListAsync(cancellationToken)).Select(label => new BoxDetailReportRow(label.Item!.Name, label.StockReceipt?.BatchNumber ?? "-", label.BarcodeValue, label.ProductionStatus, label.DispatchRecord?.SalesOrderNumber, label.DispatchRecord?.Customer?.Name ?? "Not dispatched")).ToList();
    }

    public async Task<IReadOnlyList<string>> ListBatchesAsync(CancellationToken cancellationToken)
    {
        var fromLabels = await db.BarcodeLabels.AsNoTracking()
            .Where(label => label.StockReceipt != null)
            .Select(label => label.StockReceipt!.BatchNumber)
            .Distinct()
            .ToListAsync(cancellationToken);
        var fromReceipts = await db.StockReceipts.AsNoTracking()
            .OrderByDescending(receipt => receipt.ReceivedAt)
            .Select(receipt => receipt.BatchNumber)
            .Distinct()
            .ToListAsync(cancellationToken);
        return fromReceipts.Union(fromLabels).Distinct().ToList();
    }
}
