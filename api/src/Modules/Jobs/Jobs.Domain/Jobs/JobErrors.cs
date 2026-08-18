using Jobs.Domain.Common;
using JobTracker.SharedKernel.Results;

namespace Jobs.Domain.Jobs;

public static class JobErrors
{
    public static readonly Error InvalidTitle =
        Error.Validation("Job.InvalidTitle", "Title must be non-empty and at most 200 characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("Job.DescriptionTooLong", "Description must be at most 4000 characters.");

    public static readonly Error CannotScheduleInPast =
        Error.Conflict("Job.CannotScheduleInPast", "Scheduled date must be strictly in the future.");

    public static Error InvalidTransition(JobStatus from, JobStatus to) =>
        Error.Conflict("Job.InvalidTransition", $"Cannot transition from {from} to {to}.");

    public static readonly Error SignatureRequired =
        Error.Validation("Job.SignatureRequired", "A signature URL is required to complete a job.");

    public static readonly Error InvalidSignatureUrl =
        Error.Validation("Job.InvalidSignatureUrl", "Signature URL must be an absolute URI.");

    public static readonly Error InvalidCancellationReason =
        Error.Validation("Job.InvalidCancellationReason", "Cancellation reason must be non-empty and at most 500 characters.");

    public static readonly Error InvalidPhotoUrl =
        Error.Validation("Job.InvalidPhotoUrl", "Photo URL must be an absolute URI.");

    public static readonly Error CaptionTooLong =
        Error.Validation("Job.CaptionTooLong", "Caption must be at most 500 characters.");

    public static readonly Error CannotAddPhotoToTerminalJob =
        Error.Conflict("Job.CannotAddPhotoToTerminalJob", "Cannot add photos to a completed or cancelled job.");

    public static Error NotFound(JobId id) =>
        Error.NotFound("Job.NotFound", $"Job with id {id} was not found.");
}
