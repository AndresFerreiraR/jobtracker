using JobTracker.Api.Infrastructure.Files;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JobTracker.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
[Produces("application/json")]
public sealed class SignatureUploadController(
    IFileStorage storage,
    IOptions<FileStorageOptions> options) : ControllerBase
{
    [HttpPost("signature")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadSignature(
        [FromForm] UploadPhotoForm form,
        CancellationToken ct)
    {
        var opts = options.Value;
        var file = form.File;
        if (file is null || file.Length == 0)
            return Problem(title: "Missing file", statusCode: StatusCodes.Status400BadRequest);
        if (file.Length > opts.MaxSizeBytes)
            return Problem(
                title: "File too large",
                detail: $"Max allowed size is {opts.MaxSizeBytes} bytes.",
                statusCode: StatusCodes.Status400BadRequest);

        StoredFile stored;
        try
        {
            await using var stream = file.OpenReadStream();
            stored = await storage.SaveAsync(stream, file.FileName, file.ContentType, ct);
        }
        catch (InvalidOperationException e)
        {
            return Problem(title: "Invalid file", detail: e.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var absoluteUrl = stored.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? stored.Url
            : $"{Request.Scheme}://{Request.Host}{stored.Url}";

        return Created(absoluteUrl, new { url = absoluteUrl, size = stored.Size });
    }
}
