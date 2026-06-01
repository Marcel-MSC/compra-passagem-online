using System.Net;
using CompraPassagemOnline.Domain.Enums;
using CompraPassagemOnline.Tests.Infrastructure;
using FluentAssertions;
using Xunit.Abstractions;

namespace CompraPassagemOnline.Tests.Scenarios;

[Collection(nameof(ChallengeScenarioCollection))]
public sealed class ConcurrentSeatPurchaseTests
{
    private readonly ChallengeScenarioFixture _fixture;
    private readonly ScenarioReporter _report;

    public ConcurrentSeatPurchaseTests(ChallengeScenarioFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _report = new ScenarioReporter(output);
    }

    [Fact]
    public async Task Two_users_reserving_same_seat_one_succeeds_one_gets_conflict()
    {
        var flow = new PurchaseFlowClient(_fixture.Client);
        var seats = await flow.GetAvailableSeatsAsync(_fixture.SeedTripId);
        var seat = seats.First();

        _report.BeginScenario(1, $"Concorrência no assento {seat.SeatNumber}");

        var taskA = flow.ReserveSeatAsync(_fixture.SeedTripId, seat.Id, "user-a-concurrent");
        var taskB = flow.ReserveSeatAsync(_fixture.SeedTripId, seat.Id, "user-b-concurrent");

        var responses = await Task.WhenAll(taskA, taskB);
        var success = responses.Single(r => r.IsSuccessStatusCode);
        var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);

        _report.Step("Usuário A reserva", success == responses[0] ? "OK (201)" : conflict == responses[0] ? "CONFLITO (409)" : "—");
        _report.Step("Usuário B reserva", success == responses[1] ? "OK (201)" : "CONFLITO (409)");

        success.StatusCode.Should().Be(HttpStatusCode.Created);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var seatAfter = await flow.GetSeatAsync(_fixture.SeedTripId, seat.Id);
        seatAfter!.Status.Should().Be(SeatStatus.Held);
        _report.FinalState($"Assento: {seatAfter.Status} | Reservas bem-sucedidas: 1");
    }
}

[CollectionDefinition(nameof(ChallengeScenarioCollection))]
public sealed class ChallengeScenarioCollection : ICollectionFixture<ChallengeScenarioFixture>
{
}
