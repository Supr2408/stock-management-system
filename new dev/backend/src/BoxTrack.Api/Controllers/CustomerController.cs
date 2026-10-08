using BoxTrack.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomerController(ICustomerCatalog catalog) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,QC")]
    public async Task<ActionResult<Page<CustomerRow>>> List(string? search, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Ok(await catalog.ListAsync(search, page, pageSize, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CustomerRow>> Create(CustomerInput input, CancellationToken cancellationToken) => await ExecuteAsync(() => catalog.CreateAsync(input, User.Identity?.Name, cancellationToken));

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CustomerRow>> Update(int id, CustomerInput input, CancellationToken cancellationToken) => await ExecuteAsync(() => catalog.UpdateAsync(id, input, User.Identity?.Name, cancellationToken));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try { await catalog.DeleteAsync(id, User.Identity?.Name, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<CustomerImportResult>> Import(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Choose a non-empty .csv, .xls, or .xlsx file." });
        var extension = Path.GetExtension(file.FileName);
        if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Only .csv, .xls, and .xlsx files are supported." });
        try
        {
            await using var stream = file.OpenReadStream();
            var rows = CustomerImportParser.Parse(stream, file.FileName);
            return Ok(await catalog.ImportAsync(rows, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("export")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Export(string format = "csv", CancellationToken cancellationToken = default)
    {
        var rows = await AllCustomersAsync(cancellationToken);
        if (format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
            return File(BuildXlsx(rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "boxtrack-customers.xlsx");
        if (format.Equals("xls", StringComparison.OrdinalIgnoreCase))
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BuildTabDelimited(rows))).ToArray(), "application/vnd.ms-excel; charset=utf-8", "boxtrack-customers.xls");
        if (!format.Equals("csv", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Only csv, xls, and xlsx exports are supported." });
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BuildCsv(rows))).ToArray(), "text/csv; charset=utf-8", "boxtrack-customers.csv");
    }

    private async Task<ActionResult<CustomerRow>> ExecuteAsync(Func<Task<CustomerRow>> action)
    {
        try { return Ok(await action()); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    private async Task<List<CustomerRow>> AllCustomersAsync(CancellationToken cancellationToken)
    {
        var rows = new List<CustomerRow>();
        const int pageSize = 100;
        for (var page = 1; ; page++)
        {
            var result = await catalog.ListAsync(null, page, pageSize, cancellationToken);
            rows.AddRange(result.Items);
            if (rows.Count >= result.Total) return rows;
        }
    }

    private static string BuildCsv(IReadOnlyList<CustomerRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("C_ID,C_Name,C_Add1,C_Add2,C_City,C_Pin,C_State,C_Country");
        foreach (var row in rows)
            builder.AppendLine(string.Join(",", ExportId(row), Csv(row.Name), Csv(row.Address1), Csv(row.Address2), Csv(row.City), Csv(row.Pincode), Csv(row.State), Csv(row.Country)));
        return builder.ToString();
    }

    private static byte[] BuildXlsx(IReadOnlyList<CustomerRow> rows)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            AddEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            AddEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            AddEntry(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            AddEntry(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Customers\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheet(rows));
        }

        return stream.ToArray();
    }

    private static string BuildTabDelimited(IReadOnlyList<CustomerRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("C_ID\tC_Name\tC_Add1\tC_Add2\tC_City\tC_Pin\tC_State\tC_Country");
        foreach (var row in rows)
            builder.AppendLine(string.Join("\t", ExportId(row), Tab(row.Name), Tab(row.Address1), Tab(row.Address2), Tab(row.City), Tab(row.Pincode), Tab(row.State), Tab(row.Country)));
        return builder.ToString();
    }

    private static string BuildWorksheet(IReadOnlyList<CustomerRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols><col min=\"1\" max=\"1\" width=\"12\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"34\" customWidth=\"1\"/><col min=\"3\" max=\"4\" width=\"36\" customWidth=\"1\"/><col min=\"5\" max=\"8\" width=\"16\" customWidth=\"1\"/></cols><sheetData>");
        builder.Append(Row(1, ["C_ID", "C_Name", "C_Add1", "C_Add2", "C_City", "C_Pin", "C_State", "C_Country"]));
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            builder.Append(Row(index + 2, [ExportId(row).ToString(), row.Name, row.Address1 ?? string.Empty, row.Address2 ?? string.Empty, row.City ?? string.Empty, row.Pincode ?? string.Empty, row.State ?? string.Empty, row.Country ?? string.Empty]));
        }

        builder.Append("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static string Row(int rowNumber, IReadOnlyList<string> values)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowNumber}\">");
        for (var index = 0; index < values.Count; index++)
        {
            var cell = $"{(char)('A' + index)}{rowNumber}";
            builder.Append(CultureInfo.InvariantCulture, $"<c r=\"{cell}\" t=\"inlineStr\"><is><t>{XmlEscape(values[index])}</t></is></c>");
        }

        builder.Append("</row>");
        return builder.ToString();
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static int ExportId(CustomerRow row) => row.LegacyId ?? row.Id;
    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    private static string Tab(string? value) => (value ?? string.Empty).Replace('\t', ' ').ReplaceLineEndings(" ");
    private static string XmlEscape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
