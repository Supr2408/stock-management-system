using System.Globalization;
using System.Text;
using System.Text.Json;
using BoxTrack.Domain;
using ExcelDataReader;

namespace BoxTrack.Application;

public sealed record ItemRow(int Id, int Code, string Name, string? Description, int PackagingPerBox, decimal GrossWeightKg, int DepartmentId, string DepartmentName);
public sealed record ItemImportRow(int SourceRow, int? Code, string? Name, string? Description, int? PackagingPerBox, decimal? GrossWeightKg, string? Department, int? DepartmentId);
public sealed record ImportIssue(int Row, string Message, string? Code = null);
public sealed record ItemDuplicateComparison(int Row, ItemRow Existing, ItemImportRow Uploaded);
public sealed record ItemImportResult(int Imported, IReadOnlyList<ImportIssue> Issues, IReadOnlyList<ItemDuplicateComparison> Duplicates);

public interface IItemCatalog
{
    Task<Page<ItemRow>> ListAsync(string? search, int? departmentId, int page, int pageSize, CancellationToken cancellationToken);
    Task<ItemRow> CreateAsync(ItemInput input, string? userId, CancellationToken cancellationToken);
    Task<ItemRow> UpdateAsync(int id, ItemInput input, string? userId, CancellationToken cancellationToken);
    Task DeleteAsync(int id, string? userId, CancellationToken cancellationToken);
    Task<ItemImportResult> ImportAsync(IReadOnlyList<ItemImportRow> rows, string? userId, CancellationToken cancellationToken);
}

public static class ItemImportParser
{
    public static IReadOnlyList<ItemImportRow> Parse(Stream content, string fileName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(content, new ExcelReaderConfiguration { AutodetectSeparators = [',', ';', '\t'] })
            : ExcelReaderFactory.CreateReader(content);
        var rows = new List<ItemImportRow>();
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!reader.Read()) throw new InvalidOperationException("The import file is empty.");
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var header = Normalize(reader.GetValue(index)?.ToString());
            if (!string.IsNullOrWhiteSpace(header)) headers[header] = index;
        }
        var required = new[] { "itemid", "itemname", "description", "department", "packing", "grossweight", "departmentid" };
        var missing = required.Where(header => !headers.ContainsKey(header)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"The import file is missing required columns: {string.Join(", ", missing)}.");
        var sourceRow = 1;
        while (reader.Read())
        {
            sourceRow++;
            if (Enumerable.Range(0, reader.FieldCount).All(index => string.IsNullOrWhiteSpace(reader.GetValue(index)?.ToString()))) continue;
            rows.Add(new ItemImportRow(sourceRow, ParseInt(reader, headers["itemid"]), Value(reader, headers["itemname"]), Value(reader, headers["description"]), ParseInt(reader, headers["packing"]), ParseDecimal(reader, headers["grossweight"]), Value(reader, headers["department"]), ParseInt(reader, headers["departmentid"])));
        }
        return rows;
    }

    private static string? Value(IExcelDataReader reader, int index) => reader.GetValue(index)?.ToString()?.Trim();
    private static int? ParseInt(IExcelDataReader reader, int index) => int.TryParse(Value(reader, index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static decimal? ParseDecimal(IExcelDataReader reader, int index) => decimal.TryParse(Value(reader, index), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static string Normalize(string? value) => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
