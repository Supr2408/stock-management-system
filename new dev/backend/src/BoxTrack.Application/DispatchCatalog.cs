using System.Text;
using ExcelDataReader;

namespace BoxTrack.Application;

public sealed record DispatchInput(int CustomerId, string SalesOrderNumber, string InvoiceNumber, DateOnly DispatchDate, string SourceFileName, IReadOnlyList<string> Barcodes);
public sealed record DispatchCustomerRow(string Name, string? Address1, string? Address2, string? City, string? Pincode, string? State, string? Country);
public sealed record DispatchLabelRow(string BarcodeValue, int ItemCode, string ItemName, string BatchNumber, int PiecesPerBox, decimal GrossWeightKg);
public sealed record DispatchResult(long DispatchId, int Dispatched, string SalesOrderNumber, string InvoiceNumber, DispatchCustomerRow Customer, DateOnly DispatchDate, IReadOnlyList<DispatchLabelRow> Labels);
public sealed record DispatchHistoryRow(long DispatchId, string SalesOrderNumber, string InvoiceNumber, DispatchCustomerRow Customer, DateOnly DispatchDate, string SourceFileName, int LabelCount, IReadOnlyList<DispatchLabelRow> Labels);

public interface IDispatchCatalog
{
    Task<DispatchResult> DispatchAsync(DispatchInput input, string? userId, CancellationToken cancellationToken);
    Task<Page<DispatchHistoryRow>> ListAsync(string? salesOrderNumber, string? invoiceNumber, string? customer, DateOnly? dispatchDate, string? barcode, int page, int pageSize, CancellationToken cancellationToken);
}

public static class DispatchBarcodeParser
{
    public static IReadOnlyList<string> Parse(Stream content, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".txt") return ParseText(content);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = extension == ".csv"
            ? ExcelReaderFactory.CreateCsvReader(content, new ExcelReaderConfiguration { AutodetectSeparators = [',', ';', '\t'] })
            : ExcelReaderFactory.CreateReader(content);
        var values = new List<string>();
        while (reader.Read())
            for (var index = 0; index < reader.FieldCount; index++) AddValue(values, reader.GetValue(index)?.ToString());
        return Validate(values);
    }

    private static IReadOnlyList<string> ParseText(Stream content)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var values = new List<string>();
        while (reader.ReadLine() is { } line)
            foreach (var value in line.Split(new[] { ',', ';', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)) AddValue(values, value);
        return Validate(values);
    }

    private static void AddValue(List<string> values, string? value)
    {
        var barcode = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (!string.IsNullOrWhiteSpace(barcode)) values.Add(barcode);
    }

    private static IReadOnlyList<string> Validate(List<string> values)
    {
        if (values.Count == 0) throw new InvalidOperationException("The scanner file does not contain any barcode numbers.");
        var duplicates = values.GroupBy(value => value).FirstOrDefault(group => group.Count() > 1);
        if (duplicates is not null) throw new InvalidOperationException($"The scanner file contains barcode {duplicates.Key} more than once.");
        return values;
    }
}
