using CompraPassagemOnline.Application.Booking;
using CompraPassagemOnline.Application.Common;
using CompraPassagemOnline.Application.Events;
using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Application.Payments;
using CompraPassagemOnline.Application.Ticketing;
using CompraPassagemOnline.Domain.Entities;
using CompraPassagemOnline.Domain.Enums;
using MassTransit;
using MediatR;

namespace CompraPassagemOnline.Application.Payments;

public sealed class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, PaymentResultDto>
{
    private readonly IAppDatabase _database;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IMediator _mediator;
    private readonly IPublishEndpoint _publishEndpoint;

    public ProcessPaymentCommandHandler(
        IAppDatabase database,
        IPaymentGateway paymentGateway,
        IMediator mediator,
        IPublishEndpoint publishEndpoint)
    {
        _database = database;
        _paymentGateway = paymentGateway;
        _mediator = mediator;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<PaymentResultDto> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var existing = await _database.GetPaymentByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return MapExistingPayment(existing);

        var order = await _database.GetOrderWithDetailsAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException($"Pedido {request.OrderId} não encontrado.");

        if (order.Status != OrderStatus.AwaitingPayment)
            throw new InvalidOperationDomainException("Pedido não está aguardando pagamento.");

        var reservation = order.Reservation
            ?? throw new InvalidOperationDomainException("Pedido sem reserva associada.");

        if (reservation.Status != ReservationStatus.Active || reservation.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationDomainException("Reserva expirada. Selecione o assento novamente.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            IdempotencyKey = request.IdempotencyKey,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _database.AddPaymentAsync(payment, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);

        PaymentGatewayResult gatewayResult;
        if (request.SimulateFailure)
        {
            gatewayResult = new PaymentGatewayResult(false, null, "Pagamento recusado pelo emissor.");
        }
        else
        {
            gatewayResult = await _paymentGateway.ChargeAsync(order.TotalAmount, request.IdempotencyKey, cancellationToken);
        }

        payment.ProcessedAt = DateTime.UtcNow;

        if (!gatewayResult.Success)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = gatewayResult.FailureReason;
            await _database.SaveChangesAsync(cancellationToken);

            await _publishEndpoint.Publish(new PaymentFailedEvent(order.Id, payment.Id, payment.FailureReason), cancellationToken);
            await _mediator.Send(new CompensateOrderCommand(order.Id), cancellationToken);

            return new PaymentResultDto(payment.Id, payment.Status.ToString(), payment.FailureReason, null, null);
        }

        payment.Status = PaymentStatus.Confirmed;
        payment.GatewayTransactionId = gatewayResult.TransactionId;
        order.Status = OrderStatus.Paid;
        reservation.Status = ReservationStatus.Converted;

        await _database.SaveChangesAsync(cancellationToken);
        await _publishEndpoint.Publish(new PaymentConfirmedEvent(order.Id, payment.Id), cancellationToken);

        var ticket = await _mediator.Send(new IssueTicketCommand(order.Id), cancellationToken);

        return new PaymentResultDto(
            payment.Id,
            payment.Status.ToString(),
            null,
            ticket.Id,
            ticket.TicketCode);
    }

    private static PaymentResultDto MapExistingPayment(Payment payment) =>
        new(
            payment.Id,
            payment.Status.ToString(),
            payment.FailureReason,
            payment.Order?.Ticket?.Id,
            payment.Order?.Ticket?.TicketCode);
}
