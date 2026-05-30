namespace CompraPassagemOnline.Application.Interfaces;

public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ChargeAsync(decimal amount, string idempotencyKey, CancellationToken cancellationToken);
}

public sealed record PaymentGatewayResult(bool Success, string? TransactionId, string? FailureReason);
