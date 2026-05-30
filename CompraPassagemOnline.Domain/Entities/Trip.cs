namespace CompraPassagemOnline.Domain.Entities;

public class Trip
{
    public Guid Id { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureAt { get; set; }
    public DateTime ArrivalAt { get; set; }
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
}
