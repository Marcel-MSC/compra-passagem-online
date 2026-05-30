using CompraPassagemOnline.Application;
using CompraPassagemOnline.Infrastructure;
using CompraPassagemOnline.Infrastructure.Persistence;
using CompraPassagemOnline.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReservationExpiryWorker>();
builder.Services.AddHostedService<PaymentReconcileWorker>();

var host = builder.Build();

await DatabaseSeeder.SeedAsync(host.Services);

host.Run();
