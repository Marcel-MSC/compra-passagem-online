using CompraPassagemOnline.Domain.Enums;

namespace CompraPassagemOnline.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Issued;
    public DateTime IssuedAt { get; set; }
    public Order? Order { get; set; }
}
