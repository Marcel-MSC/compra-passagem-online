using CompraPassagemOnline.Application.Ticketing;
using CompraPassagemOnline.Domain.Enums;
using CompraPassagemOnline.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CompraPassagemOnline.Tests.Scenarios;

[Collection(nameof(ChallengeScenarioCollection))]
public sealed class ReservationAbandonmentTests
{
    private readonly ChallengeScenarioFixture _fixture;
    private readonly ScenarioReporter _report;

    public ReservationAbandonmentTests(ChallengeScenarioFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _report = new ScenarioReporter(output);
    }

    [Fact]
    public async Task Abandoned_reservation_expires_and_seat_becomes_available()
    {
        var flow = new PurchaseFlowClient(_fixture.Client);
        var seat = (await flow.GetAvailableSeatsAsync(_fixture.SeedTripId)).First();

        _report.BeginScenario(3, "Abandono após reservar (sem pagamento)");

        var reservation = await flow.ReserveSeatOrNullAsync(
            _fixture.SeedTripId,
            seat.Id,
            "user-abandon");
        reservation.Should().NotBeNull();
        _report.Step("Reserva criada (TTL 5s em Testing)", "OK (201)");

        var held = await flow.GetSeatAsync(_fixture.SeedTripId, seat.Id);
        held!.Status.Should().Be(SeatStatus.Held);
        _report.Step("Assento após reserva", "Held");

        await Task.Delay(TimeSpan.FromSeconds(6));
        _report.Step("Aguardou expiração", "6 segundos");

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new ExpireReservationCommand(reservation!.Id));
        }

        var seatAfter = await flow.GetSeatAsync(_fixture.SeedTripId, seat.Id);
        seatAfter!.Status.Should().Be(SeatStatus.Available);
        _report.FinalState($"Assento: {seatAfter.Status} | Reserva expirada pelo worker/handler");
        _report.Note("Em produção o ReservationExpiryWorker executa o mesmo comando a cada 30s.");
    }
}
