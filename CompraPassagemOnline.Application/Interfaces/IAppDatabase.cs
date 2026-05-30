using CompraPassagemOnline.Domain.Entities;
using CompraPassagemOnline.Domain.Enums;

namespace CompraPassagemOnline.Application.Interfaces;

public interface IAppDatabase
{
    Task<IReadOnlyList<Trip>> SearchTripsAsync(string origin, string destination, DateOnly date, CancellationToken cancellationToken);
    Task<Trip?> GetTripAsync(Guid tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Seat>> GetSeatsByTripAsync(Guid tripId, CancellationToken cancellationToken);
    Task<Seat?> GetSeatAsync(Guid seatId, CancellationToken cancellationToken);
    Task<bool> TryHoldSeatAsync(Guid seatId, CancellationToken cancellationToken);
    Task ReleaseSeatAsync(Guid seatId, CancellationToken cancellationToken);
    Task MarkSeatSoldAsync(Guid seatId, CancellationToken cancellationToken);
    Task AddReservationAsync(Reservation reservation, CancellationToken cancellationToken);
    Task<Reservation?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Reservation>> GetExpiredReservationsAsync(DateTime utcNow, CancellationToken cancellationToken);
    Task AddOrderAsync(Order order, CancellationToken cancellationToken);
    Task<Order?> GetOrderWithDetailsAsync(Guid orderId, CancellationToken cancellationToken);
    Task<Payment?> GetPaymentByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken);
    Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
