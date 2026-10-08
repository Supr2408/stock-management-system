using System.Text.Json;
using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class DepartmentCatalogService(BoxTrackDbContext db) : IDepartmentCatalog
{
    public async Task<Page<DepartmentRow>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Departments.AsNoTracking().Where(department => department.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(department => department.Name.ToLower().Contains(search.Trim().ToLower()));
        var total = await query.CountAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var records = await query.OrderBy(department => department.Name).Skip((page - 1) * pageSize).Take(pageSize).Select(department => new DepartmentRow(department.Id, department.Name)).ToListAsync(cancellationToken);
        return new Page<DepartmentRow>(records, total, page, pageSize);
    }

    public async Task<DepartmentRow> CreateAsync(DepartmentInput input, string? userId, CancellationToken cancellationToken)
    {
        var name = NormalizeName(input.Name);
        await EnsureUniqueAsync(name, null, cancellationToken);
        var entity = new Department { Name = name, CreatedBy = userId };
        db.Departments.Add(entity);
        await SaveWithAuditAsync(entity, "Created", null, cancellationToken);
        return new DepartmentRow(entity.Id, entity.Name);
    }

    public async Task<DepartmentRow> UpdateAsync(int id, DepartmentInput input, string? userId, CancellationToken cancellationToken)
    {
        var name = NormalizeName(input.Name);
        await EnsureUniqueAsync(name, id, cancellationToken);
        var entity = await db.Departments.SingleOrDefaultAsync(department => department.Id == id && department.IsActive, cancellationToken) ?? throw new KeyNotFoundException("The requested department was not found.");
        var before = JsonSerializer.Serialize(new { entity.Id, entity.Name, entity.IsActive });
        entity.Name = name;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveWithAuditAsync(entity, "Updated", before, cancellationToken);
        return new DepartmentRow(entity.Id, entity.Name);
    }

    public async Task DeleteAsync(int id, string? userId, CancellationToken cancellationToken)
    {
        var entity = await db.Departments.SingleOrDefaultAsync(department => department.Id == id && department.IsActive, cancellationToken) ?? throw new KeyNotFoundException("The requested department was not found.");
        if (await db.Items.AnyAsync(item => item.IsActive && item.DepartmentId == id, cancellationToken)) throw new InvalidOperationException("This department cannot be deleted because items exist in it.");
        var before = JsonSerializer.Serialize(new { entity.Id, entity.Name, entity.IsActive });
        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveWithAuditAsync(entity, "Deleted", before, cancellationToken);
    }

    private async Task EnsureUniqueAsync(string name, int? excludedId, CancellationToken cancellationToken)
    {
        if (await db.Departments.AnyAsync(department => department.IsActive && department.Name.ToLower() == name.ToLower() && (!excludedId.HasValue || department.Id != excludedId.Value), cancellationToken)) throw new InvalidOperationException("A department with this name already exists.");
    }

    private async Task SaveWithAuditAsync(Department entity, string action, string? before, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog { UserId = entity.CreatedBy, EntityName = "Department", Action = action, BeforeJson = before, AfterJson = JsonSerializer.Serialize(new { entity.Id, entity.Name, entity.IsActive }) });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeName(string value) { if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Department name is required."); return value.Trim(); }
}
