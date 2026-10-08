using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace BoxTrack.Application;

public sealed record CustomerRow(int Id, int? LegacyId, string Name, string? Address1, string? Address2, string? City, string? Pincode, string? State, string? Country);
public sealed record CustomerImportRow(int SourceRow, int? LegacyId, string? Name, string? Address1, string? Address2, string? City, string? Pincode, string? State, string? Country);
public sealed record CustomerDuplicateComparison(int Row, CustomerRow Existing, CustomerImportRow Uploaded);
public sealed record CustomerImportResult(int Imported, IReadOnlyList<ImportIssue> Issues, IReadOnlyList<CustomerDuplicateComparison> Duplicates);

public interface ICustomerCatalog
{
    Task<Page<CustomerRow>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<CustomerRow> CreateAsync(CustomerInput input, string? userId, CancellationToken cancellationToken);
    Task<CustomerRow> UpdateAsync(int id, CustomerInput input, string? userId, CancellationToken cancellationToken);
    Task DeleteAsync(int id, string? userId, CancellationToken cancellationToken);
    Task<CustomerImportResult> ImportAsync(IReadOnlyList<CustomerImportRow> rows, string? userId, CancellationToken cancellationToken);
}

public static class CustomerImportParser
{
    public static IReadOnlyList<CustomerImportRow> Parse(Stream content, string fileName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(content, new ExcelReaderConfiguration { AutodetectSeparators = [',', ';', '\t'] })
            : ExcelReaderFactory.CreateReader(content);
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!reader.Read()) throw new InvalidOperationException("The import file is empty.");
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var header = Normalize(reader.GetValue(index)?.ToString());
            if (!string.IsNullOrWhiteSpace(header)) headers[header] = index;
        }

        var cId = Header(headers, "cid", "custid", "customerid", "legacyid");
        var name = Header(headers, "cname", "customername", "name");
        var address1 = Header(headers, "cadd1", "address1", "addr1");
        var address2 = Header(headers, "cadd2", "address2", "addr2");
        var city = Header(headers, "ccity", "city");
        var pincode = Header(headers, "cpin", "pincode", "pin");
        var state = Header(headers, "cstate", "state");
        var country = Header(headers, "ccountry", "country");
        var missing = new[] { (cId, "C_ID"), (name, "C_Name"), (address1, "C_Add1"), (address2, "C_Add2"), (city, "C_City"), (pincode, "C_Pin"), (state, "C_State"), (country, "C_Country") }.Where(column => !column.Item1.HasValue).Select(column => column.Item2).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"The import file is missing required columns: {string.Join(", ", missing)}.");

        var rows = new List<CustomerImportRow>();
        var sourceRow = 1;
        while (reader.Read())
        {
            sourceRow++;
            if (Enumerable.Range(0, reader.FieldCount).All(index => string.IsNullOrWhiteSpace(reader.GetValue(index)?.ToString()))) continue;
            rows.Add(new CustomerImportRow(sourceRow, ParseInt(reader, cId!.Value), Value(reader, name!.Value), Value(reader, address1!.Value), Value(reader, address2!.Value), Value(reader, city!.Value), Value(reader, pincode!.Value), Value(reader, state!.Value), Value(reader, country!.Value)));
        }

        return rows;
    }

    private static int? Header(IReadOnlyDictionary<string, int> headers, params string[] names) => names.Select(name => headers.TryGetValue(name, out var index) ? index : (int?)null).FirstOrDefault(index => index.HasValue);
    private static string? Value(IExcelDataReader reader, int index) => reader.GetValue(index)?.ToString()?.Trim();
    private static int? ParseInt(IExcelDataReader reader, int index) => int.TryParse(Value(reader, index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static string Normalize(string? value) => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
