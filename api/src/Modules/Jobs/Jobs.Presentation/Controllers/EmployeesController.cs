using Jobs.Application.Employees.Commands.CreateEmployee;
using Jobs.Application.Employees.Queries;
using Jobs.Application.Employees.Queries.GetEmployeesByIds;
using Jobs.Application.Employees.Queries.ListEmployees;
using Jobs.Presentation.Contracts;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Presentation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jobs.Presentation.Controllers;

[ApiController]
[Route("api/v1/employees")]
[Produces("application/json")]
public sealed class EmployeesController(ISender sender, IUnitOfWork uow) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int take = 20,
        CancellationToken ct = default) =>
        ToActionResult(await sender.Send(new ListEmployeesQuery(q, take), ct));

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new CreateEmployeeCommand(body.Name, body.Email, body.Phone), ct);
        if (result.IsSuccess) await uow.SaveChangesAsync(ct);
        return result.IsSuccess
            ? Created($"/api/v1/employees/{result.Value}", new { id = result.Value })
            : ToActionResult(result);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Batch([FromBody] GetEmployeesByIdsRequest body, CancellationToken ct) =>
        ToActionResult(await sender.Send(new GetEmployeesByIdsQuery(body.Ids ?? Array.Empty<Guid>()), ct));
}
