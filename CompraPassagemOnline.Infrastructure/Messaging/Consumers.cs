using CompraPassagemOnline.Application.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CompraPassagemOnline.Infrastructure.Messaging;

public sealed class PaymentFailedConsumer : IConsumer<PaymentFailedEvent>
{
    private readonly ILogger<PaymentFailedConsumer> _logger;

    public PaymentFailedConsumer(ILogger<PaymentFailedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        _logger.LogWarning("Pagamento falhou para pedido {OrderId}: {Reason}", context.Message.OrderId, context.Message.Reason);
        return Task.CompletedTask;
    }
}

public sealed class PaymentConfirmedConsumer : IConsumer<PaymentConfirmedEvent>
{
    private readonly ILogger<PaymentConfirmedConsumer> _logger;

    public PaymentConfirmedConsumer(ILogger<PaymentConfirmedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<PaymentConfirmedEvent> context)
    {
        _logger.LogInformation("Pagamento confirmado para pedido {OrderId}", context.Message.OrderId);
        return Task.CompletedTask;
    }
}

public sealed class TicketIssuedConsumer : IConsumer<TicketIssuedEvent>
{
    private readonly ILogger<TicketIssuedConsumer> _logger;

    public TicketIssuedConsumer(ILogger<TicketIssuedConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<TicketIssuedEvent> context)
    {
        _logger.LogInformation(
            "Bilhete {TicketCode} emitido para pedido {OrderId}",
            context.Message.TicketCode,
            context.Message.OrderId);
        return Task.CompletedTask;
    }
}

public sealed class ReservationExpiredConsumer : IConsumer<ReservationExpiredEvent>
{
    private readonly ILogger<ReservationExpiredConsumer> _logger;

    public ReservationExpiredConsumer(ILogger<ReservationExpiredConsumer> logger) => _logger = logger;

    public Task Consume(ConsumeContext<ReservationExpiredEvent> context)
    {
        _logger.LogInformation("Reserva {ReservationId} expirada", context.Message.ReservationId);
        return Task.CompletedTask;
    }
}
