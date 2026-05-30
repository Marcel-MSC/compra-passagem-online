using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Application.Inventory;
using CompraPassagemOnline.Application.Common;
using MediatR;

namespace CompraPassagemOnline.Application.Inventory;

public sealed class GetSeatMapQueryHandler : IRequestHandler<GetSeatMapQuery, IReadOnlyList<SeatDto>>
{
    private readonly IAppDatabase _database;

    public GetSeatMapQueryHandler(IAppDatabase database) => _database = database;

    public async Task<IReadOnlyList<SeatDto>> Handle(GetSeatMapQuery request, CancellationToken cancellationToken)
    {
        var trip = await _database.GetTripAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Viagem {request.TripId} não encontrada.");

        var seats = await _database.GetSeatsByTripAsync(request.TripId, cancellationToken);
        return seats.Select(s => new SeatDto(s.Id, s.SeatNumber, s.Status)).ToList();
    }
}
