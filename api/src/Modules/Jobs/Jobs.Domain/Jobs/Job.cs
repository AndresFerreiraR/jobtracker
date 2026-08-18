using Jobs.Domain.Common;
using Jobs.Domain.Jobs.Events;
using JobTracker.SharedKernel.Primitives;
using JobTracker.SharedKernel.Results;

namespace Jobs.Domain.Jobs;

public sealed class Job : AggregateRoot<JobId>
{
    private readonly List<JobPhoto> _photos = new();

    public OrganizationId OrganizationId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public JobStatus Status { get; private set; }
    public DateTimeOffset? ScheduledDate { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? SignatureUrl { get; private set; }
    public AssigneeId? AssigneeId { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    public IReadOnlyCollection<JobPhoto> Photos => _photos.AsReadOnly();

    private Job() { }

    public static Result<Job> Create(
        OrganizationId organizationId,
        string? title,
        string? description,
        Address address,
        CustomerId customerId,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            return JobErrors.InvalidTitle;
        if (description is { Length: > 4000 })
            return JobErrors.DescriptionTooLong;

        var job = new Job
        {
            Id = JobId.New(),
            OrganizationId = organizationId,
            Title = title!.Trim(),
            Description = (description ?? string.Empty).Trim(),
            Address = address,
            Status = JobStatus.Draft,
            CustomerId = customerId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
        };

        job.RaiseDomainEvent(new JobCreatedDomainEvent(
            job.Id,
            job.OrganizationId,
            job.CustomerId,
            nowUtc));

        return job;
    }

    public Result Schedule(DateTimeOffset scheduledDate, AssigneeId assigneeId, DateTimeOffset nowUtc)
    {
        if (Status is not JobStatus.Draft)
            return Result.Failure(JobErrors.InvalidTransition(Status, JobStatus.Scheduled));
        if (scheduledDate <= nowUtc)
            return Result.Failure(JobErrors.CannotScheduleInPast);

        Status = JobStatus.Scheduled;
        ScheduledDate = scheduledDate;
        AssigneeId = assigneeId;
        UpdatedAt = nowUtc;

        RaiseDomainEvent(new JobScheduledDomainEvent(
            Id, OrganizationId, assigneeId, scheduledDate, nowUtc));

        return Result.Success();
    }

    public Result Start(DateTimeOffset nowUtc)
    {
        if (Status is not JobStatus.Scheduled)
            return Result.Failure(JobErrors.InvalidTransition(Status, JobStatus.InProgress));

        Status = JobStatus.InProgress;
        StartedAt = nowUtc;
        UpdatedAt = nowUtc;

        RaiseDomainEvent(new JobStartedDomainEvent(Id, OrganizationId, nowUtc, nowUtc));
        return Result.Success();
    }

    public Result<JobPhotoId> AddPhoto(string? url, DateTimeOffset capturedAt, string? caption)
    {
        if (Status is JobStatus.Completed or JobStatus.Cancelled)
            return JobErrors.CannotAddPhotoToTerminalJob;
        if (string.IsNullOrWhiteSpace(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
            return JobErrors.InvalidPhotoUrl;
        if (caption is { Length: > 500 })
            return JobErrors.CaptionTooLong;

        var photoId = JobPhotoId.New();
        _photos.Add(new JobPhoto(photoId, Id, url!, capturedAt, caption));
        UpdatedAt = capturedAt;
        return photoId;
    }

    public Result Complete(string? signatureUrl, DateTimeOffset nowUtc)
    {
        if (Status is not JobStatus.InProgress)
            return Result.Failure(JobErrors.InvalidTransition(Status, JobStatus.Completed));
        if (string.IsNullOrWhiteSpace(signatureUrl))
            return Result.Failure(JobErrors.SignatureRequired);
        if (!Uri.IsWellFormedUriString(signatureUrl, UriKind.Absolute))
            return Result.Failure(JobErrors.InvalidSignatureUrl);

        Status = JobStatus.Completed;
        SignatureUrl = signatureUrl;
        CompletedAt = nowUtc;
        UpdatedAt = nowUtc;

        RaiseDomainEvent(new JobCompletedDomainEvent(
            Id,
            OrganizationId,
            CustomerId,
            AssigneeId!.Value,
            StartedAt!.Value,
            nowUtc,
            signatureUrl!,
            nowUtc));
        return Result.Success();
    }

    public Result Cancel(string? reason, DateTimeOffset nowUtc)
    {
        if (Status is JobStatus.Completed or JobStatus.Cancelled)
            return Result.Failure(JobErrors.InvalidTransition(Status, JobStatus.Cancelled));
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return Result.Failure(JobErrors.InvalidCancellationReason);

        Status = JobStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledAt = nowUtc;
        UpdatedAt = nowUtc;

        RaiseDomainEvent(new JobCancelledDomainEvent(Id, OrganizationId, reason.Trim(), nowUtc));
        return Result.Success();
    }
}
