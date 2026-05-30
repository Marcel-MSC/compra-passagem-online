using CompraPassagemOnline.Domain.Enums;

namespace CompraPassagemOnline.Domain.Entities;

public class Seat
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    public int Version { get; set; }
    public Trip? Trip { get; set; }
}
