using MediatR;

namespace CompraPassagemOnline.Application.Payments;

public sealed record ProcessPaymentCommand(Guid OrderId, string IdempotencyKey, bool SimulateFailure = false) : IRequest<PaymentResultDto>;

public sealed record PaymentResultDto(Guid PaymentId, string Status, string? FailureReason, Guid? TicketId, string? TicketCode);
