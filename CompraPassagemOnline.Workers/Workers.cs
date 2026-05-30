using CompraPassagemOnline.Application;
using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Application.Ticketing;
using CompraPassagemOnline.Infrastructure;
using CompraPassagemOnline.Infrastructure.Persistence;
using MediatR;

namespace CompraPassagemOnline.Workers;

public sealed class ReservationExpiryWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReservationExpiryWorker> _logger;

    public ReservationExpiryWorker(IServiceProvider services, ILogger<ReservationExpiryWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<IAppDatabase>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var expired = await database.GetExpiredReservationsAsync(DateTime.UtcNow, stoppingToken);
                foreach (var reservation in expired)
                {
                    await mediator.Send(new ExpireReservationCommand(reservation.Id), stoppingToken);
                    _logger.LogInformation("Reserva expirada processada: {ReservationId}", reservation.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao expirar reservas");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

public sealed class PaymentReconcileWorker : BackgroundService
{
    private readonly ILogger<PaymentReconcileWorker> _logger;

    public PaymentReconcileWorker(ILogger<PaymentReconcileWorker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Reconciliação de pagamentos pendentes executada");
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
