namespace Jobs.Application.Jobs.Queries;

public sealed record JobAddressDto(
    string Street,
    string City,
    string State,
    string ZipCode,
    decimal? Latitude,
    decimal? Longitude);

public sealed record JobPhotoDto(
    Guid Id,
    string Url,
    DateTimeOffset CapturedAt,
    string? Caption);

public sealed record JobDetailsDto(
    Guid Id,
    string Title,
    string Description,
    JobAddressDto Address,
    string Status,
    DateTimeOffset? ScheduledDate,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    string? SignatureUrl,
    Guid? AssigneeId,
    Guid CustomerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<JobPhotoDto> Photos);

public sealed record JobListItemDto(
    Guid Id,
    string Title,
    string Status,
    Guid CustomerId,
    Guid? AssigneeId,
    DateTimeOffset? ScheduledDate,
    DateTimeOffset CreatedAt);
