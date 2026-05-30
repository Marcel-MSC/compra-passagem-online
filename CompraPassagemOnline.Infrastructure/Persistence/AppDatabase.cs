using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Domain.Entities;
using CompraPassagemOnline.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CompraPassagemOnline.Infrastructure.Persistence;

public sealed class AppDatabase : IAppDatabase
{
    private readonly AppDbContext _context;

    public AppDatabase(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Trip>> SearchTripsAsync(
        string origin,
        string destination,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        return await _context.Trips
            .AsNoTracking()
            .Include(t => t.Seats)
            .Where(t =>
                t.Origin == origin &&
                t.Destination == destination &&
                t.DepartureAt >= start &&
                t.DepartureAt < end)
            .OrderBy(t => t.DepartureAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Trip?> GetTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

    public Task<IReadOnlyList<Seat>> GetSeatsByTripAsync(Guid tripId, CancellationToken cancellationToken) =>
        _context.Seats.AsNoTracking()
            .Where(s => s.TripId == tripId)
            .OrderBy(s => s.SeatNumber)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Seat>)t.Result, cancellationToken);

    public Task<Seat?> GetSeatAsync(Guid seatId, CancellationToken cancellationToken) =>
        _context.Seats.FirstOrDefaultAsync(s => s.Id == seatId, cancellationToken);

    public async Task<bool> TryHoldSeatAsync(Guid seatId, CancellationToken cancellationToken)
    {
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Seats"
             SET "Status" = {(int)SeatStatus.Held}, "Version" = "Version" + 1
             WHERE "Id" = {seatId} AND "Status" = {(int)SeatStatus.Available}
             """,
            cancellationToken);

        return rows > 0;
    }

    public async Task ReleaseSeatAsync(Guid seatId, CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Seats"
             SET "Status" = {(int)SeatStatus.Available}, "Version" = "Version" + 1
             WHERE "Id" = {seatId} AND "Status" = {(int)SeatStatus.Held}
             """,
            cancellationToken);
    }

    public async Task MarkSeatSoldAsync(Guid seatId, CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Seats"
             SET "Status" = {(int)SeatStatus.Sold}, "Version" = "Version" + 1
             WHERE "Id" = {seatId}
             """,
            cancellationToken);
    }

    public async Task AddReservationAsync(Reservation reservation, CancellationToken cancellationToken) =>
        await _context.Reservations.AddAsync(reservation, cancellationToken);

    public Task<Reservation?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken) =>
        _context.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

    public Task<IReadOnlyList<Reservation>> GetExpiredReservationsAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        _context.Reservations
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= utcNow)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<Reservation>)t.Result, cancellationToken);

    public async Task AddOrderAsync(Order order, CancellationToken cancellationToken) =>
        await _context.Orders.AddAsync(order, cancellationToken);

    public Task<Order?> GetOrderWithDetailsAsync(Guid orderId, CancellationToken cancellationToken) =>
        _context.Orders
            .Include(o => o.Reservation)
            .Include(o => o.Passengers)
            .Include(o => o.Payment)
            .Include(o => o.Ticket)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public Task<Payment?> GetPaymentByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        _context.Payments
            .Include(p => p.Order!)
            .ThenInclude(o => o.Ticket)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken) =>
        await _context.Payments.AddAsync(payment, cancellationToken);

    public async Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken) =>
        await _context.Tickets.AddAsync(ticket, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
