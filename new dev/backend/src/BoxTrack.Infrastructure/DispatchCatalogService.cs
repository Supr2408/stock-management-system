using System.Text.Json;
using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class DispatchCatalogService(BoxTrackDbContext db) : IDispatchCatalog
{
    public async Task<Page<DispatchHistoryRow>> ListAsync(string? salesOrderNumber, string? invoiceNumber, string? customer, DateOnly? dispatchDate, string? barcode, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var query = db.DispatchRecords.AsNoTracking()
            .Include(record => record.Customer)
            .Include(record => record.BarcodeLabels).ThenInclude(label => label.Item)
            .Include(record => record.BarcodeLabels).ThenInclude(label => label.StockReceipt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(salesOrderNumber))
        {
            var value = salesOrderNumber.Trim();
            query = query.Where(record => EF.Functions.ILike(record.SalesOrderNumber, $"%{value}%"));
        }
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var value = invoiceNumber.Trim();
            query = query.Where(record => EF.Functions.ILike(record.InvoiceNumber, $"%{value}%"));
        }
        if (!string.IsNullOrWhiteSpace(customer))
        {
            var value = customer.Trim();
            query = query.Where(record => record.Customer != null && EF.Functions.ILike(record.Customer.Name, $"%{value}%"));
        }
        if (dispatchDate.HasValue) query = query.Where(record => record.DispatchDate == dispatchDate.Value);
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var value = barcode.Trim();
            query = query.Where(record => record.BarcodeLabels.Any(label => EF.Functions.ILike(label.BarcodeValue, $"%{value}%")));
        }

        var total = await query.CountAsync(cancellationToken);
        var records = await query.OrderByDescending(record => record.DispatchDate).ThenByDescending(record => record.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var rows = records.Select(record => new DispatchHistoryRow(
            record.Id,
            record.SalesOrderNumber,
            record.InvoiceNumber,
            new DispatchCustomerRow(record.Customer?.Name ?? "Unknown customer", record.Customer?.Address1, record.Customer?.Address2, record.Customer?.City, record.Customer?.Pincode, record.Customer?.State, record.Customer?.Country),
            record.DispatchDate,
            record.SourceFileName,
            record.LabelCount,
            record.BarcodeLabels.OrderBy(label => label.BarcodeValue).Select(label => new DispatchLabelRow(label.BarcodeValue, label.ItemCode, label.Item?.Name ?? "Unknown item", label.StockReceipt?.BatchNumber ?? "-", label.Item?.PackagingPerBox ?? 0, label.Item?.GrossWeightKg ?? 0)).ToList()
        )).ToList();
        return new Page<DispatchHistoryRow>(rows, total, page, pageSize);
    }

    public async Task<DispatchResult> DispatchAsync(DispatchInput input, string? userId, CancellationToken cancellationToken)
    {
        if (input.CustomerId <= 0 || !await db.Customers.AnyAsync(customer => customer.Id == input.CustomerId && customer.IsActive, cancellationToken)) throw new InvalidOperationException("Select a valid active customer.");
        if (string.IsNullOrWhiteSpace(input.SalesOrderNumber)) throw new InvalidOperationException("Sales order number is required.");
        if (string.IsNullOrWhiteSpace(input.InvoiceNumber)) throw new InvalidOperationException("Invoice number is required.");
        if (input.DispatchDate == default) throw new InvalidOperationException("Dispatch date is required.");
        if (input.Barcodes.Count == 0) throw new InvalidOperationException("The scanner file does not contain any barcode numbers.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var labels = await db.BarcodeLabels.Include(label => label.Item).Include(label => label.StockReceipt).Where(label => input.Barcodes.Contains(label.BarcodeValue)).ToListAsync(cancellationToken);
        if (labels.Count != input.Barcodes.Count)
        {
            var found = labels.Select(label => label.BarcodeValue).ToHashSet();
            var missing = input.Barcodes.First(value => !found.Contains(value));
            throw new InvalidOperationException($"Barcode {missing} was not found.");
        }
        var unavailable = labels.FirstOrDefault(label => label.ProductionStatus != "AddedToStock");
        if (unavailable is not null) throw new InvalidOperationException($"Barcode {unavailable.BarcodeValue} is not available in stock for dispatch.");

        var customer = await db.Customers.SingleAsync(value => value.Id == input.CustomerId, cancellationToken);
        var record = new DispatchRecord { CustomerId = customer.Id, SalesOrderNumber = input.SalesOrderNumber.Trim(), InvoiceNumber = input.InvoiceNumber.Trim(), DispatchDate = input.DispatchDate, SourceFileName = input.SourceFileName, LabelCount = labels.Count, CreatedBy = userId };
        db.DispatchRecords.Add(record);
        foreach (var label in labels) { label.ProductionStatus = "Dispatched"; label.DispatchRecord = record; }
        db.AuditLogs.Add(new AuditLog { UserId = userId, EntityName = "DispatchRecord", Action = "Dispatched", AfterJson = JsonSerializer.Serialize(new { record.SalesOrderNumber, record.InvoiceNumber, record.DispatchDate, record.CustomerId, record.LabelCount, Barcodes = input.Barcodes }) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var customerRow = new DispatchCustomerRow(customer.Name, customer.Address1, customer.Address2, customer.City, customer.Pincode, customer.State, customer.Country);
        return new DispatchResult(record.Id, labels.Count, record.SalesOrderNumber, record.InvoiceNumber, customerRow, record.DispatchDate, labels.OrderBy(label => label.BarcodeValue).Select(label => new DispatchLabelRow(label.BarcodeValue, label.ItemCode, label.Item!.Name, label.StockReceipt?.BatchNumber ?? "-", label.Item.PackagingPerBox, label.Item.GrossWeightKg)).ToList());
    }
}
