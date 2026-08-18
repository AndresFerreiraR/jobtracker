using Jobs.Application.Jobs.Commands.AddJobPhoto;
using Jobs.Application.Jobs.Commands.CancelJob;
using Jobs.Application.Jobs.Commands.CompleteJob;
using Jobs.Application.Jobs.Commands.CreateJob;
using Jobs.Application.Jobs.Commands.ScheduleJob;
using Jobs.Application.Jobs.Commands.StartJob;
using Jobs.Application.Jobs.Queries;
using Jobs.Application.Jobs.Queries.GetJobById;
using Jobs.Application.Jobs.Queries.ListJobs;
using Jobs.Presentation.Contracts;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Pagination;
using JobTracker.BuildingBlocks.Presentation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jobs.Presentation.Controllers;

[ApiController]
[Route("api/v1/jobs")]
[Produces("application/json")]
public sealed class JobsController(ISender sender, IUnitOfWork uow) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest body, CancellationToken ct)
    {
        var cmd = new CreateJobCommand(
            body.Title,
            body.Description,
            new AddressDto(
                body.Address.Street,
                body.Address.City,
                body.Address.State,
                body.Address.ZipCode,
                body.Address.Latitude,
                body.Address.Longitude),
            body.CustomerId);

        var result = await sender.Send(cmd, ct);
        if (result.IsSuccess) await uow.SaveChangesAsync(ct);
        return result.IsSuccess
            ? Created($"/api/v1/jobs/{result.Value}", new { id = result.Value })
            : ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        ToActionResult(await sender.Send(new GetJobByIdQuery(id), ct));

    [HttpGet]
    [ProducesResponseType(typeof(PagedList<JobListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] Guid? assigneeId,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTimeOffset? scheduledFrom,
        [FromQuery] DateTimeOffset? scheduledTo,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        ToActionResult(await sender.Send(
            new ListJobsQuery(status, assigneeId, customerId, scheduledFrom, scheduledTo, cursor, pageSize),
            ct));

    [HttpPost("{id:guid}/schedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Schedule(Guid id, [FromBody] ScheduleJobRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new ScheduleJobCommand(id, body.ScheduledDate, body.AssigneeId), ct);
        if (result.IsSuccess) await uow.SaveChangesAsync(ct);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new StartJobCommand(id), ct);
        if (result.IsSuccess) await uow.SaveChangesAsync(ct);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/photos")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddPhoto(Guid id, [FromBody] AddJobPhotoRequest body, CancellationToken ct)
    {
        var result = await sender.Send(
            new AddJobPhotoCommand(id, body.Url, body.CapturedAt, body.Caption),
            ct);

        if (result.IsSuccess) await uow.SaveChangesAsync(ct);
        return result.IsSuccess
            ? Created($"/api/v1/jobs/{id}/photos/{result.Value}", new { id = result.Value })
            : ToActionResult(result);
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteJobRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new CompleteJobCommand(id, body.SignatureUrl), ct);
        if (result.IsSuccess) await uow.SaveChangesAsync(ct);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelJobRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new CancelJobCommand(id, body.Reason), ct);
        if (result.IsSuccess) await uow.SaveChangesAsync(ct);
        return ToActionResult(result);
    }
}
