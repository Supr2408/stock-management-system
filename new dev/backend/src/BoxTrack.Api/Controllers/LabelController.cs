using BoxTrack.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,QC")]
[Route("api/labels")]
public sealed class LabelController(ILabelCatalog catalog) : ControllerBase
{
    [HttpGet("logos")]
    public async Task<ActionResult<IReadOnlyList<LabelLogoRow>>> Logos(CancellationToken cancellationToken) => Ok(await catalog.ListLogosAsync(cancellationToken));

    [HttpPost("logos")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<LabelLogoRow>> UploadLogo([FromForm] string name, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Choose a non-empty logo file." });
        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await catalog.UploadLogoAsync(new LabelLogoUpload(name, file.FileName, file.ContentType, stream), User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("generate")]
    public async Task<ActionResult<LabelGenerationResult>> Generate(LabelGenerationInput input, CancellationToken cancellationToken)
    {
        try { return Ok(await catalog.GenerateAsync(input, User.Identity?.Name, cancellationToken)); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("generated")]
    public async Task<ActionResult<Page<GeneratedLabelRow>>> Generated(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default) => Ok(await catalog.ListGeneratedAsync(page, pageSize, cancellationToken));
}
