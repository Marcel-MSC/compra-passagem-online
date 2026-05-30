namespace CompraPassagemOnline.Application.Events;

public sealed record SeatHeldEvent(Guid ReservationId, Guid TripId, Guid SeatId, string UserId, DateTime ExpiresAt);
public sealed record SeatReleasedEvent(Guid ReservationId, Guid TripId, Guid SeatId);
public sealed record PaymentConfirmedEvent(Guid OrderId, Guid PaymentId);
public sealed record PaymentFailedEvent(Guid OrderId, Guid PaymentId, string? Reason);
public sealed record TicketIssuedEvent(Guid OrderId, Guid TicketId, string TicketCode);
public sealed record ReservationExpiredEvent(Guid ReservationId, Guid TripId, Guid SeatId);
