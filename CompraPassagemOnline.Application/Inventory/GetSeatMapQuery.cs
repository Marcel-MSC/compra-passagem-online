using CompraPassagemOnline.Domain.Enums;
using MediatR;

namespace CompraPassagemOnline.Application.Inventory;

public sealed record GetSeatMapQuery(Guid TripId) : IRequest<IReadOnlyList<SeatDto>>;

public sealed record SeatDto(Guid Id, string SeatNumber, SeatStatus Status);
