using System.Collections.Concurrent;
using BoxTrack.Domain;

namespace BoxTrack.Application;

public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int CurrentPage, int PageSize);
public sealed record ItemInput(string Name, string? Description, int PackagingPerBox, decimal GrossWeightKg, int DepartmentId, int? Code = null);
public sealed record DepartmentInput(string Name);
public sealed record CustomerInput(int? LegacyId, string Name, string? Address1, string? Address2, string? City, string? Pincode, string? State, string? Country);

public sealed class MasterService
{
    private readonly ConcurrentDictionary<int, Department> departments = new();
    private readonly ConcurrentDictionary<int, Item> items = new();
    private readonly ConcurrentDictionary<int, Customer> customers = new();
    private readonly object codeLock = new();
    private int nextDepartmentId;
    private int nextItemId;
    private int nextCustomerId;

    public MasterService()
    {
        CreateDepartment(new DepartmentInput("AFC"));
        CreateDepartment(new DepartmentInput("AFR"));
        CreateDepartment(new DepartmentInput("PLASCON"));
    }

    public Page<Department> Departments(string? search, int page, int pageSize) => PageOf(departments.Values.Where(x => x.IsActive && (string.IsNullOrWhiteSpace(search) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Name), page, pageSize);
    public Page<Item> Items(string? search, int? departmentId, int page, int pageSize) => PageOf(items.Values.Where(x => x.IsActive && (string.IsNullOrWhiteSpace(search) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) && (!departmentId.HasValue || x.DepartmentId == departmentId)).OrderBy(x => x.Code), page, pageSize);
    public Page<Customer> Customers(string? search, int page, int pageSize) => PageOf(customers.Values.Where(x => x.IsActive && (string.IsNullOrWhiteSpace(search) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Name), page, pageSize);

    public Department CreateDepartment(DepartmentInput input) { ValidateName(input.Name); if (departments.Values.Any(x => x.IsActive && x.Name.Equals(input.Name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("A department with this name already exists."); var entity = new Department { Id = Interlocked.Increment(ref nextDepartmentId), Name = input.Name.Trim() }; departments[entity.Id] = entity; return entity; }
    public Item CreateItem(ItemInput input) { ValidateItem(input); lock (codeLock) { var code = input.Code ?? NextCode(); if (code is < 101 or > 999 || items.Values.Any(x => x.IsActive && x.Code == code)) throw new InvalidOperationException("Item ID Code must be unique and between 101 and 999."); var entity = new Item { Id = Interlocked.Increment(ref nextItemId), Code = code, Name = input.Name.Trim(), Description = input.Description, PackagingPerBox = input.PackagingPerBox, GrossWeightKg = input.GrossWeightKg, DepartmentId = input.DepartmentId }; items[entity.Id] = entity; return entity; } }
    public Customer CreateCustomer(CustomerInput input) { ValidateName(input.Name); ValidatePincode(input.Pincode); var entity = new Customer { Id = Interlocked.Increment(ref nextCustomerId), LegacyId = input.LegacyId, Name = input.Name.Trim(), Address1 = input.Address1, Address2 = input.Address2, City = input.City, Pincode = input.Pincode, State = NormalizeState(input.State), Country = NormalizeCountry(input.Country) }; customers[entity.Id] = entity; return entity; }
    public void DeleteDepartment(int id) { if (items.Values.Any(x => x.IsActive && x.DepartmentId == id)) throw new InvalidOperationException("This department cannot be deleted because items exist in it."); SoftDelete(departments, id); }
    public void DeleteItem(int id) => SoftDelete(items, id);
    public void DeleteCustomer(int id) => SoftDelete(customers, id);
    private int NextCode() { var highest = items.Values.Select(x => x.Code).DefaultIfEmpty(100).Max(); if (highest >= 999) throw new InvalidOperationException("No item ID Codes remain; the maximum is 999."); return highest + 1; }
    private static void ValidateName(string value) { if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Name is required."); }
    private void ValidateItem(ItemInput input) { ValidateName(input.Name); if (input.PackagingPerBox <= 0) throw new InvalidOperationException("Standard packaging must be greater than zero."); if (input.GrossWeightKg <= 0) throw new InvalidOperationException("Gross weight must be greater than zero."); if (!departments.ContainsKey(input.DepartmentId)) throw new InvalidOperationException("The selected department does not exist."); }
    private static void ValidatePincode(string? value) { if (!string.IsNullOrWhiteSpace(value) && (value.Length != 6 || !value.All(char.IsDigit))) throw new InvalidOperationException("Pincode must contain exactly 6 digits."); }
    private static string? NormalizeState(string? value) => value?.Trim().ToUpperInvariant() switch { "GUJARAT" or "GUJ" or "GJ" => "Gujarat", null or "" => null, _ => value.Trim() };
    private static string? NormalizeCountry(string? value) => value?.Trim().ToUpperInvariant() switch { "INDIA" or "IN" => "India", null or "" => null, _ => value.Trim() };
    private static void SoftDelete<T>(ConcurrentDictionary<int, T> records, int id) where T : AuditedEntity { if (!records.TryGetValue(id, out var entity) || !entity.IsActive) throw new KeyNotFoundException("The requested record was not found."); entity.IsActive = false; entity.UpdatedAt = DateTimeOffset.UtcNow; }
    private static Page<T> PageOf<T>(IEnumerable<T> values, int page, int pageSize) { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var list = values.ToList(); return new Page<T>(list.Skip((page - 1) * pageSize).Take(pageSize).ToList(), list.Count, page, pageSize); }
}
