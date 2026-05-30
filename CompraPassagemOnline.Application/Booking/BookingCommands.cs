using MediatR;

namespace CompraPassagemOnline.Application.Booking;

public sealed record CreateReservationCommand(Guid TripId, Guid SeatId, string UserId) : IRequest<ReservationDto>;

public sealed record ReservationDto(Guid Id, Guid TripId, Guid SeatId, DateTime ExpiresAt);

public sealed record CreateOrderCommand(Guid ReservationId, IReadOnlyList<PassengerInput> Passengers) : IRequest<OrderDto>;

public sealed record PassengerInput(string FullName, string DocumentNumber);

public sealed record OrderDto(Guid Id, Guid ReservationId, decimal TotalAmount, string Status);

public sealed record CompensateOrderCommand(Guid OrderId) : IRequest;
