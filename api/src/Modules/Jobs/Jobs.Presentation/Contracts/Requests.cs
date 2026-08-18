namespace Jobs.Presentation.Contracts;

public sealed record CreateJobRequest(
    string Title,
    string Description,
    AddressPayload Address,
    Guid CustomerId);

public sealed record AddressPayload(
    string Street,
    string City,
    string State,
    string ZipCode,
    decimal? Latitude,
    decimal? Longitude);

public sealed record ScheduleJobRequest(DateTimeOffset ScheduledDate, Guid AssigneeId);

public sealed record AddJobPhotoRequest(string Url, DateTimeOffset CapturedAt, string? Caption);

public sealed record CompleteJobRequest(string SignatureUrl);

public sealed record CancelJobRequest(string Reason);
