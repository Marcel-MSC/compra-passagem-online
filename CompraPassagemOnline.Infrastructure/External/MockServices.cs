using CompraPassagemOnline.Application.Interfaces;
using Polly;
using Polly.Retry;

namespace CompraPassagemOnline.Infrastructure.External;

public sealed class MockPaymentGateway : IPaymentGateway
{
    private readonly AsyncRetryPolicy<PaymentGatewayResult> _retryPolicy;

    public MockPaymentGateway()
    {
        _retryPolicy = Policy<PaymentGatewayResult>
            .HandleResult(r => !r.Success && r.FailureReason == "Gateway timeout")
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt));
    }

    public Task<PaymentGatewayResult> ChargeAsync(decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        return _retryPolicy.ExecuteAsync(() =>
        {
            var transactionId = $"txn_{idempotencyKey[..Math.Min(8, idempotencyKey.Length)]}_{Guid.NewGuid():N}"[..24];
            return Task.FromResult(new PaymentGatewayResult(true, transactionId, null));
        });
    }
}

public sealed class MockTicketIssuer : ITicketIssuer
{
    public Task<string> IssueTicketCodeAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var code = $"TKT-{orderId.ToString("N")[..8].ToUpperInvariant()}";
        return Task.FromResult(code);
    }
}
