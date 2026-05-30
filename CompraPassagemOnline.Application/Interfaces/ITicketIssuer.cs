namespace CompraPassagemOnline.Application.Interfaces;

public interface ITicketIssuer
{
    Task<string> IssueTicketCodeAsync(Guid orderId, CancellationToken cancellationToken);
}
