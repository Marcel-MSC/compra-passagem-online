using CompraPassagemOnline.Domain.Entities;
using CompraPassagemOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CompraPassagemOnline.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static readonly Guid SeedTripId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public const int SeedSeatCount = 40;

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await context.Database.MigrateAsync(cancellationToken);

        var departureAt = DateTime.UtcNow.Date.AddDays(1).AddHours(8);
        var arrivalAt = DateTime.UtcNow.Date.AddDays(1).AddHours(14);

        var existingTrip = await context.Trips
            .Include(t => t.Seats)
            .FirstOrDefaultAsync(t => t.Id == SeedTripId, cancellationToken);

        if (existingTrip is null)
        {
            var trip = CreateSeedTrip(departureAt, arrivalAt);
            context.Trips.Add(trip);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seed concluido com viagem {TripId}", trip.Id);
            return;
        }

        existingTrip.DepartureAt = departureAt;
        existingTrip.ArrivalAt = arrivalAt;

        if (existingTrip.Seats.Count == 0)
        {
            for (var i = 1; i <= SeedSeatCount; i++)
            {
                existingTrip.Seats.Add(CreateSeat(existingTrip.Id, i));
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Viagem seed {TripId} atualizada para {DepartureAt:yyyy-MM-dd} ({SeatCount} assentos)",
            SeedTripId,
            departureAt,
            existingTrip.Seats.Count);
    }

    private static Trip CreateSeedTrip(DateTime departureAt, DateTime arrivalAt)
    {
        var trip = new Trip
        {
            Id = SeedTripId,
            Origin = "Sao Paulo",
            Destination = "Rio de Janeiro",
            DepartureAt = departureAt,
            ArrivalAt = arrivalAt,
            Price = 189.90m,
            Capacity = SeedSeatCount
        };

        for (var i = 1; i <= SeedSeatCount; i++)
            trip.Seats.Add(CreateSeat(trip.Id, i));

        return trip;
    }

    private static Seat CreateSeat(Guid tripId, int seatNumber) =>
        new()
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            SeatNumber = seatNumber.ToString("D2"),
            Status = Domain.Enums.SeatStatus.Available
        };
}
