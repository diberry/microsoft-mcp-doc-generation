namespace PipelineRunner.Services;

public interface IReportWriter
{
    void Info(string message);

    void Warning(string message);

    void Error(string message);
}
