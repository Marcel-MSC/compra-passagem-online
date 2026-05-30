using CompraPassagemOnline.Domain.Enums;
using MediatR;

namespace CompraPassagemOnline.Application.Search;

public sealed record SearchTripsQuery(string Origin, string Destination, DateOnly Date) : IRequest<IReadOnlyList<TripDto>>;

public sealed record TripDto(
    Guid Id,
    string Origin,
    string Destination,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    decimal Price,
    int AvailableSeats);
