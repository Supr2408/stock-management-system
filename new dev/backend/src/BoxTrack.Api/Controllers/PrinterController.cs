using BoxTrack.Application;
using BoxTrack.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/printers")]
public sealed class PrinterController(IPrinterCatalog catalog) : ControllerBase
{
    [HttpGet("available")]
    public async Task<ActionResult<IReadOnlyList<AvailablePrinterDto>>> GetAvailablePrinters(CancellationToken cancellationToken)
    {
        var printers = await catalog.GetAvailablePrintersAsync(cancellationToken);
        return Ok(printers);
    }

    [HttpGet("configuration")]
    public async Task<ActionResult<PrinterConfigurationsSummaryDto>> GetConfiguration(CancellationToken cancellationToken)
    {
        var configuration = await catalog.GetConfigurationAsync(cancellationToken);
        return Ok(configuration);
    }

    [HttpPut("configuration/regular")]
    public async Task<ActionResult<PrinterConfigurationDto>> SetRegularPrinter(
        [FromBody] UpdatePrinterConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.PrinterName))
        {
            return BadRequest(new { message = "Printer name is required." });
        }

        try
        {
            var result = await catalog.SetConfigurationAsync(
                PrinterCategory.RegularDocument,
                request.PrinterName,
                User.Identity?.Name,
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("configuration/barcode")]
    public async Task<ActionResult<PrinterConfigurationDto>> SetBarcodePrinter(
        [FromBody] UpdateBarcodePrinterConfigRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.PrinterName))
        {
            return BadRequest(new { message = "Printer name is required." });
        }

        try
        {
            var result = await catalog.SetBarcodeConfigurationAsync(
                request,
                User.Identity?.Name,
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("test-print")]
    public async Task<ActionResult<PrintJobDto>> TestPrint(
        [FromBody] TestPrintRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "Printer category is required for test print (RegularDocument or Barcode)." });
        }

        if (!Enum.TryParse<PrinterCategory>(request.Category, true, out var category))
        {
            return BadRequest(new { message = $"Invalid printer category '{request.Category}'. Supported categories are 'RegularDocument' and 'Barcode'." });
        }

        try
        {
            var result = await catalog.TestPrintAsync(category, User.Identity?.Name, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("print-document")]
    public async Task<ActionResult<PrintJobDto>> PrintDocument(
        [FromBody] PrintReportDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "Document title is required." });
        }

        try
        {
            var result = await catalog.PrintReportDocumentAsync(request, User.Identity?.Name, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<PrintJobDto>>> ListRecentJobs(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var jobs = await catalog.ListRecentJobsAsync(limit, cancellationToken);
        return Ok(jobs);
    }
}
