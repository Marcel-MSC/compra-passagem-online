using MediatR;

namespace CompraPassagemOnline.Application.Ticketing;

public sealed record IssueTicketCommand(Guid OrderId) : IRequest<TicketDto>;

public sealed record TicketDto(Guid Id, Guid OrderId, string TicketCode, DateTime IssuedAt);

public sealed record ExpireReservationCommand(Guid ReservationId) : IRequest;
