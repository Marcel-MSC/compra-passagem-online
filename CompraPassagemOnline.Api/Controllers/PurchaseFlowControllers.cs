using CompraPassagemOnline.Application.Booking;
using CompraPassagemOnline.Application.Inventory;
using CompraPassagemOnline.Application.Payments;
using CompraPassagemOnline.Application.Search;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CompraPassagemOnline.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class TripsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TripsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("trips")]
    public async Task<ActionResult<IReadOnlyList<TripDto>>> Search(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SearchTripsQuery(from, to, date), cancellationToken);
        return Ok(result);
    }

    [HttpGet("trips/{tripId:guid}/seats")]
    public async Task<ActionResult<IReadOnlyList<SeatDto>>> GetSeats(Guid tripId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSeatMapQuery(tripId), cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Route("api")]
public sealed class ReservationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReservationsController(IMediator mediator) => _mediator = mediator;

    public sealed record CreateReservationRequest(Guid TripId, Guid SeatId, string UserId);

    [HttpPost("reservations")]
    public async Task<ActionResult<ReservationDto>> Create(
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CreateReservationCommand(request.TripId, request.SeatId, request.UserId),
            cancellationToken);
        return Created($"/api/reservations/{result.Id}", result);
    }
}

[ApiController]
[Route("api")]
public sealed class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator) => _mediator = mediator;

    public sealed record CreateOrderRequest(Guid ReservationId, IReadOnlyList<PassengerInput> Passengers);

    [HttpPost("orders")]
    public async Task<ActionResult<OrderDto>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateOrderCommand(request.ReservationId, request.Passengers), cancellationToken);
        return Created($"/api/orders/{result.Id}", result);
    }
}

[ApiController]
[Route("api")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator) => _mediator = mediator;

    public sealed record ProcessPaymentRequest(Guid OrderId, string IdempotencyKey, bool SimulateFailure = false);

    [HttpPost("payments")]
    public async Task<ActionResult<PaymentResultDto>> Process(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ProcessPaymentCommand(request.OrderId, request.IdempotencyKey, request.SimulateFailure),
            cancellationToken);
        return Ok(result);
    }
}
