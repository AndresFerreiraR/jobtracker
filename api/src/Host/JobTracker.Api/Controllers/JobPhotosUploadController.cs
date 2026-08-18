using Jobs.Application.Jobs.Commands.AddJobPhoto;
using JobTracker.Api.Infrastructure.Files;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Presentation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JobTracker.Api.Controllers;

[ApiController]
[Route("api/v1/jobs/{id:guid}/photos")]
[Produces("application/json")]
public sealed class JobPhotosUploadController(
    ISender sender,
    IUnitOfWork uow,
    IFileStorage storage,
    IOptions<FileStorageOptions> options,
    IDateTimeProvider clock) : ApiControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upload(
        [FromRoute] Guid id,
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

        var command = new AddJobPhotoCommand(id, stored.Url, form.CapturedAt ?? clock.UtcNow, form.Caption);
        var result = await sender.Send(command, ct);
        if (result.IsSuccess) await uow.SaveChangesAsync(ct);

        return result.IsSuccess
            ? Created(
                $"/api/v1/jobs/{id}/photos/{result.Value}",
                new { id = result.Value, url = stored.Url, size = stored.Size })
            : ToActionResult(result);
    }
}
