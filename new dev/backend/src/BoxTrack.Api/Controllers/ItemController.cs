using BoxTrack.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/items")]
public sealed class ItemController(IItemCatalog catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Page<ItemRow>>> List(string? search, int? departmentId, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Ok(await catalog.ListAsync(search, departmentId, page, pageSize, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ItemRow>> Create(ItemInput input, CancellationToken cancellationToken) => await ExecuteAsync(() => catalog.CreateAsync(input, User.Identity?.Name, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ItemRow>> Update(int id, ItemInput input, CancellationToken cancellationToken) => await ExecuteAsync(() => catalog.UpdateAsync(id, input, User.Identity?.Name, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try { await catalog.DeleteAsync(id, User.Identity?.Name, cancellationToken); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ItemImportResult>> Import(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Choose a non-empty .csv, .xls, or .xlsx file." });
        var extension = Path.GetExtension(file.FileName);
        if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Only .csv, .xls, and .xlsx files are supported." });
        try
        {
            await using var stream = file.OpenReadStream();
            var rows = ItemImportParser.Parse(stream, file.FileName);
            return Ok(await catalog.ImportAsync(rows, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private async Task<ActionResult<ItemRow>> ExecuteAsync(Func<Task<ItemRow>> action)
    {
        try { return Ok(await action()); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
