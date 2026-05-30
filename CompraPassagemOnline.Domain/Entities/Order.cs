using CompraPassagemOnline.Domain.Enums;

namespace CompraPassagemOnline.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.AwaitingPayment;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Reservation? Reservation { get; set; }
    public ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
    public Payment? Payment { get; set; }
    public Ticket? Ticket { get; set; }
}
