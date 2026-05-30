using CompraPassagemOnline.Application.Common;
using CompraPassagemOnline.Application.Events;
using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Application.Ticketing;
using CompraPassagemOnline.Domain.Entities;
using CompraPassagemOnline.Domain.Enums;
using MassTransit;
using MediatR;

namespace CompraPassagemOnline.Application.Ticketing;

public sealed class IssueTicketCommandHandler : IRequestHandler<IssueTicketCommand, TicketDto>
{
    private readonly IAppDatabase _database;
    private readonly ITicketIssuer _ticketIssuer;
    private readonly IPublishEndpoint _publishEndpoint;

    public IssueTicketCommandHandler(
        IAppDatabase database,
        ITicketIssuer ticketIssuer,
        IPublishEndpoint publishEndpoint)
    {
        _database = database;
        _ticketIssuer = ticketIssuer;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<TicketDto> Handle(IssueTicketCommand request, CancellationToken cancellationToken)
    {
        var order = await _database.GetOrderWithDetailsAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException($"Pedido {request.OrderId} não encontrado.");

        if (order.Ticket is not null)
            return new TicketDto(order.Ticket.Id, order.Ticket.OrderId, order.Ticket.TicketCode, order.Ticket.IssuedAt);

        var reservation = order.Reservation
            ?? throw new InvalidOperationDomainException("Pedido sem reserva associada.");

        var ticketCode = await _ticketIssuer.IssueTicketCodeAsync(order.Id, cancellationToken);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            TicketCode = ticketCode,
            Status = TicketStatus.Issued,
            IssuedAt = DateTime.UtcNow
        };

        await _database.AddTicketAsync(ticket, cancellationToken);
        await _database.MarkSeatSoldAsync(reservation.SeatId, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new TicketIssuedEvent(order.Id, ticket.Id, ticket.TicketCode), cancellationToken);

        return new TicketDto(ticket.Id, ticket.OrderId, ticket.TicketCode, ticket.IssuedAt);
    }
}

public sealed class ExpireReservationCommandHandler : IRequestHandler<ExpireReservationCommand>
{
    private readonly IAppDatabase _database;
    private readonly ISeatLockService _seatLock;
    private readonly IPublishEndpoint _publishEndpoint;

    public ExpireReservationCommandHandler(
        IAppDatabase database,
        ISeatLockService seatLock,
        IPublishEndpoint publishEndpoint)
    {
        _database = database;
        _seatLock = seatLock;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Handle(ExpireReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _database.GetReservationAsync(request.ReservationId, cancellationToken);
        if (reservation is null || reservation.Status != ReservationStatus.Active)
            return;

        if (reservation.ExpiresAt > DateTime.UtcNow)
            return;

        reservation.Status = ReservationStatus.Expired;
        await _database.ReleaseSeatAsync(reservation.SeatId, cancellationToken);
        await _seatLock.ReleaseLockAsync(reservation.TripId, reservation.SeatId, reservation.UserId, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(
            new ReservationExpiredEvent(reservation.Id, reservation.TripId, reservation.SeatId),
            cancellationToken);
        await _publishEndpoint.Publish(
            new SeatReleasedEvent(reservation.Id, reservation.TripId, reservation.SeatId),
            cancellationToken);
    }
}
