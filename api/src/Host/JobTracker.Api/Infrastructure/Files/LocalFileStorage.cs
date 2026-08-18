using Microsoft.Extensions.Options;

namespace JobTracker.Api.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; init; } = "wwwroot/uploads";
    public string PublicBaseUrl { get; init; } = "/uploads";
    public long MaxSizeBytes { get; init; } = 10 * 1024 * 1024; // 10 MB
    public string[] AllowedContentTypes { get; init; } =
        ["image/jpeg", "image/png", "image/webp"];
}

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed record StoredFile(string Url, string RelativePath, long Size);

public sealed class LocalFileStorage(
    IOptions<FileStorageOptions> options,
    ILogger<LocalFileStorage> logger) : IFileStorage
{
    private readonly FileStorageOptions _opts = options.Value;

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!_opts.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported content type '{contentType}'.");

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ContentTypeToExtension(contentType);

        var relative = Path.Combine(
            DateTime.UtcNow.ToString("yyyy/MM/dd"),
            $"{Guid.NewGuid():N}{extension}");
        var absoluteRoot = Path.GetFullPath(_opts.RootPath);
        var absolute = Path.Combine(absoluteRoot, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        await using (var target = File.Create(absolute))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var size = new FileInfo(absolute).Length;
        var url = $"{_opts.PublicBaseUrl.TrimEnd('/')}/{relative.Replace('\\', '/')}";
        logger.LogInformation("Saved {Size} bytes to {Absolute} → {Url}", size, absolute, url);
        return new StoredFile(url, relative, size);
    }

    private static string ContentTypeToExtension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png"  => ".png",
        "image/webp" => ".webp",
        _            => ".bin",
    };
}
