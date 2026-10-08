using BoxTrack.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,QC")]
[Route("api/label-templates")]
public sealed class LabelTemplateController(ILabelTemplateCatalog catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LabelTemplateDto>>> List(CancellationToken cancellationToken)
    {
        var templates = await catalog.ListTemplatesAsync(cancellationToken);
        return Ok(templates);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LabelTemplateDto>> GetById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var template = await catalog.GetTemplateByIdAsync(id, cancellationToken);
            return Ok(template);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LabelTemplateDto>> Create(
        [FromBody] SaveLabelTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await catalog.SaveTemplateAsync(null, request, User.Identity?.Name, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LabelTemplateDto>> Update(
        int id,
        [FromBody] SaveLabelTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await catalog.SaveTemplateAsync(id, request, User.Identity?.Name, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await catalog.DeleteTemplateAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/test-print")]
    public async Task<ActionResult<PrintJobDto>> TestPrint(int id, CancellationToken cancellationToken)
    {
        try
        {
            var job = await catalog.TestPrintTemplateAsync(id, User.Identity?.Name, cancellationToken);
            return Ok(job);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/print")]
    public async Task<ActionResult<PrintJobDto>> Print(
        int id,
        [FromBody] PrintTemplateJobRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await catalog.PrintTemplateAsync(id, request, User.Identity?.Name, cancellationToken);
            return Ok(job);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
