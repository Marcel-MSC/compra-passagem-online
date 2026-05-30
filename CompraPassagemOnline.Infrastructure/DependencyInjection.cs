using CompraPassagemOnline.Application.Interfaces;
using CompraPassagemOnline.Infrastructure.Caching;
using CompraPassagemOnline.Infrastructure.External;
using CompraPassagemOnline.Infrastructure.Messaging;
using CompraPassagemOnline.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CompraPassagemOnline.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgreSQL")));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") ?? "localhost:6379"));

        services.AddScoped<IAppDatabase, AppDatabase>();
        services.AddSingleton<ISeatLockService, RedisSeatLockService>();
        services.AddSingleton<ISearchCacheService, RedisSearchCacheService>();
        services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
        services.AddSingleton<ITicketIssuer, MockTicketIssuer>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<PaymentFailedConsumer>();
            x.AddConsumer<PaymentConfirmedConsumer>();
            x.AddConsumer<TicketIssuedConsumer>();
            x.AddConsumer<ReservationExpiredConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("RabbitMQ") ?? "rabbitmq://localhost");
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
