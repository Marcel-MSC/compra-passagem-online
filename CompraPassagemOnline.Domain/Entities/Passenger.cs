namespace CompraPassagemOnline.Domain.Entities;

public class Passenger
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public Order? Order { get; set; }
}
