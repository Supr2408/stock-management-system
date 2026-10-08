using System.Text.Json;
using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class CustomerCatalogService(BoxTrackDbContext db) : ICustomerCatalog
{
    public async Task<Page<CustomerRow>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking().Where(customer => customer.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(customer => customer.Name.ToLower().Contains(search.Trim().ToLower()));
        var total = await query.CountAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var records = await query.OrderBy(customer => customer.Name).Skip((page - 1) * pageSize).Take(pageSize).Select(customer => new CustomerRow(customer.Id, customer.LegacyId, customer.Name, customer.Address1, customer.Address2, customer.City, customer.Pincode, customer.State, customer.Country)).ToListAsync(cancellationToken);
        return new Page<CustomerRow>(records, total, page, pageSize);
    }

    public async Task<CustomerRow> CreateAsync(CustomerInput input, string? userId, CancellationToken cancellationToken)
    {
        var values = Normalize(input);
        var entity = new Customer { LegacyId = values.LegacyId, Name = values.Name, Address1 = values.Address1, Address2 = values.Address2, City = values.City, Pincode = values.Pincode, State = values.State, Country = values.Country, CreatedBy = userId };
        db.Customers.Add(entity);
        await SaveWithAuditAsync(entity, "Created", null, cancellationToken);
        return ToRow(entity);
    }

    public async Task<CustomerRow> UpdateAsync(int id, CustomerInput input, string? userId, CancellationToken cancellationToken)
    {
        var entity = await db.Customers.SingleOrDefaultAsync(customer => customer.Id == id && customer.IsActive, cancellationToken) ?? throw new KeyNotFoundException("The requested customer was not found.");
        var before = Serialize(entity);
        var values = Normalize(input);
        entity.LegacyId = values.LegacyId; entity.Name = values.Name; entity.Address1 = values.Address1; entity.Address2 = values.Address2; entity.City = values.City; entity.Pincode = values.Pincode; entity.State = values.State; entity.Country = values.Country; entity.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveWithAuditAsync(entity, "Updated", before, cancellationToken);
        return ToRow(entity);
    }

    public async Task DeleteAsync(int id, string? userId, CancellationToken cancellationToken)
    {
        var entity = await db.Customers.SingleOrDefaultAsync(customer => customer.Id == id && customer.IsActive, cancellationToken) ?? throw new KeyNotFoundException("The requested customer was not found.");
        var before = Serialize(entity);
        entity.IsActive = false; entity.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveWithAuditAsync(entity, "Deleted", before, cancellationToken);
    }

    public async Task<CustomerImportResult> ImportAsync(IReadOnlyList<CustomerImportRow> rows, string? userId, CancellationToken cancellationToken)
    {
        var issues = new List<ImportIssue>();
        var duplicates = new List<CustomerDuplicateComparison>();
        var existing = await db.Customers.AsNoTracking().Where(customer => customer.IsActive).Select(customer => new CustomerRow(customer.Id, customer.LegacyId, customer.Name, customer.Address1, customer.Address2, customer.City, customer.Pincode, customer.State, customer.Country)).ToListAsync(cancellationToken);
        var byId = existing.ToDictionary(customer => customer.Id);
        var byLegacyId = existing.Where(customer => customer.LegacyId.HasValue).GroupBy(customer => customer.LegacyId!.Value).ToDictionary(group => group.Key, group => group.First());
        var fileIds = new HashSet<int>();
        var additions = new List<Customer>();

        foreach (var row in rows)
        {
            var codeText = row.LegacyId?.ToString() ?? "blank";
            if (row.LegacyId is null or <= 0) { issues.Add(new ImportIssue(row.SourceRow, "C_ID must be a positive whole number.", codeText)); continue; }
            if (!fileIds.Add(row.LegacyId.Value)) { issues.Add(new ImportIssue(row.SourceRow, "Duplicate C_ID appears earlier in this file.", codeText)); continue; }
            var existingCustomer = byLegacyId.GetValueOrDefault(row.LegacyId.Value) ?? byId.GetValueOrDefault(row.LegacyId.Value);
            if (existingCustomer is not null)
            {
                duplicates.Add(new CustomerDuplicateComparison(row.SourceRow, existingCustomer, row));
                issues.Add(new ImportIssue(row.SourceRow, "Customer ID already exists in the database. Compare the records below and update it explicitly if needed.", codeText));
                continue;
            }

            try
            {
                var values = Normalize(new CustomerInput(row.LegacyId, row.Name ?? string.Empty, row.Address1, row.Address2, row.City, row.Pincode, row.State, row.Country));
                additions.Add(new Customer { LegacyId = values.LegacyId, Name = values.Name, Address1 = values.Address1, Address2 = values.Address2, City = values.City, Pincode = values.Pincode, State = values.State, Country = values.Country, CreatedBy = userId });
            }
            catch (InvalidOperationException exception)
            {
                issues.Add(new ImportIssue(row.SourceRow, exception.Message, codeText));
            }
        }

        if (additions.Count > 0)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.Customers.AddRange(additions);
            foreach (var customer in additions) db.AuditLogs.Add(new AuditLog { UserId = userId, EntityName = "Customer", Action = "Imported", AfterJson = Serialize(customer) });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return new CustomerImportResult(additions.Count, issues, duplicates);
    }

    private async Task SaveWithAuditAsync(Customer entity, string action, string? before, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog { UserId = entity.CreatedBy, EntityName = "Customer", Action = action, BeforeJson = before, AfterJson = Serialize(entity) });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static CustomerRow ToRow(Customer customer) => new(customer.Id, customer.LegacyId, customer.Name, customer.Address1, customer.Address2, customer.City, customer.Pincode, customer.State, customer.Country);
    private static string Serialize(Customer customer) => JsonSerializer.Serialize(new { customer.Id, customer.LegacyId, customer.Name, customer.Address1, customer.Address2, customer.City, customer.Pincode, customer.State, customer.Country, customer.IsActive });
    private static (int? LegacyId, string Name, string? Address1, string? Address2, string? City, string? Pincode, string? State, string? Country) Normalize(CustomerInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new InvalidOperationException("Customer name is required.");
        if (!string.IsNullOrWhiteSpace(input.Pincode) && (input.Pincode.Length != 6 || !input.Pincode.All(char.IsDigit))) throw new InvalidOperationException("Pincode must contain exactly 6 digits.");
        var state = NormalizeState(input.State);
        var country = NormalizeCountry(input.Country);
        return (input.LegacyId, input.Name.Trim(), input.Address1?.Trim(), input.Address2?.Trim(), input.City?.Trim(), input.Pincode?.Trim(), state, country);
    }

    private static string? NormalizeState(string? value)
    {
        var key = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(key)) return null;
        return key switch
        {
            "AP" or "ANDHRA PRADESH" => "Andhra Pradesh",
            "AR" or "ARUNACHAL PRADESH" => "Arunachal Pradesh",
            "AS" or "ASSAM" => "Assam",
            "BR" or "BIHAR" => "Bihar",
            "CG" or "CHHATTISGARH" => "Chhattisgarh",
            "GA" or "GOA" => "Goa",
            "GJ" or "GUJ" or "GUJARAT" => "Gujarat",
            "HR" or "HARYANA" => "Haryana",
            "HP" or "HIMACHAL PRADESH" => "Himachal Pradesh",
            "JH" or "JHARKHAND" => "Jharkhand",
            "KA" or "KARNATAKA" => "Karnataka",
            "KL" or "KERALA" => "Kerala",
            "MP" or "MADHYA PRADESH" => "Madhya Pradesh",
            "MH" or "MAHARASHTRA" => "Maharashtra",
            "MN" or "MANIPUR" => "Manipur",
            "ML" or "MEGHALAYA" => "Meghalaya",
            "MZ" or "MIZORAM" => "Mizoram",
            "NL" or "NAGALAND" => "Nagaland",
            "OD" or "OR" or "ODISHA" or "ORISSA" => "Odisha",
            "PB" or "PUNJAB" => "Punjab",
            "RJ" or "RAJASTHAN" => "Rajasthan",
            "SK" or "SIKKIM" => "Sikkim",
            "TN" or "TAMIL NADU" => "Tamil Nadu",
            "TS" or "TG" or "TELANGANA" => "Telangana",
            "TR" or "TRIPURA" => "Tripura",
            "UP" or "UTTAR PRADESH" => "Uttar Pradesh",
            "UK" or "UA" or "UTTARAKHAND" => "Uttarakhand",
            "WB" or "WEST BENGAL" => "West Bengal",
            "AN" or "ANDAMAN AND NICOBAR ISLANDS" => "Andaman and Nicobar Islands",
            "CH" or "CHANDIGARH" => "Chandigarh",
            "DNHDD" or "DADRA AND NAGAR HAVELI AND DAMAN AND DIU" or "DADRA & NAGAR HAVELI AND DAMAN & DIU" => "Dadra and Nagar Haveli and Daman and Diu",
            "DL" or "DELHI" or "NCT DELHI" => "Delhi",
            "JK" or "JAMMU AND KASHMIR" => "Jammu and Kashmir",
            "LA" or "LADAKH" => "Ladakh",
            "LD" or "LAKSHADWEEP" => "Lakshadweep",
            "PY" or "PUDUCHERRY" or "PONDICHERRY" => "Puducherry",
            _ => value!.Trim()
        };
    }

    private static string? NormalizeCountry(string? value)
    {
        var key = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(key)) return null;
        return key switch
        {
            "IN" or "IND" or "INDIA" or "BHARAT" => "India",
            "AE" or "UAE" or "UNITED ARAB EMIRATES" => "United Arab Emirates",
            "BD" or "BANGLADESH" => "Bangladesh",
            "BT" or "BHUTAN" => "Bhutan",
            "LK" or "SRI LANKA" => "Sri Lanka",
            "NP" or "NEPAL" => "Nepal",
            "US" or "USA" or "UNITED STATES" or "UNITED STATES OF AMERICA" => "United States",
            "GB" or "UK" or "UNITED KINGDOM" => "United Kingdom",
            _ => value!.Trim()
        };
    }
}
