using Microsoft.AspNetCore.Http;

namespace JobTracker.Api.Infrastructure.Files;

public sealed class UploadPhotoForm
{
    public IFormFile File { get; set; } = default!;
    public string? Caption { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
}
