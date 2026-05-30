using CompraPassagemOnline.Application.Booking;
using CompraPassagemOnline.Application.Common;
using CompraPassagemOnline.Application.Events;
using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Domain.Entities;
using CompraPassagemOnline.Domain.Enums;
using MassTransit;
using MediatR;

namespace CompraPassagemOnline.Application.Booking;

public sealed class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, ReservationDto>
{
    private readonly IAppDatabase _database;
    private readonly ISeatLockService _seatLock;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreateReservationCommandHandler(
        IAppDatabase database,
        ISeatLockService seatLock,
        IPublishEndpoint publishEndpoint)
    {
        _database = database;
        _seatLock = seatLock;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<ReservationDto> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        var seat = await _database.GetSeatAsync(request.SeatId, cancellationToken)
            ?? throw new NotFoundException($"Assento {request.SeatId} não encontrado.");

        if (seat.TripId != request.TripId)
            throw new InvalidOperationDomainException("Assento não pertence à viagem informada.");

        var lockAcquired = await _seatLock.TryAcquireLockAsync(
            request.TripId,
            request.SeatId,
            request.UserId,
            ReservationOptions.HoldDuration,
            cancellationToken);

        if (!lockAcquired)
            throw new SeatConflictException("Assento indisponível ou já reservado por outro usuário.");

        try
        {
            var held = await _database.TryHoldSeatAsync(request.SeatId, cancellationToken);
            if (!held)
            {
                await _seatLock.ReleaseLockAsync(request.TripId, request.SeatId, request.UserId, cancellationToken);
                throw new SeatConflictException("Assento indisponível ou já reservado por outro usuário.");
            }

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                TripId = request.TripId,
                SeatId = request.SeatId,
                UserId = request.UserId,
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(ReservationOptions.HoldDuration)
            };

            await _database.AddReservationAsync(reservation, cancellationToken);
            await _database.SaveChangesAsync(cancellationToken);

            await _publishEndpoint.Publish(
                new SeatHeldEvent(reservation.Id, reservation.TripId, reservation.SeatId, reservation.UserId, reservation.ExpiresAt),
                cancellationToken);

            return new ReservationDto(reservation.Id, reservation.TripId, reservation.SeatId, reservation.ExpiresAt);
        }
        catch
        {
            await _seatLock.ReleaseLockAsync(request.TripId, request.SeatId, request.UserId, cancellationToken);
            throw;
        }
    }
}

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly IAppDatabase _database;

    public CreateOrderCommandHandler(IAppDatabase database) => _database = database;

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _database.GetReservationAsync(request.ReservationId, cancellationToken)
            ?? throw new NotFoundException($"Reserva {request.ReservationId} não encontrada.");

        if (reservation.Status != ReservationStatus.Active || reservation.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationDomainException("Reserva expirada ou inválida.");

        var trip = await _database.GetTripAsync(reservation.TripId, cancellationToken)
            ?? throw new NotFoundException("Viagem não encontrada.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Status = OrderStatus.AwaitingPayment,
            TotalAmount = trip.Price,
            CreatedAt = DateTime.UtcNow,
            Passengers = request.Passengers.Select(p => new Passenger
            {
                Id = Guid.NewGuid(),
                FullName = p.FullName,
                DocumentNumber = p.DocumentNumber
            }).ToList()
        };

        foreach (var passenger in order.Passengers)
            passenger.OrderId = order.Id;

        await _database.AddOrderAsync(order, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);

        return new OrderDto(order.Id, order.ReservationId, order.TotalAmount, order.Status.ToString());
    }
}

public sealed class CompensateOrderCommandHandler : IRequestHandler<CompensateOrderCommand>
{
    private readonly IAppDatabase _database;
    private readonly ISeatLockService _seatLock;
    private readonly IPublishEndpoint _publishEndpoint;

    public CompensateOrderCommandHandler(
        IAppDatabase database,
        ISeatLockService seatLock,
        IPublishEndpoint publishEndpoint)
    {
        _database = database;
        _seatLock = seatLock;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Handle(CompensateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _database.GetOrderWithDetailsAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException($"Pedido {request.OrderId} não encontrado.");

        if (order.Status == OrderStatus.Cancelled)
            return;

        var reservation = order.Reservation
            ?? throw new InvalidOperationDomainException("Pedido sem reserva associada.");

        order.Status = OrderStatus.Cancelled;
        reservation.Status = ReservationStatus.Cancelled;

        await _database.ReleaseSeatAsync(reservation.SeatId, cancellationToken);
        await _seatLock.ReleaseLockAsync(reservation.TripId, reservation.SeatId, reservation.UserId, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(
            new SeatReleasedEvent(reservation.Id, reservation.TripId, reservation.SeatId),
            cancellationToken);
    }
}
