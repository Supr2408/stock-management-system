using BoxTrack.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,QC")]
[Route("api/dispatch")]
public sealed class DispatchController(IDispatchCatalog catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Page<DispatchHistoryRow>>> List(string? salesOrderNumber, string? invoiceNumber, string? customer, DateOnly? dispatchDate, string? barcode, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        return Ok(await catalog.ListAsync(salesOrderNumber, invoiceNumber, customer, dispatchDate, barcode, page, pageSize, cancellationToken));
    }

    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<DispatchResult>> Create([FromForm] int customerId, [FromForm] string salesOrderNumber, [FromForm] string invoiceNumber, [FromForm] DateOnly dispatchDate, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Choose a non-empty scanner file." });
        var extension = Path.GetExtension(file.FileName);
        if (!new[] { ".txt", ".csv", ".xls", ".xlsx" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = "Only .txt, .csv, .xls, and .xlsx scanner files are supported." });
        try
        {
            await using var stream = file.OpenReadStream();
            var barcodes = DispatchBarcodeParser.Parse(stream, file.FileName);
            return Ok(await catalog.DispatchAsync(new DispatchInput(customerId, salesOrderNumber, invoiceNumber, dispatchDate, file.FileName, barcodes), User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
