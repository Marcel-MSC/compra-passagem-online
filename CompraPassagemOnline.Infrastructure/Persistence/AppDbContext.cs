using CompraPassagemOnline.Domain.Entities;
using CompraPassagemOnline.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CompraPassagemOnline.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Origin).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Destination).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.HasIndex(x => new { x.Origin, x.Destination, x.DepartureAt });
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SeatNumber).HasMaxLength(10).IsRequired();
            entity.HasIndex(x => new { x.TripId, x.SeatNumber }).IsUnique();
            entity.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TotalAmount).HasPrecision(10, 2);
            entity.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId);
        });

        modelBuilder.Entity<Passenger>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DocumentNumber).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TicketCode).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.TicketCode).IsUnique();
        });
    }
}
