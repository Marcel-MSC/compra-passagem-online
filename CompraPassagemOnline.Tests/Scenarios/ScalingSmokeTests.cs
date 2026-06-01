using System.Net;
using CompraPassagemOnline.Tests.Infrastructure;
using FluentAssertions;
using Xunit.Abstractions;

namespace CompraPassagemOnline.Tests.Scenarios;

[Collection(nameof(ChallengeScenarioCollection))]
public sealed class ScalingSmokeTests
{
    private readonly ChallengeScenarioFixture _fixture;
    private readonly ScenarioReporter _report;

    public ScalingSmokeTests(ChallengeScenarioFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _report = new ScenarioReporter(output);
    }

    [Fact]
    public async Task Parallel_reservations_on_different_seats_all_succeed()
    {
        const int parallelCount = 20;
        var flow = new PurchaseFlowClient(_fixture.Client);
        var seats = await flow.GetAvailableSeatsAsync(_fixture.SeedTripId);
        seats.Count.Should().BeGreaterThanOrEqualTo(parallelCount);

        _report.BeginScenario(4, $"Escala (smoke): {parallelCount} reservas paralelas em assentos distintos");

        var targets = seats.Take(parallelCount).ToList();
        var started = DateTime.UtcNow;

        var tasks = targets.Select((seat, index) =>
            flow.ReserveSeatAsync(
                _fixture.SeedTripId,
                seat.Id,
                $"scale-user-{index}")).ToArray();

        var responses = await Task.WhenAll(tasks);
        var elapsed = DateTime.UtcNow - started;

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        _report.Step("Reservas com sucesso", $"{successCount}/{parallelCount}");
        _report.Step("Conflitos indevidos", conflictCount.ToString());
        _report.Step("Tempo total", $"{elapsed.TotalMilliseconds:F0} ms");

        successCount.Should().Be(parallelCount);
        conflictCount.Should().Be(0);
        _report.FinalState($"Throughput ~{parallelCount / elapsed.TotalSeconds:F1} reservas/s (ambiente de teste)");
    }
}
