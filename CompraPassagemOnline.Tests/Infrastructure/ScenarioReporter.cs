using Xunit.Abstractions;

namespace CompraPassagemOnline.Tests.Infrastructure;

public sealed class ScenarioReporter
{
    private readonly ITestOutputHelper _output;

    public ScenarioReporter(ITestOutputHelper output) => _output = output;

    public void BeginScenario(int number, string title) =>
        _output.WriteLine($"\n[Cenário {number}] {title}");

    public void Step(string description, string result) =>
        _output.WriteLine($"  {description,-42} {result}");

    public void FinalState(string description) =>
        _output.WriteLine($"  Estado final — {description}");

    public void Note(string message) =>
        _output.WriteLine($"  Nota: {message}");
}
