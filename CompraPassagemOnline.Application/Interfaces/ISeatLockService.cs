namespace CompraPassagemOnline.Application.Interfaces;

public interface ISeatLockService
{
    Task<bool> TryAcquireLockAsync(Guid tripId, Guid seatId, string ownerId, TimeSpan ttl, CancellationToken cancellationToken);
    Task ReleaseLockAsync(Guid tripId, Guid seatId, string ownerId, CancellationToken cancellationToken);
}
