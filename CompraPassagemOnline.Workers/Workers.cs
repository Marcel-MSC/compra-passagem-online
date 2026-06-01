using CompraPassagemOnline.Application.Common;
using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Application.Ticketing;
using MediatR;
using Microsoft.Extensions.Options;

namespace CompraPassagemOnline.Workers;

public sealed class ReservationExpiryWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReservationExpiryWorker> _logger;
    private readonly TimeSpan _interval;

    public ReservationExpiryWorker(
        IServiceProvider services,
        ILogger<ReservationExpiryWorker> logger,
        IOptions<ReservationOptions> options)
    {
        _services = services;
        _logger = logger;
        _interval = options.Value.ExpiryWorkerInterval;
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

            await Task.Delay(_interval, stoppingToken);
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
