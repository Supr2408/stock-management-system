using BoxTrack.Domain;

namespace BoxTrack.Application;

public sealed record DepartmentRow(int Id, string Name);

public interface IDepartmentCatalog
{
    Task<Page<DepartmentRow>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<DepartmentRow> CreateAsync(DepartmentInput input, string? userId, CancellationToken cancellationToken);
    Task<DepartmentRow> UpdateAsync(int id, DepartmentInput input, string? userId, CancellationToken cancellationToken);
    Task DeleteAsync(int id, string? userId, CancellationToken cancellationToken);
}
