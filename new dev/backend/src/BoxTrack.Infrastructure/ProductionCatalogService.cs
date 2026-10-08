using System.Text.Json;
using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class ProductionCatalogService(BoxTrackDbContext db, ICurrentUserService currentUser) : IProductionCatalog
{
    public async Task<Page<PendingProductionLabelRow>> ListPendingAsync(int? departmentId, int? manufactureYear, int? manufactureMonth, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        // Enforce department scoping for Production users (override whatever frontend passed)
        int? effectiveDeptId = departmentId;
        if (currentUser.IsProduction && currentUser.DepartmentId.HasValue)
        {
            effectiveDeptId = currentUser.DepartmentId.Value;
        }

        var query = db.BarcodeLabels.AsNoTracking().Include(label => label.Item).ThenInclude(item => item!.Department)
            .Where(label => label.ProductionStatus == "PendingProduction");
        if (effectiveDeptId.HasValue) query = query.Where(label => label.Item!.DepartmentId == effectiveDeptId.Value);
        if (manufactureYear.HasValue && manufactureMonth.HasValue)
            query = query.Where(label => label.ManufactureDate.Year == manufactureYear.Value && label.ManufactureDate.Month == manufactureMonth.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(label => label.BarcodeValue.Contains(value) || label.Item!.Name.ToLower().Contains(value));
        }
        var total = await query.CountAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var rows = await query.OrderBy(label => label.BarcodeValue).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(label => new PendingProductionLabelRow(label.Id, label.ItemId, label.ItemCode, label.Item!.Name, label.Item.Department!.Name, label.BarcodeValue, label.ManufactureDate, label.SerialNumber))
            .ToListAsync(cancellationToken);
        return new Page<PendingProductionLabelRow>(rows, total, page, pageSize);
    }

    public async Task<AddToStockResult> AddToStockAsync(AddToStockInput input, string? userId, CancellationToken cancellationToken)
    {
        var batchNumber = input.BatchNumber?.Trim();
        var fromBarcode = input.FromBarcode?.Trim();
        var toBarcode = input.ToBarcode?.Trim();
        if (string.IsNullOrWhiteSpace(batchNumber)) throw new InvalidOperationException("Batch number is required.");
        if (string.IsNullOrWhiteSpace(fromBarcode) || string.IsNullOrWhiteSpace(toBarcode)) throw new InvalidOperationException("Choose a start and end barcode.");
        if (string.CompareOrdinal(fromBarcode, toBarcode) > 0) throw new InvalidOperationException("Start barcode must be before or equal to end barcode.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var labels = await db.BarcodeLabels.Include(l => l.Item)
            .Where(label => label.BarcodeValue.CompareTo(fromBarcode) >= 0 && label.BarcodeValue.CompareTo(toBarcode) <= 0)
            .OrderBy(label => label.BarcodeValue).ToListAsync(cancellationToken);
        if (labels.Count == 0) throw new InvalidOperationException("No generated labels were found in this range.");
        if (labels.Any(label => label.ProductionStatus != "PendingProduction")) throw new InvalidOperationException("This range includes labels that were already added to stock.");
        if (labels.Select(label => label.ItemId).Distinct().Count() != 1) throw new InvalidOperationException("A stock range must belong to one item.");

        // Enforce department scoping for Production users
        if (currentUser.IsProduction && currentUser.DepartmentId.HasValue)
        {
            var itemDeptId = labels[0].Item?.DepartmentId;
            if (itemDeptId != currentUser.DepartmentId.Value)
            {
                throw new InvalidOperationException("You are only authorized to add stock for your assigned department.");
            }
        }

        var receipt = new StockReceipt { ItemId = labels[0].ItemId, BatchNumber = batchNumber, FromBarcode = fromBarcode, ToBarcode = toBarcode, Quantity = labels.Count, CreatedBy = userId };
        db.StockReceipts.Add(receipt);
        foreach (var label in labels)
        {
            label.ProductionStatus = "AddedToStock";
            label.StockReceipt = receipt;
        }
        db.AuditLogs.Add(new AuditLog { UserId = userId, EntityName = "StockReceipt", Action = "AddedToStock", AfterJson = JsonSerializer.Serialize(new { batchNumber, fromBarcode, toBarcode, quantity = labels.Count, itemId = receipt.ItemId }) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AddToStockResult(receipt.Id, receipt.BatchNumber, receipt.ItemId, receipt.Quantity, receipt.FromBarcode, receipt.ToBarcode);
    }

    public async Task<IReadOnlyList<string>> ListBatchesAsync(CancellationToken cancellationToken)
    {
        var labelQuery = db.BarcodeLabels.AsNoTracking()
            .Where(label => label.StockReceipt != null && label.StockReceipt!.BatchNumber != null);
        var receiptQuery = db.StockReceipts.AsNoTracking()
            .Where(receipt => receipt.BatchNumber != null);

        if (currentUser.IsProduction && currentUser.DepartmentId.HasValue)
        {
            var deptId = currentUser.DepartmentId.Value;
            labelQuery = labelQuery.Where(label => label.Item!.DepartmentId == deptId);
            receiptQuery = receiptQuery.Where(receipt => receipt.Item!.DepartmentId == deptId);
        }

        var fromLabels = await labelQuery
            .Select(label => label.StockReceipt!.BatchNumber!)
            .Distinct()
            .ToListAsync(cancellationToken);
        var fromReceipts = await receiptQuery
            .Select(receipt => receipt.BatchNumber!)
            .Distinct()
            .ToListAsync(cancellationToken);
        return fromReceipts.Union(fromLabels).Distinct().ToList();
    }
}
