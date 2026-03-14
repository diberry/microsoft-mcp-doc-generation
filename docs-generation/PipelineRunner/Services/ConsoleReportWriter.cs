namespace PipelineRunner.Services;

public sealed class ConsoleReportWriter : IReportWriter
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public ConsoleReportWriter(TextWriter? output = null, TextWriter? error = null)
    {
        _output = output ?? Console.Out;
        _error = error ?? Console.Error;
    }

    public void Info(string message)
        => _output.WriteLine(message);

    public void Warning(string message)
        => _output.WriteLine($"WARNING: {message}");

    public void Error(string message)
        => _error.WriteLine($"ERROR: {message}");
}
