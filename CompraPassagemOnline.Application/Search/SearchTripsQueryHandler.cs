using CompraPassagemOnline.Application.Common;
using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Application.Search;
using MediatR;

namespace CompraPassagemOnline.Application.Search;

public sealed class SearchTripsQueryHandler : IRequestHandler<SearchTripsQuery, IReadOnlyList<TripDto>>
{
    private readonly IAppDatabase _database;
    private readonly ISearchCacheService _cache;

    public SearchTripsQueryHandler(IAppDatabase database, ISearchCacheService cache)
    {
        _database = database;
        _cache = cache;
    }

    public async Task<IReadOnlyList<TripDto>> Handle(SearchTripsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"search:{request.Origin}:{request.Destination}:{request.Date:yyyy-MM-dd}";
        var cached = await _cache.GetAsync<IReadOnlyList<TripDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var trips = await _database.SearchTripsAsync(request.Origin, request.Destination, request.Date, cancellationToken);
        var result = trips.Select(t => new TripDto(
            t.Id,
            t.Origin,
            t.Destination,
            t.DepartureAt,
            t.ArrivalAt,
            t.Price,
            t.Seats.Count(s => s.Status == Domain.Enums.SeatStatus.Available))).ToList();

        await _cache.SetAsync(cacheKey, result, ReservationOptions.SearchCacheTtl, cancellationToken);
        return result;
    }
}
