using System.Text.Json;
using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class ItemCatalogService(BoxTrackDbContext db) : IItemCatalog
{
    public async Task<Page<ItemRow>> ListAsync(string? search, int? departmentId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Items.AsNoTracking().Include(item => item.Department).Where(item => item.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(item => item.Name.ToLower().Contains(search.Trim().ToLower()));
        if (departmentId.HasValue) query = query.Where(item => item.DepartmentId == departmentId.Value);
        var total = await query.CountAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var records = await query.OrderBy(item => item.Code).Skip((page - 1) * pageSize).Take(pageSize).Select(item => new ItemRow(item.Id, item.Code, item.Name, item.Description, item.PackagingPerBox, item.GrossWeightKg, item.DepartmentId, item.Department!.Name)).ToListAsync(cancellationToken);
        return new Page<ItemRow>(records, total, page, pageSize);
    }

    public async Task<ItemRow> CreateAsync(ItemInput input, string? userId, CancellationToken cancellationToken)
    {
        Validate(input);
        var code = input.Code ?? (await db.Items.MaxAsync(item => (int?)item.Code, cancellationToken) ?? 100) + 1;
        if (code is < 101 or > 999) throw new InvalidOperationException("Item ID Code must be between 101 and 999.");
        if (await db.Items.AnyAsync(item => item.IsActive && item.Code == code, cancellationToken)) throw new InvalidOperationException($"Item ID Code {code} already exists.");
        var entity = new Item { Code = code, Name = input.Name.Trim(), Description = input.Description?.Trim(), PackagingPerBox = input.PackagingPerBox, GrossWeightKg = input.GrossWeightKg, DepartmentId = input.DepartmentId, CreatedBy = userId };
        db.Items.Add(entity);
        await SaveWithAuditAsync(entity, "Created", null, cancellationToken);
        return await ToRowAsync(entity.Id, cancellationToken);
    }

    public async Task<ItemRow> UpdateAsync(int id, ItemInput input, string? userId, CancellationToken cancellationToken)
    {
        Validate(input);
        var entity = await db.Items.SingleOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken) ?? throw new KeyNotFoundException("The requested item was not found.");
        var before = SerializeItem(entity);
        entity.Name = input.Name.Trim();
        entity.Description = input.Description?.Trim();
        entity.PackagingPerBox = input.PackagingPerBox;
        entity.GrossWeightKg = input.GrossWeightKg;
        entity.DepartmentId = input.DepartmentId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveWithAuditAsync(entity, "Updated", before, cancellationToken);
        return await ToRowAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, string? userId, CancellationToken cancellationToken)
    {
        var entity = await db.Items.SingleOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken) ?? throw new KeyNotFoundException("The requested item was not found.");
        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveWithAuditAsync(entity, "Deleted", SerializeItem(entity), cancellationToken);
    }

    public async Task<ItemImportResult> ImportAsync(IReadOnlyList<ItemImportRow> rows, string? userId, CancellationToken cancellationToken)
    {
        var issues = new List<ImportIssue>();
        var existingCodes = (await db.Items.Where(item => item.IsActive).Select(item => item.Code).ToListAsync(cancellationToken)).ToHashSet();
        var existingItems = await db.Items.AsNoTracking().Where(item => item.IsActive).Select(item => new ItemRow(item.Id, item.Code, item.Name, item.Description, item.PackagingPerBox, item.GrossWeightKg, item.DepartmentId, item.Department!.Name)).ToDictionaryAsync(item => item.Code, cancellationToken);
        var fileCodes = new HashSet<int>();
        var departments = await db.Departments.Where(department => department.IsActive).ToListAsync(cancellationToken);
        var additions = new List<Item>();
        var duplicates = new List<ItemDuplicateComparison>();
        foreach (var row in rows)
        {
            var codeText = row.Code?.ToString() ?? "blank";
            if (row.Code is null or < 101 or > 999) { issues.Add(new ImportIssue(row.SourceRow, "Item ID must be an integer between 101 and 999.", codeText)); continue; }
            if (!fileCodes.Add(row.Code.Value)) { issues.Add(new ImportIssue(row.SourceRow, "Duplicate Item ID appears earlier in this file.", codeText)); continue; }
            if (existingCodes.Contains(row.Code.Value)) { duplicates.Add(new ItemDuplicateComparison(row.SourceRow, existingItems[row.Code.Value], row)); issues.Add(new ImportIssue(row.SourceRow, "Item ID already exists in the database. Compare the records below and update it explicitly if needed.", codeText)); continue; }
            if (string.IsNullOrWhiteSpace(row.Name)) { issues.Add(new ImportIssue(row.SourceRow, "Item Name is required.", codeText)); continue; }
            if (row.PackagingPerBox is null or <= 0) { issues.Add(new ImportIssue(row.SourceRow, "Packing must be a whole number greater than zero.", codeText)); continue; }
            if (row.GrossWeightKg is null or <= 0) { issues.Add(new ImportIssue(row.SourceRow, "Gross Weight must be greater than zero.", codeText)); continue; }
            var department = row.DepartmentId.HasValue ? departments.FirstOrDefault(value => value.Id == row.DepartmentId.Value) : departments.FirstOrDefault(value => value.Name.Equals(row.Department?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (department is null) { issues.Add(new ImportIssue(row.SourceRow, "Department ID or Department name does not match an active department.", codeText)); continue; }
            additions.Add(new Item { Code = row.Code.Value, Name = row.Name.Trim(), Description = row.Description?.Trim(), PackagingPerBox = row.PackagingPerBox.Value, GrossWeightKg = row.GrossWeightKg.Value, DepartmentId = department.Id, CreatedBy = userId });
        }
        if (additions.Count > 0)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Items.AddRange(additions);
            foreach (var item in additions) db.AuditLogs.Add(new AuditLog { UserId = userId, EntityName = "Item", Action = "Imported", AfterJson = SerializeItem(item) });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return new ItemImportResult(additions.Count, issues, duplicates);
    }

    private async Task SaveWithAuditAsync(Item entity, string action, string? before, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog { UserId = entity.CreatedBy, EntityName = "Item", Action = action, BeforeJson = before, AfterJson = SerializeItem(entity) });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ItemRow> ToRowAsync(int id, CancellationToken cancellationToken) => await db.Items.AsNoTracking().Where(item => item.Id == id).Select(item => new ItemRow(item.Id, item.Code, item.Name, item.Description, item.PackagingPerBox, item.GrossWeightKg, item.DepartmentId, item.Department!.Name)).SingleAsync(cancellationToken);
    private static string SerializeItem(Item item) => JsonSerializer.Serialize(new { item.Id, item.Code, item.Name, item.Description, item.PackagingPerBox, item.GrossWeightKg, item.DepartmentId, item.IsActive });
    private void Validate(ItemInput input) { if (string.IsNullOrWhiteSpace(input.Name)) throw new InvalidOperationException("Item Name is required."); if (input.PackagingPerBox <= 0) throw new InvalidOperationException("Packing must be a whole number greater than zero."); if (input.GrossWeightKg <= 0) throw new InvalidOperationException("Gross Weight must be greater than zero."); if (!db.Departments.Any(department => department.Id == input.DepartmentId && department.IsActive)) throw new InvalidOperationException("The selected department does not exist."); }
}
