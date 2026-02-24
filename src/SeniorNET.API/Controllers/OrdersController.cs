using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeniorNET.Application.Commands.CancelOrder;
using SeniorNET.Application.Commands.CreateOrder;
using SeniorNET.Application.DTOs;
using SeniorNET.Application.Queries.GetOrder;
using SeniorNET.Application.Queries.GetOrders;

namespace SeniorNET.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender _sender;

    public OrdersController(ISender sender) => _sender = sender;

    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrderCommand command, CancellationToken ct)
    {
        var order = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await _sender.Send(new GetOrderQuery(id), ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetOrdersQuery(page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _sender.Send(new CancelOrderCommand(id), ct);
        return NoContent();
    }
}
