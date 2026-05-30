using CompraPassagemOnline.Domain.Enums;

namespace CompraPassagemOnline.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid SeatId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Trip? Trip { get; set; }
    public Seat? Seat { get; set; }
}
