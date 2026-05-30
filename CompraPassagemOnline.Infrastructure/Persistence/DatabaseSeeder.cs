using CompraPassagemOnline.Domain.Entities;
using CompraPassagemOnline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CompraPassagemOnline.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await context.Database.MigrateAsync(cancellationToken);

        if (await context.Trips.AnyAsync(cancellationToken))
            return;

        var trip = new Trip
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Origin = "Sao Paulo",
            Destination = "Rio de Janeiro",
            DepartureAt = DateTime.UtcNow.Date.AddDays(1).AddHours(8),
            ArrivalAt = DateTime.UtcNow.Date.AddDays(1).AddHours(14),
            Price = 189.90m,
            Capacity = 40
        };

        for (var i = 1; i <= 40; i++)
        {
            trip.Seats.Add(new Seat
            {
                Id = Guid.NewGuid(),
                TripId = trip.Id,
                SeatNumber = i.ToString("D2"),
                Status = Domain.Enums.SeatStatus.Available
            });
        }

        context.Trips.Add(trip);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed concluído com viagem {TripId}", trip.Id);
    }
}
