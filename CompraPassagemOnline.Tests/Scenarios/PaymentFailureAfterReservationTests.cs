using CompraPassagemOnline.Domain.Enums;
using CompraPassagemOnline.Tests.Infrastructure;
using FluentAssertions;
using Xunit.Abstractions;

namespace CompraPassagemOnline.Tests.Scenarios;

[Collection(nameof(ChallengeScenarioCollection))]
public sealed class PaymentFailureAfterReservationTests
{
    private readonly ChallengeScenarioFixture _fixture;
    private readonly ScenarioReporter _report;

    public PaymentFailureAfterReservationTests(ChallengeScenarioFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _report = new ScenarioReporter(output);
    }

    [Fact]
    public async Task Payment_failure_triggers_compensation_and_releases_seat()
    {
        var flow = new PurchaseFlowClient(_fixture.Client);
        var seat = (await flow.GetAvailableSeatsAsync(_fixture.SeedTripId)).First();

        _report.BeginScenario(2, "Falha no pagamento após reserva");

        var reservation = await flow.ReserveSeatOrNullAsync(
            _fixture.SeedTripId,
            seat.Id,
            "user-payment-fail");
        reservation.Should().NotBeNull();
        _report.Step("Reserva criada", "OK (201)");

        var order = await flow.CreateOrderAsync(reservation!.Id);
        _report.Step("Pedido criado", $"OK ({order.Status})");

        var payment = await flow.PayOrNullAsync(order.Id, $"fail-{Guid.NewGuid():N}", simulateFailure: true);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(nameof(PaymentStatus.Failed));
        _report.Step("Pagamento simulado", "FALHOU (compensação)");

        var seatAfter = await flow.GetSeatAsync(_fixture.SeedTripId, seat.Id);
        seatAfter!.Status.Should().Be(SeatStatus.Available);
        _report.FinalState($"Assento: {seatAfter.Status} | Pagamento: {payment.Status}");
        _report.Note("Pedido cancelado e reserva liberada via CompensateOrderCommand.");
    }
}
