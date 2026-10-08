using BoxTrack.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/departments")]
public sealed class DepartmentController(IDepartmentCatalog catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Page<DepartmentRow>>> List(string? search, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) => Ok(await catalog.ListAsync(search, page, pageSize, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DepartmentRow>> Create(DepartmentInput input, CancellationToken cancellationToken) => await ExecuteAsync(() => catalog.CreateAsync(input, User.Identity?.Name, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DepartmentRow>> Update(int id, DepartmentInput input, CancellationToken cancellationToken) => await ExecuteAsync(() => catalog.UpdateAsync(id, input, User.Identity?.Name, cancellationToken));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try { await catalog.DeleteAsync(id, User.Identity?.Name, cancellationToken); return NoContent(); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    private async Task<ActionResult<DepartmentRow>> ExecuteAsync(Func<Task<DepartmentRow>> action)
    {
        try { return Ok(await action()); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
