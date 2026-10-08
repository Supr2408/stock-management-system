using BoxTrack.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Production,QC")]
[Route("api/production")]
public sealed class ProductionController(IProductionCatalog catalog) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<ActionResult<Page<PendingProductionLabelRow>>> Pending(int? departmentId, int? manufactureYear, int? manufactureMonth, string? search, int page = 1, int pageSize = 500, CancellationToken cancellationToken = default) => Ok(await catalog.ListPendingAsync(departmentId, manufactureYear, manufactureMonth, search, page, pageSize, cancellationToken));

    [HttpPost("add-to-stock")]
    public async Task<ActionResult<AddToStockResult>> AddToStock(AddToStockInput input, CancellationToken cancellationToken)
    {
        try { return Ok(await catalog.AddToStockAsync(input, User.Identity?.Name, cancellationToken)); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("batches")]
    public Task<IReadOnlyList<string>> Batches(CancellationToken cancellationToken) => catalog.ListBatchesAsync(cancellationToken);
}
