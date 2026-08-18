using Jobs.Domain.Common;
using JobTracker.SharedKernel.Primitives;

namespace Jobs.Domain.Jobs;

public sealed class JobPhoto : Entity<JobPhotoId>
{
    public JobId JobId { get; private set; }
    public string Url { get; private set; } = null!;
    public DateTimeOffset CapturedAt { get; private set; }
    public string? Caption { get; private set; }

    private JobPhoto() { }

    internal JobPhoto(JobPhotoId id, JobId jobId, string url, DateTimeOffset capturedAt, string? caption)
        : base(id)
    {
        JobId = jobId;
        Url = url;
        CapturedAt = capturedAt;
        Caption = caption;
    }
}
