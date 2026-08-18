using Jobs.Application.Customers.Commands.CreateCustomer;
using Jobs.Application.Customers.Queries;
using Jobs.Application.Customers.Queries.GetCustomersByIds;
using Jobs.Application.Customers.Queries.ListCustomers;
using Jobs.Presentation.Contracts;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Presentation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jobs.Presentation.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Produces("application/json")]
public sealed class CustomersController(ISender sender, IUnitOfWork uow) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int take = 20,
        CancellationToken ct = default) =>
        ToActionResult(await sender.Send(new ListCustomersQuery(q, take), ct));

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest body, CancellationToken ct)
    {
        var result = await sender.Send(new CreateCustomerCommand(body.Name, body.Email, body.Phone), ct);
        if (result.IsSuccess) await uow.SaveChangesAsync(ct);
        return result.IsSuccess
            ? Created($"/api/v1/customers/{result.Value}", new { id = result.Value })
            : ToActionResult(result);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Batch([FromBody] GetCustomersByIdsRequest body, CancellationToken ct) =>
        ToActionResult(await sender.Send(new GetCustomersByIdsQuery(body.Ids ?? Array.Empty<Guid>()), ct));
}
